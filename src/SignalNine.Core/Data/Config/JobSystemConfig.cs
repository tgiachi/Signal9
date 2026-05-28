namespace SignalNine.Core.Data.Config;

public class JobSystemConfig
{
    private const int DefaultMaxConcurrentJobs = 2;
    private const int DefaultMaxLogEntriesPerJob = 500;

    public int MaxConcurrentJobs { get; set; } = DefaultMaxConcurrentJobs;

    public int MaxLogEntriesPerJob { get; set; } = DefaultMaxLogEntriesPerJob;

    /// <summary>
    /// When true (default), the web orchestrator also runs an in-process JobWorkerService for
    /// the "workers" stream — useful for single-machine standalone deploys. Set to false in
    /// distributed setups where only remote SignalNine.Worker processes should consume
    /// jobs:workers. The Internal loop (library.scan etc) always runs on the web.
    /// </summary>
    public bool RunInProcessWorker { get; set; } = true;
}
