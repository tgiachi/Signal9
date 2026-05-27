namespace SignalNine.Core.Data.Config;

public class JobSystemConfig
{
    private const int DefaultMaxConcurrentJobs = 2;
    private const int DefaultMaxLogEntriesPerJob = 500;

    public int MaxConcurrentJobs { get; set; } = DefaultMaxConcurrentJobs;

    public int MaxLogEntriesPerJob { get; set; } = DefaultMaxLogEntriesPerJob;
}
