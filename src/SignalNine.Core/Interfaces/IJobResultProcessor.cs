namespace SignalNine.Core.Interfaces;

public interface IJobResultProcessor
{
    string JobType { get; }
    Task ApplyAsync(Guid jobId, string? resultJson, CancellationToken cancellationToken = default);
}
