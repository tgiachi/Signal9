using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Interfaces;
using SignalNine.Tests.Support.Jobs;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Data.Jobs;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class JobSignalRTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";
    private const string TestJobType = "test.fake";

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;

    public JobSignalRTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
        _previousRootDirectory = Environment.GetEnvironmentVariable(RootDirectoryVariableName);
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _rootDirectory);
        var handler = new FakeJobHandler();

        _factory = new WebApplicationFactory<Program>()
                   .WithWebHostBuilder(
                       builder =>
                       {
                           builder.ConfigureServices(
                               services =>
                               {
                                   services.AddSingleton<IJobHandler>(handler);
                               }
                           );
                       }
                   );
    }

    [Fact]
    public async Task StatusHub_JobQueued_PublishesStatus()
    {
        var receivedStatus = new TaskCompletionSource<JobResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = CreateConnection("/hubs/jobs/status");
        connection.On<JobResponse>(
            "JobStatusChanged",
            response =>
            {
                if (response.Type == TestJobType)
                {
                    receivedStatus.TrySetResult(response);
                }
            }
        );
        await connection.StartAsync();
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());

        var createdJob = await EnqueueAsync(client);
        var status = await receivedStatus.Task.WaitAsync(TestTimeout);

        Assert.Equal(createdJob.Id, status.Id);
    }

    [Fact]
    public async Task LogHub_JobWritesLog_PublishesLog()
    {
        var receivedLog = new TaskCompletionSource<JobLogResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = CreateConnection("/hubs/jobs/logs");
        connection.On<JobLogResponse>(
            "JobLogReceived",
            response =>
            {
                if (response.Message == "Started")
                {
                    receivedLog.TrySetResult(response);
                }
            }
        );
        await connection.StartAsync();
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());

        var createdJob = await EnqueueAsync(client);
        var log = await receivedLog.Task.WaitAsync(TestTimeout);

        Assert.Equal(createdJob.Id, log.JobId);
        Assert.Equal("Started", log.Message);
    }

    private HubConnection CreateConnection(string path)
        => new HubConnectionBuilder()
           .WithUrl(
               new Uri(_factory.Server.BaseAddress, path),
               options =>
               {
                   options.AccessTokenProvider = () => Task.FromResult<string?>(JwtClientFactory.CreateAccessToken());
                   options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
               }
           )
           .Build();

    private static async Task<JobResponse> EnqueueAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/jobs",
            new
            {
                Type = TestJobType,
                Payload = new
                {
                    Name = "first"
                }
            }
        );
        var job = await response.Content.ReadFromJsonAsync<JobResponse>();

        Assert.NotNull(job);

        return job;
    }

    public void Dispose()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _previousRootDirectory);

        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, true);
        }

        GC.SuppressFinalize(this);
    }
}
