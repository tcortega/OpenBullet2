using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IronPython.Compiler;
using IronPython.Hosting;
using IronPython.Runtime;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using RuriLib.Exceptions;
using RuriLib.Helpers.Blocks;
using RuriLib.Helpers.CSharp;
using RuriLib.Helpers.Transpilers;
using RuriLib.Legacy.LS;
using RuriLib.Legacy.Models;
using RuriLib.Logging;
using RuriLib.Models.Bots;
using RuriLib.Models.Configs;
using RuriLib.Models.Data;
using RuriLib.Models.Data.Resources;
using RuriLib.Models.Data.Resources.Options;
using RuriLib.Models.Proxies;
using RuriLib.Models.Variables;
using RuriLib.Providers.RandomNumbers;
using RuriLib.Providers.UserAgents;
using RuriLib.Services;

namespace RuriLib.Models.Runner;

public class ConfigRunner(Config config, RunnerOptions options)
{
    public IRandomUAProvider RandomUAProvider { get; set; } = null!;
    public IRNGProvider RNGProvider { get; set; } = null!;
    public RuriLibSettingsService RuriLibSettings { get; set; } = null!;
    public PluginRepository PluginRepo { get; set; } = null!;
    public Config Config { get; init; } = config;
    public RunnerOptions Options { get; init; } = options;
    private static ConcurrentDictionary<string, Script> _cachedScripts = new();

