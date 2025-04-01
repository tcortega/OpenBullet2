using FluentValidation;
using RuriLib.Models.Proxies;

namespace OpenBullet2.Web.Dtos.Config;

/// <summary>
/// DTO representing the request to run a config.
/// </summary>
public class RunConfigDto
{
    /// <summary>
    /// The id of the config to run.
    /// </summary>
    public required string ConfigId { get; init; }
    
    /// <summary>
    /// The data to run the config with.
    /// </summary>
    public required string Data { get; init; }

    /// <summary>
    /// The Wordlist Type to use when slicing the <see cref="Data"/>.
    /// </summary>
    public string WordlistType { get; init; } = "Credentials";

    /// <summary>
    /// The proxy to use for the request. If not provided, will run the config without a proxy.
    /// </summary>
    public string? Proxy { get; init; }

    /// <summary>
    /// The proxy type to use for the request.
    /// </summary>
    public ProxyType? ProxyType { get; init; }
}

internal class RunConfigDtoValidator : AbstractValidator<RunConfigDto>
{
    public RunConfigDtoValidator()
    {
        RuleFor(dto => dto.ConfigId)
            .NotEmpty()
            .WithMessage("ConfigId must not be empty.");
        
        RuleFor(dto => dto.Data)
            .NotNull()
            .WithMessage("Data must not be null.");
        
        RuleFor(dto => dto.Proxy)
            .NotEmpty()
            .When(dto => dto.Proxy != null)
            .WithMessage("If a proxy is provided, it cannot be empty.");
        
        RuleFor(dto => dto.ProxyType)
            .NotNull()
            .When(dto => !string.IsNullOrWhiteSpace(dto.Proxy))
            .WithMessage("If a proxy is specified, a proxy type must be provided.");
    }
}
