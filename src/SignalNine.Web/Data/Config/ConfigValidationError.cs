using System.Text.Json.Serialization;

namespace SignalNine.Web.Data.Config;

/// <summary>
/// HTTP 422 response body returned when a posted TOML configuration fails validation.
/// Field names are camelCase to match the frontend contract.
/// </summary>
public class ConfigValidationError
{
    /// <summary>
    /// Human-readable validation message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 1-based line number where the error was detected, if available.
    /// </summary>
    [JsonPropertyName("line")]
    public int? Line { get; init; }

    /// <summary>
    /// 1-based column number where the error was detected, if available.
    /// </summary>
    [JsonPropertyName("column")]
    public int? Column { get; init; }
}
