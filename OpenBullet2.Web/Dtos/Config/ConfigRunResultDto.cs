namespace OpenBullet2.Web.Dtos.Config;

/// <summary>
/// DTO representing the result of a config run.
/// </summary>
public class ConfigRunResultDto
{
    /// <summary>
    /// The status the config ended with.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// The error message if the config ended with an error.
    /// </summary>
    public required string? Error { get; init; }

    /// <summary>
    /// The list of captures made by the config.
    /// </summary>
    public required IEnumerable<CaptureDto>? Captures { get; init; }
}

/// <summary>
/// DTO representing a captured variable.
/// </summary>
public class CaptureDto
{
    /// <summary>
    /// The name of the variable.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The value of the variable.
    /// </summary>
    public required string Value { get; init; }
}
