using SignalNine.Core.Data.Config;

namespace SignalNine.Core.Interfaces;

/// <summary>
/// Configures the global Serilog logger for the SignalNine process.
/// </summary>
public interface ISerilogService
{
    /// <summary>
    /// Applies the Serilog configuration described by the application configuration.
    /// </summary>
    /// <param name="config">The application configuration to apply.</param>
    void Configure(SignalNineConfig config);
}