    public async Task<RunnerResult> RunAsync()
    {
        // Build the C# script if in Stack or LoliCode mode
        if (Config.Mode is ConfigMode.Stack or ConfigMode.LoliCode && string.IsNullOrWhiteSpace(config.CSharpScript))
        {
            Config.CSharpScript = Config.Mode == ConfigMode.Stack
                ? Stack2CSharpTranspiler.Transpile(Config.Stack, Config.Settings)
                : Loli2CSharpTranspiler.Transpile(Config.LoliCodeScript, Config.Settings);

            // Stacker is not currently available for the startup phase
            Config.StartupCSharpScript = Loli2CSharpTranspiler.Transpile(Config.StartupLoliCodeScript, Config.Settings);
        }

        if (Options.UseProxy && !Options.TestProxy.Contains(':'))
        {
            throw new InvalidProxyException(Options.TestProxy);
        }

        var cts = new CancellationTokenSource();

        var sw = new Stopwatch();
        var wordlistType = RuriLibSettings.Environment.WordlistTypes.First(w => w.Name == Options.WordlistType);
        var dataLine = new DataLine(Options.TestData, wordlistType);
        var proxy = Options.UseProxy ? Proxy.Parse(Options.TestProxy, Options.ProxyType) : null;

        var providers = new Bots.Providers(RuriLibSettings)
        {
            RNG = RNGProvider
        };

        if (!RuriLibSettings.RuriLibSettings.GeneralSettings.UseCustomUserAgentsList)
        {
            providers.RandomUA = RandomUAProvider;
        }

        // Build the BotData
        var botData = new BotData(providers, Config.Settings, new NoOpLogger(), dataLine, proxy, Options.UseProxy)
        {
            CancellationToken = cts.Token,
        };
        using var httpClient = new HttpClient();
        botData.SetObject("httpClient", httpClient);
        var runtime = Python.CreateRuntime();
        var pyengine = runtime.GetEngine("py");
        var pco = (PythonCompilerOptions)pyengine.GetCompilerOptions();
        pco.Module &= ~ModuleOptions.Optimized;
        botData.SetObject("ironPyEngine", pyengine);
        botData.AsyncLocker = new();

        dynamic globals = new ExpandoObject();

        if (!_cachedScripts.TryGetValue(Config.Id, out var script))
        {
            script = new ScriptBuilder()
                .Build(Config.CSharpScript, Config.Settings.ScriptSettings, PluginRepo);
            _cachedScripts.TryAdd(Config.Id, script);
        }
        
        var startupScriptId = $"startup-{Config.Id}";
        if (!_cachedScripts.TryGetValue(startupScriptId, out var startupScript))
        {
            startupScript = new ScriptBuilder().Build(Config.StartupCSharpScript, Config.Settings.ScriptSettings, PluginRepo);
            _cachedScripts.TryAdd(startupScriptId, startupScript);
        }

        // Initialize resources
        Dictionary<string, ConfigResource> resources = new();

        // Resources will need to be disposed of
        foreach (var opt in Config.Settings.DataSettings.Resources)
        {
            try
            {
                resources[opt.Name] = opt switch
                {
                    LinesFromFileResourceOptions x => new LinesFromFileResource(x),
                    RandomLinesFromFileResourceOptions x => new RandomLinesFromFileResource(x),
                    _ => throw new NotImplementedException()
                };
            }
            catch
            {
                // logger.LogInformation("Could not create resource {Name}", opt.Name);
            }
        }

        // Add resources to global variables
        globals.Resources = resources;
        globals.OwnerId = 0;
        globals.JobId = 0;
        var scriptGlobals = new ScriptGlobals(botData, globals);

        // Set custom inputs
        foreach (var input in Config.Settings.InputSettings.CustomInputs)
        {
            (scriptGlobals.input as IDictionary<string, object>)!.Add(input.VariableName, input.DefaultAnswer);
        }

        // [LEGACY] Set up the VariablesList
        if (Config.Mode == ConfigMode.Legacy)
        {
            var slices = new List<Variable>();

            foreach (var slice in dataLine.GetVariables())
            {
                var sliceValue = botData.ConfigSettings.DataSettings.UrlEncodeDataAfterSlicing
                    ? Uri.EscapeDataString(slice.AsString())
                    : slice.AsString();

                slices.Add(new StringVariable(sliceValue) { Name = slice.Name });
            }

            var legacyVariables = new VariablesList(slices);

            foreach (var input in Config.Settings.InputSettings.CustomInputs)
            {
                legacyVariables.Set(new StringVariable(input.DefaultAnswer) { Name = input.VariableName });
            }

            botData.SetObject("legacyVariables", legacyVariables);
        }

        var captures = new List<Capture>();
        try
        {
            sw.Start();

            if (Config.Mode != ConfigMode.Legacy)
            {
                // If the startup script is not empty, execute it
                if (!string.IsNullOrWhiteSpace(Config.StartupCSharpScript))
                {
                    // This data is temporary and will not be persisted to the bots, it is
                    // only used in this context to be able to use variables e.g. data.SOURCE
                    // and other things like providers, settings, logger.
                    // By default it doesn't support proxies.
                    var startupData = new BotData(providers, Config.Settings, new NoOpLogger(),
                        new DataLine(string.Empty, wordlistType))
                    {
                        CancellationToken = cts.Token,
                    };

                    var startupGlobals = new ScriptGlobals(startupData, globals);
                    await startupScript.RunAsync(startupGlobals, null, cts.Token).ConfigureAwait(false);
                }

                var state = await script.RunAsync(scriptGlobals, null, cts.Token).ConfigureAwait(false);

                foreach (var scriptVar in state.Variables)
                {
                    try
                    {
                        var type = DescriptorsRepository.ToVariableType(scriptVar.Type);
                        if (!type.HasValue || scriptVar.Name.StartsWith("tmp_")) continue;

                        var variable =
                            DescriptorsRepository.ToVariable(scriptVar.Name, scriptVar.Type, scriptVar.Value);
                        variable.MarkedForCapture = botData.MarkedForCapture.Contains(scriptVar.Name);

                        if (variable.MarkedForCapture)
                        {
                            captures.Add(new Capture
                            {
                                Name = scriptVar.Name,
                                Value = scriptVar.Value?.ToString() ?? string.Empty
                            });
                        }
                    }
                    catch
                    {
                        // The type is not supported, e.g. it was generated using custom C# code and not blocks
                        // so we just disregard it
                    }
                }
            }
            else
            {
                // [LEGACY] Run the LoliScript in the old way
                var loliScript = new LoliScript(Config.LoliScript);
                var lsGlobals = new LSGlobals(botData);

                do
                {
                    if (cts.IsCancellationRequested)
                    {
                        break;
                    }

                    await loliScript.TakeStep(lsGlobals).ConfigureAwait(false);

                    // Options.Variables.Clear();
                    // var legacyVariables = botData.TryGetObject<VariablesList>("legacyVariables");
                    // Options.Variables.AddRange(legacyVariables.Variables);
                    // Options.Variables.AddRange(lsGlobals.Globals.Variables);
                } while (loliScript.CanProceed);
            }
        }
        catch (OperationCanceledException)
        {
            botData.STATUS = "ERROR";
        }
        catch (Exception ex)
        {
            botData.STATUS = "ERROR";
            var logErrorMessage = RuriLibSettings.RuriLibSettings.GeneralSettings.VerboseMode
                ? ex.ToString()
                : ex.Message;

            botData.ERROR = logErrorMessage;
            //
            // logger.LogInformation("[ExecutionInfo] {ex.GetType().Name}: {logErrorMessage}");
        }
        finally
        {
            sw.Stop();
            botData.DisposeObjectsExcept();

            // Dispose resources
            foreach (var resource in resources.Where(r => r.Value is IDisposable)
                         .Select(r => r.Value).Cast<IDisposable>())
            {
                resource.Dispose();
            }

            botData.AsyncLocker.Dispose();
        }

        return new RunnerResult
        {
            BotData = botData,
            Captures = captures
        };
    }
}
