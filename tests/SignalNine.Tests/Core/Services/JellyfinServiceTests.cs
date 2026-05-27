using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using SignalNine.Core.Data.Jellyfin;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services;

namespace SignalNine.Tests.Core.Services;

public class JellyfinServiceTests
{
    private static (JellyfinService Service, StubMessageHandler Handler) BuildService(
        (string BaseUrl, string ApiKey)? credentials,
        Func<HttpRequestMessage, HttpResponseMessage> respond
    )
    {
        var handler = new StubMessageHandler(respond);
        var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var connection = new StubConnectionService(credentials);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new JellyfinService(factory, connection, cache);
        return (service, handler);
    }

    [Fact]
    public async Task GetServerInfo_NoConnection_Throws()
    {
        var (service, _) = BuildService(credentials: null, _ => new HttpResponseMessage(HttpStatusCode.OK));
        await Assert.ThrowsAsync<JellyfinNotConfiguredException>(() => service.GetServerInfoAsync());
    }

    [Fact]
    public async Task GetServerInfo_Happy_ReturnsMappedDto()
    {
        var (service, _) = BuildService(
            ("https://jf.local", "tok"),
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"ServerName":"Home Jellyfin","Version":"10.9.0","Id":"abc123"}""",
                    Encoding.UTF8,
                    "application/json"
                )
            }
        );

        var info = await service.GetServerInfoAsync();
        Assert.Equal("Home Jellyfin", info.ServerName);
        Assert.Equal("10.9.0", info.Version);
        Assert.Equal("abc123", info.Id);
    }

    [Fact]
    public async Task GetServerInfo_401_ThrowsAuthException()
    {
        var (service, _) = BuildService(
            ("https://jf.local", "wrong-key"),
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        );

        await Assert.ThrowsAsync<JellyfinAuthException>(() => service.GetServerInfoAsync());
    }

    [Fact]
    public async Task GetServerInfo_503_ThrowsUnreachable()
    {
        var (service, _) = BuildService(
            ("https://jf.local", "tok"),
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        );

        var ex = await Assert.ThrowsAsync<JellyfinUnreachableException>(() => service.GetServerInfoAsync());
        Assert.Equal(503, ex.StatusCode);
    }

    [Fact]
    public async Task GetServerInfo_NetworkFailure_ThrowsUnreachable()
    {
        var (service, _) = BuildService(
            ("https://jf.local", "tok"),
            _ => throw new HttpRequestException("boom")
        );

        var ex = await Assert.ThrowsAsync<JellyfinUnreachableException>(() => service.GetServerInfoAsync());
        Assert.Null(ex.StatusCode);
    }

    [Fact]
    public async Task ListLibraries_Happy_ReturnsMappedItems()
    {
        var (service, _) = BuildService(
            ("https://jf.local", "tok"),
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"Items":[{"Id":"lib-1","Name":"Movies","CollectionType":"movies"},{"Id":"lib-2","Name":"TV Shows","CollectionType":"tvshows"}]}""",
                    Encoding.UTF8,
                    "application/json"
                )
            }
        );

        var libs = await service.ListLibrariesAsync();
        Assert.Equal(2, libs.Count);
        Assert.Equal("lib-1", libs[0].Id);
        Assert.Equal("Movies", libs[0].Name);
        Assert.Equal("movies", libs[0].CollectionType);
    }

    [Fact]
    public async Task GetItem_Happy_ReturnsMappedItem()
    {
        var (service, _) = BuildService(
            ("https://jf.local", "tok"),
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"Id":"item-1","Name":"Die Hard","Type":"Movie","RunTimeTicks":73200000000,"ProductionYear":1988,"Overview":"NYPD cop John McClane..."}""",
                    Encoding.UTF8,
                    "application/json"
                )
            }
        );

        var item = await service.GetItemAsync("item-1");
        Assert.NotNull(item);
        Assert.Equal("Die Hard", item!.Name);
        Assert.Equal(1988, item.ProductionYear);
        Assert.Equal(73200000000, item.RunTimeTicks);
    }

    [Fact]
    public async Task GetItem_404_ReturnsNull()
    {
        var (service, _) = BuildService(
            ("https://jf.local", "tok"),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        );

        var item = await service.GetItemAsync("nope");
        Assert.Null(item);
    }

    [Fact]
    public async Task AuthHeader_SetCorrectly()
    {
        HttpRequestMessage? captured = null;
        var (service, _) = BuildService(
            ("https://jf.local", "secret-tok"),
            req =>
            {
                captured = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"ServerName":"X","Version":"1.0","Id":"x"}""",
                        Encoding.UTF8,
                        "application/json"
                    )
                };
            }
        );

        await service.GetServerInfoAsync();

        Assert.NotNull(captured);
        Assert.NotNull(captured!.Headers.Authorization);
        Assert.Equal("MediaBrowser", captured.Headers.Authorization!.Scheme);
        Assert.Contains("Token=\"secret-tok\"", captured.Headers.Authorization.Parameter ?? "");
    }

    private sealed class StubMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_respond(request));
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubConnectionService : IJellyfinConnectionService
    {
        private readonly (string BaseUrl, string ApiKey)? _creds;
        public StubConnectionService((string BaseUrl, string ApiKey)? creds)
        {
            _creds = creds;
        }

        public Task<JellyfinConnectionStatus> GetStatusAsync(CancellationToken ct = default)
            => Task.FromResult(new JellyfinConnectionStatus(_creds is not null, _creds?.BaseUrl, null));
        public Task SetAsync(string baseUrl, string apiKey, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<(string BaseUrl, string ApiKey)?> GetCredentialsAsync(CancellationToken ct = default)
            => Task.FromResult(_creds);
        public Task MarkVerifiedAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
