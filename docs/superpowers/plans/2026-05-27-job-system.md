# Job System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an in-memory SignalNine job system with configurable concurrency, cancellation, progress tracking, HTTP status endpoints, SignalR status updates, and SignalR log streaming.

**Architecture:** Jobs are queued and tracked in memory for the lifetime of the process. Core owns job contracts, snapshots, state transitions, progress, logs, and the in-memory queue; Web owns the hosted worker, HTTP endpoints, and SignalR hubs. Configuration is loaded from TOML through `SignalNineConfig.JobSystem.MaxConcurrentJobs`.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, SignalR, `BackgroundService`, `System.Threading.Channels`, xUnit, `WebApplicationFactory`.

---

## Scope Decisions

- The queue is internal and in-memory. Queued jobs, progress, and logs are lost when the process restarts.
- Job concurrency is controlled by `SignalNineConfig.JobSystem.MaxConcurrentJobs`.
- A job can be canceled while queued or running.
- Progress is stored as percent plus message.
- Logs are retained in memory per job, capped by `SignalNineConfig.JobSystem.MaxLogEntriesPerJob`.
- HTTP endpoints are under `/api/jobs`.
- SignalR hubs are under `/hubs/jobs/status` and `/hubs/jobs/logs`.
- Job endpoints require JWT authorization. Tests mint JWTs directly with `JwtTokenService`.

## File Structure

- Modify `src/SignalNine.Core/Data/Config/SignalNineConfig.cs`: add `JobSystem`.
- Create `src/SignalNine.Core/Data/Config/JobSystemConfig.cs`: concurrency and retention defaults.
- Modify `src/SignalNine.Core/Toml/SignalNineTomlContext.cs`: register `JobSystemConfig`.
- Modify `src/SignalNine.Core/Services/ConfigService.cs`: backfill missing `[JobSystem]`.
- Create `src/SignalNine.Core/Types/JobStateType.cs`: queued/running/completed/failed/canceled.
- Create `src/SignalNine.Core/Types/JobLogLevelType.cs`: trace/debug/information/warning/error/critical.
- Create `src/SignalNine.Core/Data/Jobs/*.cs`: command, snapshot, progress, log entry, execution context.
- Create `src/SignalNine.Core/Interfaces/IJobHandler.cs`: job handler contract.
- Create `src/SignalNine.Core/Interfaces/IJobManager.cs`: queue and state contract.
- Create `src/SignalNine.Core/Interfaces/IJobNotificationPublisher.cs`: status/log publishing contract.
- Create `src/SignalNine.Core/Services/InMemoryJobManager.cs`: in-memory queue, state, progress, logs, cancellation.
- Create `src/SignalNine.Core/Services/NoOpJobNotificationPublisher.cs`: default publisher used before SignalR wiring.
- Create `src/SignalNine.Web/Services/JobWorkerService.cs`: hosted worker with configurable concurrency.
- Create `src/SignalNine.Web/Services/SignalRJobNotificationPublisher.cs`: broadcasts status/log updates.
- Create `src/SignalNine.Web/Hubs/JobStatusHub.cs`: status SignalR hub.
- Create `src/SignalNine.Web/Hubs/JobLogHub.cs`: log SignalR hub.
- Create `src/SignalNine.Web/Data/Jobs/*.cs`: request/response DTOs.
- Create `src/SignalNine.Web/Endpoints/JobEndpoints.cs`: minimal API mappings.
- Modify `src/SignalNine.Web/Program.cs`: register services, hosted worker, hubs, endpoints.
- Create `tests/SignalNine.Tests/Support/Jobs/*.cs`: fake handlers.
- Create `tests/SignalNine.Tests/Support/Web/*.cs`: JWT client helper.
- Create `tests/SignalNine.Tests/Core/Services/InMemoryJobManagerTests.cs`.
- Create `tests/SignalNine.Tests/Web/JobEndpointTests.cs`.
- Create `tests/SignalNine.Tests/Web/JobSignalRTests.cs`.

---

### Task 1: Job System Config

**Files:**
- Modify: `src/SignalNine.Core/Data/Config/SignalNineConfig.cs`
- Create: `src/SignalNine.Core/Data/Config/JobSystemConfig.cs`
- Modify: `src/SignalNine.Core/Toml/SignalNineTomlContext.cs`
- Modify: `src/SignalNine.Core/Services/ConfigService.cs`
- Test: `tests/SignalNine.Tests/Core/Services/ConfigServiceTests.cs`

- [ ] **Step 1: Write the failing config tests**

Add these constants and assertions to `ConfigServiceTests`:

```csharp
private const int DefaultMaxConcurrentJobs = 2;
private const int DefaultMaxLogEntriesPerJob = 500;
private const int TestMaxConcurrentJobs = 4;
private const int TestMaxLogEntriesPerJob = 25;
```

In `LoadAsync_MissingConfigFile_CreatesDefaultConfigFile`, assert:

```csharp
Assert.Equal(DefaultMaxConcurrentJobs, config.JobSystem.MaxConcurrentJobs);
Assert.Equal(DefaultMaxLogEntriesPerJob, config.JobSystem.MaxLogEntriesPerJob);
```

In `LoadAsync_SavedConfigFile_ReturnsSavedConfig`, set:

```csharp
JobSystem =
{
    MaxConcurrentJobs = TestMaxConcurrentJobs,
    MaxLogEntriesPerJob = TestMaxLogEntriesPerJob
}
```

and assert:

```csharp
Assert.Equal(expectedConfig.JobSystem.MaxConcurrentJobs, config.JobSystem.MaxConcurrentJobs);
Assert.Equal(expectedConfig.JobSystem.MaxLogEntriesPerJob, config.JobSystem.MaxLogEntriesPerJob);
```

Add this test:

