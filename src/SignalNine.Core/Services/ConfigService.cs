using SignalNine.Core.Data.Config;
using SignalNine.Core.Directories;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Toml;
using SignalNine.Core.Types;

namespace SignalNine.Core.Services;

public class ConfigService : IConfigService
{
    private const string FileName = "signalnine.toml";

    private readonly DirectoriesConfig _directoriesConfig;

    public string ConfigPath { get; }

    public ConfigService(DirectoriesConfig directoriesConfig)
    {
        ArgumentNullException.ThrowIfNull(directoriesConfig);

        _directoriesConfig = directoriesConfig;
        ConfigPath = Path.Combine(_directoriesConfig.GetPath(DirectoryType.Configs), FileName);
    }

    public async Task<SignalNineConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigPath))
        {
            var defaultConfig = new SignalNineConfig();
            await SaveAsync(defaultConfig, cancellationToken).ConfigureAwait(false);

            return defaultConfig;
        }

        var toml = await File.ReadAllTextAsync(ConfigPath, cancellationToken).ConfigureAwait(false);
        var config = TomlUtils.Deserialize(toml, SignalNineTomlContext.Default.SignalNineConfig);
        var updated = false;

        if (config.Jwt is null)
        {
            config.Jwt = new JwtConfig();
            updated = true;
        }

        if (config.JobSystem is null)
        {
            config.JobSystem = new JobSystemConfig();
            updated = true;
        }

        if (!HasConfigSection(toml, nameof(SignalNineConfig.Jwt)) ||
            !HasConfigSection(toml, nameof(SignalNineConfig.JobSystem)))
        {
            updated = true;
        }

        if (updated)
        {
            await SaveAsync(config, cancellationToken).ConfigureAwait(false);
        }

        return config;
    }

    public async Task SaveAsync(SignalNineConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var directory = Path.GetDirectoryName(ConfigPath);

        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var toml = TomlUtils.Serialize(config, SignalNineTomlContext.Default.SignalNineConfig);
        await File.WriteAllTextAsync(ConfigPath, toml, cancellationToken).ConfigureAwait(false);
    }

    private static bool HasConfigSection(string toml, string sectionName)
        => toml.Contains($"[{sectionName}]", StringComparison.Ordinal) ||
           toml.Contains($"{sectionName}.", StringComparison.Ordinal) ||
           toml.Contains($"{sectionName} =", StringComparison.Ordinal);
}
