using SignalNine.Core.Data.Config;

namespace SignalNine.Core.Interfaces;

/// <summary>
/// Loads and persists the SignalNine application configuration.
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// Gets the absolute path of the TOML configuration file.
    /// </summary>
    string ConfigPath { get; }

    /// <summary>
    /// Loads the SignalNine configuration, creating the default file when it does not exist.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the file operation.</param>
    /// <returns>The loaded SignalNine configuration.</returns>
    Task<SignalNineConfig> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the SignalNine configuration to the TOML configuration file.
    /// </summary>
    /// <param name="config">The configuration to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the file operation.</param>
    Task SaveAsync(SignalNineConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the supplied TOML text deserializes into a valid <see cref="SignalNineConfig" />.
    /// </summary>
    /// <param name="toml">The TOML text to validate.</param>
    /// <returns>A result describing success or the validation error (with optional line/column when available).</returns>
    TomlValidationResult Validate(string toml);

    /// <summary>
    /// Persists the supplied raw TOML text to the configuration file after a successful <see cref="Validate" />.
    /// </summary>
    /// <param name="toml">The validated TOML text to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the file operation.</param>
    Task SaveRawAsync(string toml, CancellationToken cancellationToken = default);
}