```csharp
[Fact]
public async Task LoadAsync_LegacyConfigFile_AddsJobSystemDefaults()
{
    var directoriesConfig = CreateDirectoriesConfig();
    var service = new ConfigService(directoriesConfig);

    await File.WriteAllTextAsync(
        service.ConfigPath,
        """
        LogLevel = 3
        LogToFile = true
        DatabaseUrl = "sqlite://{ROOT_DIRECTORY}/db/signalnine.db"
        DatabaseType = 0
        [Jwt]
        Issuer = "SignalNine"
        Audience = "SignalNine"
        Secret = "signalnine-development-secret-change-before-production"
        ExpirationMinutes = 60
        """
    );

    var config = await service.LoadAsync();
    var savedToml = await File.ReadAllTextAsync(service.ConfigPath);

    Assert.Equal(DefaultMaxConcurrentJobs, config.JobSystem.MaxConcurrentJobs);
    Assert.Contains("JobSystem", savedToml);
    Assert.Contains("MaxConcurrentJobs", savedToml);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
dotnet test tests/SignalNine.Tests/SignalNine.Tests.csproj --filter ConfigServiceTests
```

Expected: compile fails because `SignalNineConfig.JobSystem` and `JobSystemConfig` do not exist.

- [ ] **Step 3: Implement config model and TOML registration**

Create `src/SignalNine.Core/Data/Config/JobSystemConfig.cs`:

```csharp
namespace SignalNine.Core.Data.Config;

public class JobSystemConfig
{
    private const int DefaultMaxConcurrentJobs = 2;
    private const int DefaultMaxLogEntriesPerJob = 500;

    public int MaxConcurrentJobs { get; set; } = DefaultMaxConcurrentJobs;

    public int MaxLogEntriesPerJob { get; set; } = DefaultMaxLogEntriesPerJob;
}
```

Add to `SignalNineConfig`:

```csharp
public JobSystemConfig JobSystem { get; set; } = new();
```

Add to `SignalNineTomlContext`:

```csharp
[TomlSerializable(typeof(JobSystemConfig))]
```

- [ ] **Step 4: Implement config backfill**

Refactor `ConfigService.LoadAsync` so it calls:

```csharp
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
```

Replace `HasJwtConfig` with:

```csharp
private static bool HasConfigSection(string toml, string sectionName)
    => toml.Contains($"[{sectionName}]", StringComparison.Ordinal) ||
       toml.Contains($"{sectionName}.", StringComparison.Ordinal) ||
       toml.Contains($"{sectionName} =", StringComparison.Ordinal);
```

- [ ] **Step 5: Run test to verify it passes**

Run:

```bash
dotnet test tests/SignalNine.Tests/SignalNine.Tests.csproj --filter ConfigServiceTests
```

Expected: all `ConfigServiceTests` pass.

- [ ] **Step 6: Commit**

```bash
git add src/SignalNine.Core/Data/Config src/SignalNine.Core/Toml/SignalNineTomlContext.cs src/SignalNine.Core/Services/ConfigService.cs tests/SignalNine.Tests/Core/Services/ConfigServiceTests.cs
git commit -m "feat(config): add job system settings"
```

---

### Task 2: Core Job Contracts

**Files:**
- Create: `src/SignalNine.Core/Types/JobStateType.cs`
- Create: `src/SignalNine.Core/Types/JobLogLevelType.cs`
- Create: `src/SignalNine.Core/Data/Jobs/EnqueueJobCommand.cs`
- Create: `src/SignalNine.Core/Data/Jobs/JobProgressSnapshot.cs`
- Create: `src/SignalNine.Core/Data/Jobs/JobSnapshot.cs`
- Create: `src/SignalNine.Core/Data/Jobs/JobLogEntry.cs`
- Create: `src/SignalNine.Core/Data/Jobs/JobExecutionContext.cs`
- Create: `src/SignalNine.Core/Interfaces/IJobHandler.cs`
- Create: `src/SignalNine.Core/Interfaces/IJobManager.cs`
- Create: `src/SignalNine.Core/Interfaces/IJobNotificationPublisher.cs`

- [ ] **Step 1: Create job state and log level enums**

```csharp
namespace SignalNine.Core.Types;

public enum JobStateType
{
    Queued,
    Running,
    Completed,
    Failed,
    Canceled
}
```

```csharp
namespace SignalNine.Core.Types;

public enum JobLogLevelType
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}
```

- [ ] **Step 2: Create job data models**

`EnqueueJobCommand.cs`:

```csharp
namespace SignalNine.Core.Data.Jobs;

public class EnqueueJobCommand
{
    public string Type { get; set; } = "";

    public string PayloadJson { get; set; } = "{}";
}
```

`JobProgressSnapshot.cs`:

```csharp
namespace SignalNine.Core.Data.Jobs;

public class JobProgressSnapshot
{
    public int Percent { get; set; }

    public string Message { get; set; } = "";

    public DateTimeOffset UpdatedAt { get; set; }
}
```

`JobSnapshot.cs`:

```csharp
using SignalNine.Core.Types;

namespace SignalNine.Core.Data.Jobs;

public class JobSnapshot
{
    public Guid Id { get; set; }

    public string Type { get; set; } = "";

    public string PayloadJson { get; set; } = "{}";

    public JobStateType State { get; set; }

    public JobProgressSnapshot Progress { get; set; } = new();

    public string Error { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}
```

`JobLogEntry.cs`:

```csharp
using SignalNine.Core.Types;

namespace SignalNine.Core.Data.Jobs;

public class JobLogEntry
{
    public long Sequence { get; set; }

    public Guid JobId { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public JobLogLevelType Level { get; set; }

    public string Message { get; set; } = "";
}
```

- [ ] **Step 3: Create interfaces with XML docs**

`IJobHandler.cs`:

