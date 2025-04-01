using OpenBullet2.Core.Services;
using OpenBullet2.Web.Dtos.Config;
using OpenBullet2.Web.Exceptions;
using RuriLib.Models.Proxies;
using RuriLib.Models.Runner;
using RuriLib.Providers.RandomNumbers;
using RuriLib.Providers.UserAgents;
using RuriLib.Services;

namespace OpenBullet2.Web.Services;

/// <summary>
/// The service that runs configs.
/// </summary>
/// <param name="pluginRepo"></param>
/// <param name="randomUAProvider"></param>
/// <param name="rngProvider"></param>
/// <param name="rlSettingsService"></param>
/// <param name="configService"></param>
/// <param name="logger"></param>
public sealed class ConfigRunnerService(
    PluginRepository pluginRepo,
    IRandomUAProvider randomUAProvider,
    IRNGProvider rngProvider,
    RuriLibSettingsService rlSettingsService,
    ConfigService configService,
    ILogger<ConfigRunnerService> logger)
{
    /// <summary>
    /// The method that runs a config.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    /// <exception cref="EntryNotFoundException"></exception>
    public async Task<ConfigRunResultDto> RunConfigAsync(RunConfigDto dto)
    {
        var config = configService.Configs.Find(c => c.Id == dto.ConfigId);
        if (config is null)
        {
            throw new EntryNotFoundException(ErrorCode.ConfigNotFound,
                dto.ConfigId, nameof(ConfigService));
        }

        logger.LogInformation("Running config {ConfigName}", config.Metadata.Name);
        var options = new RunnerOptions
        {
            TestData = dto.Data,
            WordlistType = dto.WordlistType,
            UseProxy = dto.Proxy is not null,
            TestProxy = dto.Proxy ?? string.Empty,
            ProxyType = dto.ProxyType ?? ProxyType.Http,
        };

        var runner = new ConfigRunner(config, options)
        {
            PluginRepo = pluginRepo,
            RandomUAProvider = randomUAProvider,
            RNGProvider = rngProvider,
            RuriLibSettings = rlSettingsService
        };
        var result = await runner.RunAsync();

        logger.LogInformation("Config {ConfigName} finished with status {Status}",
            config.Metadata.Name, result.BotData.STATUS);

        var captures = result.Captures.Select(c => new CaptureDto
        {
            Name = c.Name,
            Value = c.Value
        });

        return new ConfigRunResultDto
        {
            Status = result.BotData.STATUS,
            Error = result.BotData.ERROR,
            Captures = captures
        };
    }
}
