using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using SignalNine.Core.Data.Jellyfin;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;

namespace SignalNine.Tests.Core.Services;

public class JellyfinServiceListItemsTests
{
    private static (JellyfinService Service, List<HttpRequestMessage> Captured) Build(
        Queue<HttpResponseMessage> responses
    )
    {
        var captured = new List<HttpRequestMessage>();
        var handler = new StubMessageHandler(req =>
        {
            captured.Add(req);
            return responses.Dequeue();
        });
        var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var connection = new StubConnectionService(("https://jf.local", "tok"));
        var cache = new MemoryCache(new MemoryCacheOptions());
        return (new JellyfinService(factory, connection, cache), captured);
    }

    [Fact]
    public async Task ListItems_SinglePage_ReturnsAllItems()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"Items":[{"Id":"a","Name":"A","Type":"Movie","RunTimeTicks":1000000},{"Id":"b","Name":"B","Type":"Movie"}],"TotalRecordCount":2}""",
                Encoding.UTF8, "application/json"
            )
        });

        var (service, captured) = Build(responses);
        var items = await service.ListItemsAsync("lib-1");

        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].Id);
        Assert.Single(captured);
        Assert.Contains("ParentId=lib-1", captured[0].RequestUri!.Query);
        Assert.Contains("Recursive=true", captured[0].RequestUri.Query);
        Assert.Contains("StartIndex=0", captured[0].RequestUri.Query);
        Assert.Contains("Limit=500", captured[0].RequestUri.Query);
    }

    [Fact]
    public async Task ListItems_PaginatesUntilTotalReached()
    {
        string Make500(int start) =>
            "{\"Items\":[" +
            string.Join(",", Enumerable.Range(start, 500).Select(i => $"{{\"Id\":\"i{i}\",\"Name\":\"N{i}\",\"Type\":\"Movie\"}}")) +
            "],\"TotalRecordCount\":750}";

        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Make500(0), Encoding.UTF8, "application/json")
        });
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"Items\":[" +
                string.Join(",", Enumerable.Range(500, 250).Select(i => $"{{\"Id\":\"i{i}\",\"Name\":\"N{i}\",\"Type\":\"Movie\"}}")) +
                "],\"TotalRecordCount\":750}",
                Encoding.UTF8, "application/json"
            )
        });

        var (service, captured) = Build(responses);
        var items = await service.ListItemsAsync("lib-1");

        Assert.Equal(750, items.Count);
        Assert.Equal(2, captured.Count);
        Assert.Contains("StartIndex=0", captured[0].RequestUri!.Query);
        Assert.Contains("StartIndex=500", captured[1].RequestUri!.Query);
    }

    [Fact]
    public async Task ListItems_EmptyLibrary_ReturnsEmpty()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"Items":[],"TotalRecordCount":0}""",
                Encoding.UTF8, "application/json"
            )
        });

        var (service, _) = Build(responses);
        var items = await service.ListItemsAsync("lib-empty");

        Assert.Empty(items);
    }

    [Fact]
    public async Task ListItems_401_ThrowsAuthException()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var (service, _) = Build(responses);
        await Assert.ThrowsAsync<JellyfinAuthException>(() => service.ListItemsAsync("lib-1"));
    }

    [Fact]
    public async Task ListItems_MapsTvShowFields()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"Items":[{"Id":"ep-1","Name":"Pilot","Type":"Episode","RunTimeTicks":27000000000,"SeriesName":"Breaking Bad","ParentIndexNumber":1,"IndexNumber":1}],"TotalRecordCount":1}""",
                Encoding.UTF8, "application/json"
            )
        });

        var (service, _) = Build(responses);
        var items = await service.ListItemsAsync("lib-1");

        Assert.Single(items);
        var ep = items[0];
        Assert.Equal("Breaking Bad", ep.SeriesName);
        Assert.Equal(1, ep.ParentIndexNumber);
        Assert.Equal(1, ep.IndexNumber);
    }

    private sealed class StubMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            return Task.FromResult(_respond(request));
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }
        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }

    private sealed class StubConnectionService : IJellyfinConnectionService
    {
        private readonly (string BaseUrl, string ApiKey)? _creds;
        public StubConnectionService((string BaseUrl, string ApiKey)? creds)
        {
            _creds = creds;
        }
        public Task<JellyfinConnectionStatus> GetStatusAsync(CancellationToken ct = default)
        {
            return Task.FromResult(new JellyfinConnectionStatus(_creds is not null, _creds?.BaseUrl, null));
        }
        public Task SetAsync(string baseUrl, string apiKey, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
        public Task<(string BaseUrl, string ApiKey)?> GetCredentialsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_creds);
        }
        public Task MarkVerifiedAsync(CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}