```csharp
using SignalNine.Core.Data.Jobs;

namespace SignalNine.Core.Interfaces;

/// <summary>
/// Handles one job type.
/// </summary>
public interface IJobHandler
{
    /// <summary>
    /// Gets the job type handled by this handler.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Executes the job.
    /// </summary>
    /// <param name="context">The job execution context.</param>
    /// <param name="cancellationToken">Token used to stop the job.</param>
    Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken);
}
```

`IJobNotificationPublisher.cs`:

```csharp
using SignalNine.Core.Data.Jobs;

namespace SignalNine.Core.Interfaces;

/// <summary>
/// Publishes job status and log updates to external subscribers.
/// </summary>
public interface IJobNotificationPublisher
{
    /// <summary>
    /// Publishes a job status snapshot.
    /// </summary>
    /// <param name="snapshot">The job snapshot to publish.</param>
    /// <param name="cancellationToken">Token used to cancel publishing.</param>
    Task PublishStatusAsync(JobSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a job log entry.
    /// </summary>
    /// <param name="entry">The log entry to publish.</param>
    /// <param name="cancellationToken">Token used to cancel publishing.</param>
    Task PublishLogAsync(JobLogEntry entry, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Create execution context**

`JobExecutionContext.cs`:

```csharp
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;

namespace SignalNine.Core.Data.Jobs;

public class JobExecutionContext
{
    private readonly IJobManager _jobManager;

    public Guid JobId { get; }

    public string PayloadJson { get; }

    public JobExecutionContext(Guid jobId, string payloadJson, IJobManager jobManager)
    {
        ArgumentNullException.ThrowIfNull(jobManager);

        JobId = jobId;
        PayloadJson = payloadJson;
        _jobManager = jobManager;
    }

    public Task ReportProgressAsync(int percent, string message, CancellationToken cancellationToken = default)
        => _jobManager.ReportProgressAsync(JobId, percent, message, cancellationToken);

    public Task WriteLogAsync(JobLogLevelType level, string message, CancellationToken cancellationToken = default)
        => _jobManager.WriteLogAsync(JobId, level, message, cancellationToken);
}
```

- [ ] **Step 5: Create job manager interface**

`IJobManager.cs`:

```csharp
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Types;

namespace SignalNine.Core.Interfaces;

/// <summary>
/// Manages in-memory job queueing, state, progress, logs, and cancellation.
/// </summary>
public interface IJobManager
{
    /// <summary>Queues a job for execution.</summary>
    Task<JobSnapshot> EnqueueAsync(EnqueueJobCommand command, CancellationToken cancellationToken = default);

    /// <summary>Gets all known jobs.</summary>
    IReadOnlyList<JobSnapshot> List();

    /// <summary>Gets a job by id.</summary>
    JobSnapshot? GetById(Guid jobId);

    /// <summary>Gets retained log entries for a job.</summary>
    IReadOnlyList<JobLogEntry> GetLogs(Guid jobId);

    /// <summary>Requests cancellation for a queued or running job.</summary>
    Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Waits for the next queued job id.</summary>
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);

    /// <summary>Marks a queued job as running and returns an execution context.</summary>
    Task<JobExecutionContext?> StartAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Completes a running job.</summary>
    Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Fails a running job.</summary>
    Task FailAsync(Guid jobId, Exception exception, CancellationToken cancellationToken = default);

    /// <summary>Marks a running job as canceled.</summary>
    Task MarkCanceledAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Reports job progress.</summary>
    Task ReportProgressAsync(Guid jobId, int percent, string message, CancellationToken cancellationToken = default);

    /// <summary>Writes a retained job log entry.</summary>
    Task WriteLogAsync(Guid jobId, JobLogLevelType level, string message, CancellationToken cancellationToken = default);

    /// <summary>Gets the cancellation token for a running job.</summary>
    CancellationToken GetCancellationToken(Guid jobId);
}
```

- [ ] **Step 6: Run build**

Run:

```bash
dotnet build SignalNine.slnx
```

Expected: build succeeds.

- [ ] **Step 7: Commit**

```bash
git add src/SignalNine.Core/Types src/SignalNine.Core/Data/Jobs src/SignalNine.Core/Interfaces
git commit -m "feat(jobs): add job contracts"
```

---

### Task 3: In-Memory Job Manager

**Files:**
- Create: `src/SignalNine.Core/Services/InMemoryJobManager.cs`
- Create: `src/SignalNine.Core/Services/NoOpJobNotificationPublisher.cs`
- Test: `tests/SignalNine.Tests/Core/Services/InMemoryJobManagerTests.cs`

- [ ] **Step 1: Write failing manager tests**

Create `InMemoryJobManagerTests.cs` with tests for enqueue, progress, logs, and cancel:

```csharp
using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Core.Types;

namespace SignalNine.Tests.Core.Services;

public class InMemoryJobManagerTests
{
    [Fact]
    public async Task EnqueueAsync_ValidCommand_StoresQueuedJob()
    {
        var manager = CreateManager();

        var job = await manager.EnqueueAsync(new EnqueueJobCommand { Type = "test.job", PayloadJson = "{}" });

        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Equal(JobStateType.Queued, job.State);
        Assert.Equal("test.job", job.Type);
        Assert.Single(manager.List());
    }

    [Fact]
    public async Task ReportProgressAsync_RunningJob_UpdatesProgress()
    {
        var manager = CreateManager();
        var job = await manager.EnqueueAsync(new EnqueueJobCommand { Type = "test.job", PayloadJson = "{}" });
        await manager.StartAsync(job.Id);

        await manager.ReportProgressAsync(job.Id, 42, "Halfway");

        var snapshot = manager.GetById(job.Id);
        Assert.NotNull(snapshot);
        Assert.Equal(42, snapshot.Progress.Percent);
        Assert.Equal("Halfway", snapshot.Progress.Message);
    }

