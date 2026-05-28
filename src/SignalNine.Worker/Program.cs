// src/SignalNine.Worker/Program.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;
using SignalNine.Core.Services.Ffmpeg;
using SignalNine.Core.Services.Redis;
using SignalNine.Jobs.Services;
using SignalNine.Worker.Services;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables(prefix: "SIGNAL9_");

var redisUrl = builder.Configuration["REDIS_URL"]
    ?? builder.Configuration["Redis:Url"]
    ?? throw new InvalidOperationException("SIGNAL9_REDIS_URL is required");

var workerName = builder.Configuration["WORKER_NAME"];
var workerStateFile = builder.Configuration["WORKER_STATE_FILE"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".signal9-worker-id");
var explicitIdRaw = builder.Configuration["WORKER_ID"];
Guid? explicitId = Guid.TryParse(explicitIdRaw, out var parsed) ? parsed : null;
var identity = WorkerIdentity.LoadOrCreate(workerStateFile, workerName, explicitId);

var maxConcurrent = int.TryParse(builder.Configuration["MAX_CONCURRENT_JOBS"], out var mc) ? mc : 2;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();
builder.Logging.AddSerilog(Log.Logger, dispose: true);

// Minimal SignalNineConfig — worker only needs JobSystem + FfmpegPool sections
var signalNineConfig = new SignalNineConfig
{
    JobSystem = new JobSystemConfig { MaxConcurrentJobs = maxConcurrent },
    FfmpegPool = new FfmpegPoolConfig(),
    Redis = new RedisConfig { Url = redisUrl }
};
builder.Services.AddSingleton(signalNineConfig);
builder.Services.AddSingleton(signalNineConfig.Redis);
builder.Services.AddSingleton(identity);

// Redis connection
var redisOptions = ConfigurationOptions.Parse(redisUrl);
redisOptions.AbortOnConnectFail = false;
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
builder.Services.AddSingleton(sp => new RedisStreamKeys(sp.GetRequiredService<RedisConfig>()));
builder.Services.AddSingleton<IJobQueue, RedisJobQueue>();
builder.Services.AddSingleton<IJobBus, RedisJobBus>();

// FFmpeg pool (local to this worker)
builder.Services.AddSingleton<IProcessLauncher, DefaultProcessLauncher>();
builder.Services.AddSingleton<IFfmpegPool>(sp => new FfmpegPool(
    sp.GetRequiredService<IProcessLauncher>(),
    signalNineConfig.FfmpegPool));

// Worker-eligible handlers only (library.scan stays on web).
// NOTE: MediaPipelineJobHandler constructs with IServiceScopeFactory only — it compiles.
// Pipeline tasks (ProbeMediaTask, ExtractPreviewsTask, TagMediaTask) need IDataAccess<>
// which requires a DB connection not available in this process.
// Phase 4 will refactor handlers to accept IJobBus directly and remove the DB dependency here.
// Until then: the binary compiles and the handler is registered; a job pull will throw at
// runtime when the handler tries to resolve IDataAccess<ChannelMediaEntity> from the scope.
builder.Services.AddSingleton<IJobHandler, MediaPipelineJobHandler>();

builder.Services.AddHostedService<WorkerJobLoop>();

var host = builder.Build();
Log.Information("SignalNine.Worker starting: workerId={Id} name={Name} maxConcurrent={Max}",
    identity.Id, identity.Name, maxConcurrent);

await host.RunAsync();
Log.Information("SignalNine.Worker stopped.");
