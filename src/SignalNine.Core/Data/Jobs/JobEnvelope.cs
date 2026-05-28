namespace SignalNine.Core.Data.Jobs;

public enum JobStreamTarget
{
    Internal = 0,
    Workers = 1,
    Scheduled = 2
}

public sealed record JobEnvelope(
    Guid JobId,
    string Type,
    string PayloadJson,
    string WorkDir,
    int Attempt,
    DateTimeOffset EnqueuedAt
);

public sealed record QueuedJob(string StreamId, JobEnvelope Envelope);