    [Fact]
    public async Task WriteLogAsync_RetainsConfiguredLogEntries()
    {
        var manager = CreateManager(maxLogEntriesPerJob: 2);
        var job = await manager.EnqueueAsync(new EnqueueJobCommand { Type = "test.job", PayloadJson = "{}" });
        await manager.StartAsync(job.Id);

        await manager.WriteLogAsync(job.Id, JobLogLevelType.Information, "first");
        await manager.WriteLogAsync(job.Id, JobLogLevelType.Warning, "second");
        await manager.WriteLogAsync(job.Id, JobLogLevelType.Error, "third");

        var logs = manager.GetLogs(job.Id);
        Assert.Equal(2, logs.Count);
        Assert.Equal("second", logs[0].Message);
        Assert.Equal("third", logs[1].Message);
    }

    [Fact]
    public async Task CancelAsync_RunningJob_CancelsTokenAndMarksJobCanceled()
    {
        var manager = CreateManager();
        var job = await manager.EnqueueAsync(new EnqueueJobCommand { Type = "test.job", PayloadJson = "{}" });
        await manager.StartAsync(job.Id);

        var canceled = await manager.CancelAsync(job.Id);

        Assert.True(canceled);
        Assert.True(manager.GetCancellationToken(job.Id).IsCancellationRequested);
        Assert.Equal(JobStateType.Canceled, manager.GetById(job.Id)?.State);
    }

    private static IJobManager CreateManager(int maxLogEntriesPerJob = 500)
        => new InMemoryJobManager(
            new SignalNineConfig
            {
                JobSystem =
                {
                    MaxLogEntriesPerJob = maxLogEntriesPerJob
                }
            },
            new NullJobNotificationPublisher()
        );

    private sealed class NullJobNotificationPublisher : IJobNotificationPublisher
    {
        public Task PublishStatusAsync(JobSnapshot snapshot, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task PublishLogAsync(JobLogEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
dotnet test tests/SignalNine.Tests/SignalNine.Tests.csproj --filter InMemoryJobManagerTests
```

Expected: compile fails because `InMemoryJobManager` does not exist.

- [ ] **Step 3: Implement default no-op publisher**

Create `src/SignalNine.Core/Services/NoOpJobNotificationPublisher.cs`:

```csharp
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;

namespace SignalNine.Core.Services;

public class NoOpJobNotificationPublisher : IJobNotificationPublisher
{
    public Task PublishStatusAsync(JobSnapshot snapshot, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PublishLogAsync(JobLogEntry entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
```

- [ ] **Step 4: Implement `InMemoryJobManager`**

Create `src/SignalNine.Core/Services/InMemoryJobManager.cs` with:

```csharp
using System.Collections.Concurrent;
using System.Threading.Channels;

using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;

namespace SignalNine.Core.Services;

public class InMemoryJobManager : IJobManager
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
    private readonly ConcurrentDictionary<Guid, JobSnapshot> _jobs = new();
    private readonly ConcurrentDictionary<Guid, List<JobLogEntry>> _logs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationTokens = new();
    private readonly IJobNotificationPublisher _notificationPublisher;
    private readonly int _maxLogEntriesPerJob;
    private long _logSequence;

    public InMemoryJobManager(SignalNineConfig config, IJobNotificationPublisher notificationPublisher)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(notificationPublisher);

        _notificationPublisher = notificationPublisher;
        _maxLogEntriesPerJob = Math.Max(1, config.JobSystem.MaxLogEntriesPerJob);
    }

    public async Task<JobSnapshot> EnqueueAsync(EnqueueJobCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Type))
        {
            throw new ArgumentException("Job type cannot be empty.", nameof(command));
        }

        var snapshot = new JobSnapshot
        {
            Id = Guid.NewGuid(),
            Type = command.Type,
            PayloadJson = string.IsNullOrWhiteSpace(command.PayloadJson) ? "{}" : command.PayloadJson,
            State = JobStateType.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
            Progress =
            {
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        _jobs[snapshot.Id] = snapshot;
        await _queue.Writer.WriteAsync(snapshot.Id, cancellationToken).ConfigureAwait(false);
        await _notificationPublisher.PublishStatusAsync(Clone(snapshot), cancellationToken).ConfigureAwait(false);

        return Clone(snapshot);
    }

    public IReadOnlyList<JobSnapshot> List()
        => _jobs.Values.Select(Clone).OrderByDescending(job => job.CreatedAt).ToList();

    public JobSnapshot? GetById(Guid jobId)
        => _jobs.TryGetValue(jobId, out var snapshot) ? Clone(snapshot) : null;

    public IReadOnlyList<JobLogEntry> GetLogs(Guid jobId)
        => _logs.TryGetValue(jobId, out var entries) ? entries.Select(Clone).ToList() : [];

    public async Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var snapshot) || IsTerminal(snapshot.State))
        {
            return false;
        }

        if (_cancellationTokens.TryGetValue(jobId, out var source))
        {
            await source.CancelAsync().ConfigureAwait(false);
        }

        snapshot.State = JobStateType.Canceled;
        snapshot.FinishedAt = DateTimeOffset.UtcNow;
        await _notificationPublisher.PublishStatusAsync(Clone(snapshot), cancellationToken).ConfigureAwait(false);

        return true;
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
        => _queue.Reader.ReadAsync(cancellationToken);

    public async Task<JobExecutionContext?> StartAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var snapshot) || snapshot.State != JobStateType.Queued)
        {
            return null;
        }

        snapshot.State = JobStateType.Running;
        snapshot.StartedAt = DateTimeOffset.UtcNow;
        _cancellationTokens[jobId] = new CancellationTokenSource();
        await _notificationPublisher.PublishStatusAsync(Clone(snapshot), cancellationToken).ConfigureAwait(false);

        return new JobExecutionContext(jobId, snapshot.PayloadJson, this);
    }

