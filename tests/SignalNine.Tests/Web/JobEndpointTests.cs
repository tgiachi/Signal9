using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Interfaces;
using SignalNine.Tests.Support.Jobs;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Data.Jobs;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class JobEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;

    public JobEndpointTests()
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
    public async Task Post_Jobs_QueuesJobAndReturnsAccepted()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());

        var response = await client.PostAsJsonAsync(
            "/api/jobs",
            new
            {
                Type = "test.fake",
                Payload = new
                {
                    Name = "first"
                }
            }
        );

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var job = await response.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(job);
        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Equal("test.fake", job.Type);
    }

    [Fact]
    public async Task Get_Job_ReturnsStatus()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var createdJob = await EnqueueAsync(client);

        var response = await client.GetAsync($"/api/jobs/{createdJob.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var job = await response.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(job);
        Assert.Equal(createdJob.Id, job.Id);
    }

    [Fact]
    public async Task Post_CancelJob_CancelsJob()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var createdJob = await EnqueueAsync(client);

        var response = await client.PostAsync($"/api/jobs/{createdJob.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Get_JobLogs_ReturnsRetainedLogs()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var createdJob = await EnqueueAsync(client);

        var response = await client.GetAsync($"/api/jobs/{createdJob.Id}/logs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var logs = await response.Content.ReadFromJsonAsync<IReadOnlyList<JobLogResponse>>();
        Assert.NotNull(logs);
    }

    private static async Task<JobResponse> EnqueueAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/jobs",
            new
            {
                Type = "test.fake",
                Payload = new
                {
                    Name = "first"
                }
            }
        );

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
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
