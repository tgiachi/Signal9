using System.Text.Json;

namespace SignalNine.Web.Data.Jobs;

/// <summary>
/// Payload used to enqueue a new job.
/// </summary>
public sealed record EnqueueJobRequest
{
    /// <summary>
    /// Gets the job type. Must be a non-blank value.
    /// </summary>
    public required string Type { get; init; } = "";

    /// <summary>
    /// Gets the job payload.
    /// </summary>
    public JsonElement Payload { get; init; }
}