    public Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default)
        => MoveToTerminalAsync(jobId, JobStateType.Completed, "", cancellationToken);

    public Task FailAsync(Guid jobId, Exception exception, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return MoveToTerminalAsync(jobId, JobStateType.Failed, exception.Message, cancellationToken);
    }

    public Task MarkCanceledAsync(Guid jobId, CancellationToken cancellationToken = default)
        => MoveToTerminalAsync(jobId, JobStateType.Canceled, "", cancellationToken);

    public async Task ReportProgressAsync(Guid jobId, int percent, string message, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var snapshot) || IsTerminal(snapshot.State))
        {
            return;
        }

        snapshot.Progress.Percent = Math.Clamp(percent, 0, 100);
        snapshot.Progress.Message = message;
        snapshot.Progress.UpdatedAt = DateTimeOffset.UtcNow;
        await _notificationPublisher.PublishStatusAsync(Clone(snapshot), cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteLogAsync(Guid jobId, JobLogLevelType level, string message, CancellationToken cancellationToken = default)
    {
        if (!_jobs.ContainsKey(jobId))
        {
            return;
        }

        var entry = new JobLogEntry
        {
            Sequence = Interlocked.Increment(ref _logSequence),
            JobId = jobId,
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Message = message
        };
        var entries = _logs.GetOrAdd(jobId, _ => []);

        lock (entries)
        {
            entries.Add(entry);

            while (entries.Count > _maxLogEntriesPerJob)
            {
                entries.RemoveAt(0);
            }
        }

        await _notificationPublisher.PublishLogAsync(Clone(entry), cancellationToken).ConfigureAwait(false);
    }

    public CancellationToken GetCancellationToken(Guid jobId)
        => _cancellationTokens.TryGetValue(jobId, out var source) ? source.Token : CancellationToken.None;

    private async Task MoveToTerminalAsync(
        Guid jobId,
        JobStateType state,
        string error,
        CancellationToken cancellationToken
    )
    {
        if (!_jobs.TryGetValue(jobId, out var snapshot))
        {
            return;
        }

        snapshot.State = state;
        snapshot.Error = error;
        snapshot.FinishedAt = DateTimeOffset.UtcNow;
        _cancellationTokens.TryRemove(jobId, out var source);
        source?.Dispose();
        await _notificationPublisher.PublishStatusAsync(Clone(snapshot), cancellationToken).ConfigureAwait(false);
    }

    private static bool IsTerminal(JobStateType state)
        => state is JobStateType.Completed or JobStateType.Failed or JobStateType.Canceled;

    private static JobSnapshot Clone(JobSnapshot snapshot)
        => new()
        {
            Id = snapshot.Id,
            Type = snapshot.Type,
            PayloadJson = snapshot.PayloadJson,
            State = snapshot.State,
            Progress = new JobProgressSnapshot
            {
                Percent = snapshot.Progress.Percent,
                Message = snapshot.Progress.Message,
                UpdatedAt = snapshot.Progress.UpdatedAt
            },
            Error = snapshot.Error,
            CreatedAt = snapshot.CreatedAt,
            StartedAt = snapshot.StartedAt,
            FinishedAt = snapshot.FinishedAt
        };

    private static JobLogEntry Clone(JobLogEntry entry)
        => new()
        {
            Sequence = entry.Sequence,
            JobId = entry.JobId,
            Timestamp = entry.Timestamp,
            Level = entry.Level,
            Message = entry.Message
        };
}
```

- [ ] **Step 5: Run manager tests**

Run:

```bash
dotnet test tests/SignalNine.Tests/SignalNine.Tests.csproj --filter InMemoryJobManagerTests
```

Expected: all `InMemoryJobManagerTests` pass.

- [ ] **Step 6: Commit**

```bash
git add src/SignalNine.Core/Services/InMemoryJobManager.cs src/SignalNine.Core/Services/NoOpJobNotificationPublisher.cs tests/SignalNine.Tests/Core/Services/InMemoryJobManagerTests.cs
git commit -m "feat(jobs): add in-memory job manager"
```

---

### Task 4: Hosted Worker and Configurable Concurrency

**Files:**
- Create: `src/SignalNine.Web/Services/JobWorkerService.cs`
- Create: `tests/SignalNine.Tests/Support/Jobs/FakeJobHandler.cs`
- Test: `tests/SignalNine.Tests/Web/JobWorkerServiceTests.cs`

- [ ] **Step 1: Write failing worker tests**

The tests should register two fake jobs, set `MaxConcurrentJobs = 1`, and assert the second job does not start until the first one is released. Use a fake handler with two `TaskCompletionSource` gates.

Create `tests/SignalNine.Tests/Support/Jobs/FakeJobHandler.cs`:

```csharp
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;

namespace SignalNine.Tests.Support.Jobs;

public class FakeJobHandler : IJobHandler
{
    public string Type { get; init; } = "test.fake";

    public int StartedCount { get; private set; }

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        StartedCount++;
        Started.TrySetResult();
        await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        await context.ReportProgressAsync(100, "Done", cancellationToken).ConfigureAwait(false);
    }

    public async Task WaitForStartedCountAsync(int expectedCount, CancellationToken cancellationToken)
    {
        while (StartedCount < expectedCount)
        {
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }
}
```

Create `tests/SignalNine.Tests/Web/JobWorkerServiceTests.cs`:

```csharp
using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Core.Types;
using SignalNine.Tests.Support.Jobs;
using SignalNine.Web.Services;

namespace SignalNine.Tests.Web;

