using SignalNine.Core.Interfaces;

namespace SignalNine.Core.Services;

/// <summary>
/// Phase 1 placeholder. Phase 4 replaces this with per-job-type processors that apply
/// result events to the database. For now handlers still mutate state directly, so this
/// is a no-op safety net that prevents JobBusToManagerAdapter from throwing on unknown
/// result event types.
/// </summary>
public sealed class LegacyShimResultProcessor : IJobResultProcessor
{
    public string JobType => "*";
    public Task ApplyAsync(Guid jobId, string? resultJson, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
