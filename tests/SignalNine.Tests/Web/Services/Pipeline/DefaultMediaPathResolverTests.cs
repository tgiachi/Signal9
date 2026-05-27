using SignalNine.Core.Data.Jellyfin;
using SignalNine.Core.Data.Pipeline;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Types;
using SignalNine.Web.Services.Pipeline;

namespace SignalNine.Tests.Web.Services.Pipeline;

public class DefaultMediaPathResolverTests
{
    [Fact]
    public async Task LocalFile_CombinesLibraryRootAndMediaRelativePath()
    {
        var resolver = new DefaultMediaPathResolver(new StubConnection(null));
        var media = new ChannelMediaEntity
        {
            SourceType = MediaSourceType.LocalFile,
            SourceRef = "movie.mkv"
        };
        var library = new MediaLibraryEntity
        {
            SourceType = MediaSourceType.LocalFile,
            SourceRef = "/media/movies"
        };

        var result = await resolver.ResolveAsync(media, library, CancellationToken.None);

        Assert.Equal(Path.Combine("/media/movies", "movie.mkv"), result);
    }

    [Fact]
    public async Task LocalFile_EmptyLibrarySourceRef_ThrowsMediaPathResolutionException()
    {
        var resolver = new DefaultMediaPathResolver(new StubConnection(null));
        var media = new ChannelMediaEntity
        {
            SourceType = MediaSourceType.LocalFile,
            SourceRef = "movie.mkv"
        };
        var library = new MediaLibraryEntity
        {
            SourceType = MediaSourceType.LocalFile,
            SourceRef = ""
        };

        await Assert.ThrowsAsync<MediaPathResolutionException>(
            () => resolver.ResolveAsync(media, library, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Jellyfin_BuildsStreamUrlWithBaseUrlItemIdStaticAndApiKey()
    {
        var creds = ("http://jellyfin.local:8096", "myapikey");
        var resolver = new DefaultMediaPathResolver(new StubConnection(creds));
        var media = new ChannelMediaEntity
        {
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = "abc123"
        };
        var library = new MediaLibraryEntity { SourceType = MediaSourceType.Jellyfin };

        var result = await resolver.ResolveAsync(media, library, CancellationToken.None);

        Assert.Contains("http://jellyfin.local:8096", result);
        Assert.Contains("/Videos/abc123/stream", result);
        Assert.Contains("static=true", result);
        Assert.Contains("api_key=myapikey", result);
    }

    [Fact]
    public async Task Jellyfin_TrailingSlashOnBaseUrl_NormalizedNoDoubleSlash()
    {
        var creds = ("http://jellyfin.local:8096/", "key");
        var resolver = new DefaultMediaPathResolver(new StubConnection(creds));
        var media = new ChannelMediaEntity
        {
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = "item42"
        };
        var library = new MediaLibraryEntity { SourceType = MediaSourceType.Jellyfin };

        var result = await resolver.ResolveAsync(media, library, CancellationToken.None);

        Assert.DoesNotContain("//Videos", result);
        Assert.Contains("/Videos/item42/stream", result);
    }

    [Fact]
    public async Task Jellyfin_NoConnection_ThrowsMediaPathResolutionException()
    {
        var resolver = new DefaultMediaPathResolver(new StubConnection(null));
        var media = new ChannelMediaEntity { SourceType = MediaSourceType.Jellyfin };
        var library = new MediaLibraryEntity { SourceType = MediaSourceType.Jellyfin };

        await Assert.ThrowsAsync<MediaPathResolutionException>(
            () => resolver.ResolveAsync(media, library, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Url_ThrowsMediaPathResolutionException()
    {
        var resolver = new DefaultMediaPathResolver(new StubConnection(null));
        var media = new ChannelMediaEntity { SourceType = MediaSourceType.Url };
        var library = new MediaLibraryEntity { SourceType = MediaSourceType.Url };

        await Assert.ThrowsAsync<MediaPathResolutionException>(
            () => resolver.ResolveAsync(media, library, CancellationToken.None)
        );
    }

    private sealed class StubConnection : IJellyfinConnectionService
    {
        private readonly (string BaseUrl, string ApiKey)? _creds;

        public StubConnection((string BaseUrl, string ApiKey)? creds)
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