public class JobWorkerServiceTests
{
    [Fact]
    public async Task ExecuteAsync_MaxConcurrentJobsOne_RunsOneJobAtATime()
    {
        var handler = new FakeJobHandler();
        var manager = new InMemoryJobManager(CreateConfig(), new NoOpJobNotificationPublisher());
        var service = new JobWorkerService(CreateConfig(), [handler], manager);
        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.StartAsync(timeoutSource.Token);
        var firstJob = await manager.EnqueueAsync(new EnqueueJobCommand { Type = handler.Type });
        var secondJob = await manager.EnqueueAsync(new EnqueueJobCommand { Type = handler.Type });
        await handler.WaitForStartedCountAsync(1, timeoutSource.Token);

        Assert.Equal(JobStateType.Running, manager.GetById(firstJob.Id)?.State);
        Assert.Equal(JobStateType.Queued, manager.GetById(secondJob.Id)?.State);

        handler.Release.SetResult();
        await handler.WaitForStartedCountAsync(2, timeoutSource.Token);
        await service.StopAsync(timeoutSource.Token);

        Assert.Equal(JobStateType.Completed, manager.GetById(firstJob.Id)?.State);
    }

    private static SignalNineConfig CreateConfig()
        => new()
        {
            JobSystem =
            {
                MaxConcurrentJobs = 1
            }
        };
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
dotnet test tests/SignalNine.Tests/SignalNine.Tests.csproj --filter JobWorkerServiceTests
```

Expected: compile fails because `JobWorkerService` does not exist.

- [ ] **Step 3: Implement hosted worker**

Create `src/SignalNine.Web/Services/JobWorkerService.cs`:

```csharp
using SignalNine.Core.Data.Config;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;

namespace SignalNine.Web.Services;

public class JobWorkerService : BackgroundService
{
    private readonly Dictionary<string, IJobHandler> _handlers;
    private readonly IJobManager _jobManager;
    private readonly SemaphoreSlim _concurrency;

    public JobWorkerService(
        SignalNineConfig config,
        IEnumerable<IJobHandler> handlers,
        IJobManager jobManager
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(jobManager);

        _handlers = handlers.ToDictionary(handler => handler.Type, StringComparer.OrdinalIgnoreCase);
        _jobManager = jobManager;
        _concurrency = new SemaphoreSlim(Math.Max(1, config.JobSystem.MaxConcurrentJobs));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var jobId = await _jobManager.DequeueAsync(stoppingToken).ConfigureAwait(false);
            await _concurrency.WaitAsync(stoppingToken).ConfigureAwait(false);

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await ExecuteJobAsync(jobId, stoppingToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        _concurrency.Release();
                    }
                },
                CancellationToken.None
            );
        }
    }

    private async Task ExecuteJobAsync(Guid jobId, CancellationToken stoppingToken)
    {
        var context = await _jobManager.StartAsync(jobId, stoppingToken).ConfigureAwait(false);

        if (context is null)
        {
            return;
        }

        if (!_handlers.TryGetValue(_jobManager.GetById(jobId)?.Type ?? "", out var handler))
        {
            await _jobManager.WriteLogAsync(jobId, JobLogLevelType.Error, "No handler registered for job type.", stoppingToken)
                             .ConfigureAwait(false);
            await _jobManager.FailAsync(jobId, new InvalidOperationException("No handler registered for job type."), stoppingToken)
                             .ConfigureAwait(false);
            return;
        }

        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            _jobManager.GetCancellationToken(jobId)
        );

        try
        {
            await handler.ExecuteAsync(context, linkedSource.Token).ConfigureAwait(false);
            await _jobManager.CompleteAsync(jobId, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await _jobManager.MarkCanceledAsync(jobId, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _jobManager.FailAsync(jobId, ex, stoppingToken).ConfigureAwait(false);
        }
    }
}
```

- [ ] **Step 4: Run worker tests**

Run:

```bash
dotnet test tests/SignalNine.Tests/SignalNine.Tests.csproj --filter JobWorkerServiceTests
```

Expected: worker tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/SignalNine.Web/Services/JobWorkerService.cs tests/SignalNine.Tests/Support/Jobs tests/SignalNine.Tests/Web/JobWorkerServiceTests.cs
git commit -m "feat(jobs): add hosted job worker"
```

---

### Task 5: HTTP Job Endpoints

**Files:**
- Create: `src/SignalNine.Web/Data/Jobs/EnqueueJobRequest.cs`
- Create: `src/SignalNine.Web/Data/Jobs/JobResponse.cs`
- Create: `src/SignalNine.Web/Data/Jobs/JobLogResponse.cs`
- Create: `src/SignalNine.Web/Endpoints/JobEndpoints.cs`
- Modify: `src/SignalNine.Web/Program.cs`
- Test: `tests/SignalNine.Tests/Web/JobEndpointTests.cs`

- [ ] **Step 1: Write failing endpoint tests**

Create `tests/SignalNine.Tests/Support/Web/JwtClientFactory.cs`:

```csharp
using System.Net.Http.Headers;

using SignalNine.Core.Data.Authentication;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Services;

namespace SignalNine.Tests.Support.Web;

public static class JwtClientFactory
{
    public static HttpClient CreateAuthorizedClient(HttpClient client)
    {
        var token = new JwtTokenService(new SignalNineConfig()).CreateToken(
            new JwtTokenUser
            {
                UserId = Guid.NewGuid(),
                Username = "tests",
                Email = "tests@signalnine.local",
                Role = "Admin"
            }
        );

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        return client;
    }
}
```

Create `tests/SignalNine.Tests/Web/JobEndpointTests.cs` covering:

```csharp
[Fact]
public async Task Post_Jobs_QueuesJobAndReturnsAccepted()
```

```csharp
[Fact]
public async Task Get_Job_ReturnsStatus()
```

```csharp
[Fact]
public async Task Post_CancelJob_CancelsQueuedJob()
```

```csharp
[Fact]
public async Task Get_JobLogs_ReturnsRetainedLogs()
```

Use `WebApplicationFactory<Program>`, create an authorized client with a JWT generated by `JwtTokenService`, and POST:

```json
{
  "type": "test.fake",
  "payload": { "name": "first" }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
dotnet test tests/SignalNine.Tests/SignalNine.Tests.csproj --filter JobEndpointTests
```

