using System.Text.Json.Serialization;

namespace SignalNine.Web.Data.Logging;

/// <summary>
/// SignalR payload broadcast for every Serilog log event captured by the EventSink.
/// Serialized in camelCase to match the frontend <c>LogEntry</c> contract.
/// </summary>
public class LogEntryResponse
{
    /// <summary>
    /// ISO-8601 UTC timestamp of the log event.
    /// </summary>
    [JsonPropertyName("ts")]
    public string Ts { get; init; } = string.Empty;

    /// <summary>
    /// Normalized level: one of <c>debug</c>, <c>info</c>, <c>warn</c>, <c>error</c>.
    /// </summary>
    [JsonPropertyName("level")]
    public string Level { get; init; } = string.Empty;

    /// <summary>
    /// SourceContext of the log event (typically the logger name / class name).
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Rendered log message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Extra Serilog properties associated with the event (may be omitted).
    /// </summary>
    [JsonPropertyName("props")]
    public IReadOnlyDictionary<string, object?>? Props { get; init; }
}
