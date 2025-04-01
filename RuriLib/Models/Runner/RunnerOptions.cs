using RuriLib.Models.Proxies;

namespace RuriLib.Models.Runner;

/// <summary>
/// Options for the OpenBullet 2 config debugger.
/// </summary>
public class RunnerOptions
{
    /// <summary>
    /// The data under test.
    /// </summary>
    public string TestData { get; set; } = "";

    /// <summary>
    /// The Wordlist Type to use when slicing the <see cref="TestData"/>.
    /// </summary>
    public required string WordlistType { get; set; }

    /// <summary>
    /// Whether the provided <see cref="TestProxy"/> should be used.
    /// </summary>
    public bool UseProxy { get; set; } = false;

    /// <summary>
    /// The proxy to use for remote connections.
    /// </summary>
    public string TestProxy { get; set; } = "";

    /// <summary>
    /// The type of <see cref="TestProxy"/>.
    /// </summary>
    public ProxyType ProxyType { get; set; } = ProxyType.Http;
}