Expected: endpoints return 404.

- [ ] **Step 3: Create API DTOs**

`EnqueueJobRequest.cs`:

```csharp
using System.Text.Json;

namespace SignalNine.Web.Data.Jobs;

/// <summary>Payload for queueing a job.</summary>
public sealed record EnqueueJobRequest
{
    public required string Type { get; init; }

    public JsonElement Payload { get; init; }
}
```

`JobResponse.cs`:

```csharp
using SignalNine.Core.Types;

namespace SignalNine.Web.Data.Jobs;

/// <summary>Represents the current state of a job.</summary>
public sealed record JobResponse(
    Guid Id,
    string Type,
    JobStateType State,
    int ProgressPercent,
    string ProgressMessage,
    string Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);
```

`JobLogResponse.cs`:

```csharp
using SignalNine.Core.Types;

namespace SignalNine.Web.Data.Jobs;

/// <summary>Represents one retained log entry for a job.</summary>
public sealed record JobLogResponse(
    long Sequence,
    Guid JobId,
    DateTimeOffset Timestamp,
    JobLogLevelType Level,
    string Message);
```

- [ ] **Step 4: Create endpoints**

`JobEndpoints.cs`:

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Web.Data.Jobs;

namespace SignalNine.Web.Endpoints;

public static class JobEndpoints
{
    public static WebApplication MapJobEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/jobs").RequireAuthorization();

        group.MapGet("/", (IJobManager manager) => manager.List().Select(ToResponse).ToList());

        group.MapGet(
            "/{jobId:guid}",
            Results<Ok<JobResponse>, NotFound> (Guid jobId, IJobManager manager)
                => manager.GetById(jobId) is { } job
                       ? TypedResults.Ok(ToResponse(job))
                       : TypedResults.NotFound()
        );

        group.MapGet(
            "/{jobId:guid}/logs",
            Results<Ok<IReadOnlyList<JobLogResponse>>, NotFound> (Guid jobId, IJobManager manager)
                => manager.GetById(jobId) is null
                       ? TypedResults.NotFound()
                       : TypedResults.Ok<IReadOnlyList<JobLogResponse>>(manager.GetLogs(jobId).Select(ToLogResponse).ToList())
        );

        group.MapPost(
            "/",
            async Task<Results<Accepted<JobResponse>, BadRequest<string>>> (
                EnqueueJobRequest request,
                IJobManager manager,
                CancellationToken cancellationToken
            ) =>
            {
                if (string.IsNullOrWhiteSpace(request.Type))
                {
                    return TypedResults.BadRequest("Job type cannot be empty.");
                }

                var job = await manager.EnqueueAsync(
                    new EnqueueJobCommand
                    {
                        Type = request.Type,
                        PayloadJson = request.Payload.ValueKind == System.Text.Json.JsonValueKind.Undefined
                                          ? "{}"
                                          : request.Payload.GetRawText()
                    },
                    cancellationToken
                );

                return TypedResults.Accepted($"/api/jobs/{job.Id}", ToResponse(job));
            }
        );

        group.MapPost(
            "/{jobId:guid}/cancel",
            async Task<Results<Accepted, NotFound>> (
                Guid jobId,
                IJobManager manager,
                CancellationToken cancellationToken
            ) =>
            {
                var canceled = await manager.CancelAsync(jobId, cancellationToken);

                return canceled ? TypedResults.Accepted() : TypedResults.NotFound();
            }
        );

        return app;
    }

    private static JobResponse ToResponse(JobSnapshot job)
        => new(
            job.Id,
            job.Type,
            job.State,
            job.Progress.Percent,
            job.Progress.Message,
            job.Error,
            job.CreatedAt,
            job.StartedAt,
            job.FinishedAt
        );

    private static JobLogResponse ToLogResponse(JobLogEntry entry)
        => new(entry.Sequence, entry.JobId, entry.Timestamp, entry.Level, entry.Message);
}
```

- [ ] **Step 5: Wire endpoints in `Program.cs`**

Add:

```csharp
using SignalNine.Web.Endpoints;
```

Before `builder.Build()`:

```csharp
builder.Services.AddSingleton<IJobNotificationPublisher, NoOpJobNotificationPublisher>();
builder.Services.AddSingleton<IJobManager, InMemoryJobManager>();
builder.Services.AddHostedService<JobWorkerService>();
```

After health checks:

```csharp
app.MapJobEndpoints();
```

- [ ] **Step 6: Run endpoint tests**

Run:

```bash
dotnet test tests/SignalNine.Tests/SignalNine.Tests.csproj --filter JobEndpointTests
```

Expected: endpoint tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/SignalNine.Web/Data/Jobs src/SignalNine.Web/Endpoints/JobEndpoints.cs src/SignalNine.Web/Program.cs tests/SignalNine.Tests/Web/JobEndpointTests.cs
git commit -m "feat(jobs): add job status endpoints"
```

---

### Task 6: SignalR Status and Log Streams

**Files:**
- Create: `src/SignalNine.Web/Hubs/JobStatusHub.cs`
- Create: `src/SignalNine.Web/Hubs/JobLogHub.cs`
- Create: `src/SignalNine.Web/Services/SignalRJobNotificationPublisher.cs`
- Modify: `src/SignalNine.Web/Program.cs`
- Modify: `tests/SignalNine.Tests/SignalNine.Tests.csproj`
- Test: `tests/SignalNine.Tests/Web/JobSignalRTests.cs`

- [ ] **Step 1: Add SignalR client test package**

Run:

```bash
dotnet add tests/SignalNine.Tests/SignalNine.Tests.csproj package Microsoft.AspNetCore.SignalR.Client --version 10.0.8
```

- [ ] **Step 2: Write failing SignalR tests**

Create tests that connect to:

```text
/hubs/jobs/status
/hubs/jobs/logs
```

Then enqueue a job and assert:

```csharp
Assert.Equal(jobId, receivedStatus.Id);
Assert.Equal(jobId, receivedLog.JobId);
```

Use hub method names:

```csharp
connection.On<JobResponse>("JobStatusChanged", response => ...)
connection.On<JobLogResponse>("JobLogReceived", response => ...)
```

- [ ] **Step 3: Run test to verify it fails**

Run:

```bash
dotnet test tests/SignalNine.Tests/SignalNine.Tests.csproj --filter JobSignalRTests
```

Expected: hub connections fail because hubs are not mapped.

- [ ] **Step 4: Create hubs**

`JobStatusHub.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;

namespace SignalNine.Web.Hubs;

public class JobStatusHub : Hub
{
}
```

`JobLogHub.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;

namespace SignalNine.Web.Hubs;

public class JobLogHub : Hub
{
}
```

- [ ] **Step 5: Create publisher**

`SignalRJobNotificationPublisher.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Web.Data.Jobs;
using SignalNine.Web.Hubs;

namespace SignalNine.Web.Services;

public class SignalRJobNotificationPublisher : IJobNotificationPublisher
{
    private readonly IHubContext<JobStatusHub> _statusHub;
    private readonly IHubContext<JobLogHub> _logHub;

    public SignalRJobNotificationPublisher(IHubContext<JobStatusHub> statusHub, IHubContext<JobLogHub> logHub)
    {
        ArgumentNullException.ThrowIfNull(statusHub);
        ArgumentNullException.ThrowIfNull(logHub);

        _statusHub = statusHub;
        _logHub = logHub;
    }

    public Task PublishStatusAsync(JobSnapshot snapshot, CancellationToken cancellationToken = default)
        => _statusHub.Clients.All.SendAsync("JobStatusChanged", ToResponse(snapshot), cancellationToken);

    public Task PublishLogAsync(JobLogEntry entry, CancellationToken cancellationToken = default)
        => _logHub.Clients.All.SendAsync("JobLogReceived", ToLogResponse(entry), cancellationToken);

    private static JobResponse ToResponse(JobSnapshot job)
        => new(
            job.Id,
            job.Type,
            job.State,
            job.Progress.Percent,
            job.Progress.Message,
            job.Error,
            job.CreatedAt,
            job.StartedAt,
            job.FinishedAt
        );

    private static JobLogResponse ToLogResponse(JobLogEntry entry)
        => new(entry.Sequence, entry.JobId, entry.Timestamp, entry.Level, entry.Message);
}
```

- [ ] **Step 6: Wire SignalR**

In `Program.cs`, add:

```csharp
using SignalNine.Web.Hubs;
```

Replace the no-op publisher registration with:

```csharp
builder.Services.AddSignalR();
builder.Services.AddSingleton<IJobNotificationPublisher, SignalRJobNotificationPublisher>();
```

Map hubs:

```csharp
app.MapHub<JobStatusHub>("/hubs/jobs/status").RequireAuthorization();
app.MapHub<JobLogHub>("/hubs/jobs/logs").RequireAuthorization();
```

- [ ] **Step 7: Run SignalR tests**

Run:

```bash
dotnet test tests/SignalNine.Tests/SignalNine.Tests.csproj --filter JobSignalRTests
```

Expected: SignalR tests pass.

- [ ] **Step 8: Commit**

```bash
git add src/SignalNine.Web/Hubs src/SignalNine.Web/Services/SignalRJobNotificationPublisher.cs src/SignalNine.Web/Program.cs tests/SignalNine.Tests/SignalNine.Tests.csproj tests/SignalNine.Tests/Web/JobSignalRTests.cs
git commit -m "feat(jobs): stream job updates over signalr"
```

---

### Task 7: Program Wiring and Full Verification

**Files:**
- Modify: `src/SignalNine.Web/Program.cs`
- Modify: `tests/SignalNine.Tests/Web/HealthEndpointTests.cs` if startup tests need shared helper cleanup.

- [ ] **Step 1: Verify runtime service registrations**

Ensure `Program.cs` contains:

```csharp
builder.Services.AddSignalR();
builder.Services.AddSingleton<IJobNotificationPublisher, SignalRJobNotificationPublisher>();
builder.Services.AddSingleton<IJobManager, InMemoryJobManager>();
builder.Services.AddHostedService<JobWorkerService>();
```

Keep health endpoints before protected job endpoints:

```csharp
app.MapHealthChecks("/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapJobEndpoints();
app.MapHub<JobStatusHub>("/hubs/jobs/status").RequireAuthorization();
app.MapHub<JobLogHub>("/hubs/jobs/logs").RequireAuthorization();
```

- [ ] **Step 2: Run targeted tests**

Run:

```bash
dotnet test tests/SignalNine.Tests/SignalNine.Tests.csproj --filter "ConfigServiceTests|InMemoryJobManagerTests|JobWorkerServiceTests|JobEndpointTests|JobSignalRTests"
```

Expected: targeted job system tests pass.

- [ ] **Step 3: Run full verification**

Run:

```bash
dotnet build SignalNine.slnx
dotnet test SignalNine.slnx
git diff --check
rg -n "string\\.Empty|TO""DO|ILogger<|class [A-Za-z0-9_]+\\(.*\\)|namespace [^{;]+\\{" src/SignalNine.Core/Data/Config src/SignalNine.Core/Data/Jobs src/SignalNine.Core/Interfaces src/SignalNine.Core/Services src/SignalNine.Web tests/SignalNine.Tests
codegraph index
```

Expected:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
Passed!
git diff --check produces no output.
The rg convention check reports no matches in new job files.
CodeGraph indexes successfully.
```

- [ ] **Step 4: Commit**

```bash
git add src tests docs/superpowers/plans/2026-05-27-job-system.md
git commit -m "feat(jobs): add in-memory job system"
```
