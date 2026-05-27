using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using SignalNine.Persistence.Types;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Data.Channels;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class ChannelMediaEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;

    public ChannelMediaEndpointTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
        _previousRootDirectory = Environment.GetEnvironmentVariable(RootDirectoryVariableName);
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _rootDirectory);

        _factory = new WebApplicationFactory<Program>();
    }

    public void Dispose()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _previousRootDirectory);

        if (Directory.Exists(_rootDirectory))
        {
            try { Directory.Delete(_rootDirectory, recursive: true); } catch { /* best effort */ }
        }

        GC.SuppressFinalize(this);
    }

    private static CreateChannelMediaRequest NewMovieRequest(string title = "Die Hard") =>
        new(
            Type: ChannelMediaType.Movies,
            Title: title,
            DurationSeconds: 7320,
            SourceType: MediaSourceType.Jellyfin,
            SourceRef: "jf-movie-1",
            MovieReleaseYear: 1988,
            MovieDirector: "John McTiernan",
            TvSeriesName: null,
            TvSeason: null,
            TvEpisode: null,
            CommercialAdvertiser: null,
            CommercialCampaign: null,
            InformationEdition: null
        );

    [Fact]
    public async Task Post_Media_Unauthenticated_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/media", NewMovieRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_Media_Authenticated_CreatesAndReturnsCreated()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.PostAsJsonAsync("/api/media", NewMovieRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ChannelMediaResponse>();
        Assert.NotNull(body);
        Assert.Equal(ChannelMediaType.Movies, body!.Type);
        Assert.Equal(1988, body.MovieReleaseYear);
    }

    [Fact]
    public async Task Get_Media_FilterByType_ReturnsOnlyMatchingType()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        await client.PostAsJsonAsync("/api/media", NewMovieRequest("Movie A"));

        var bumperReq = new CreateChannelMediaRequest(
            Type: ChannelMediaType.Bumper, Title: "Bumper 1", DurationSeconds: 5,
            SourceType: MediaSourceType.LocalFile, SourceRef: "bumpers/1.mp4",
            MovieReleaseYear: null, MovieDirector: null,
            TvSeriesName: null, TvSeason: null, TvEpisode: null,
            CommercialAdvertiser: null, CommercialCampaign: null, InformationEdition: null
        );
        await client.PostAsJsonAsync("/api/media", bumperReq);

        var response = await client.GetAsync("/api/media?type=Movies");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<ChannelMediaResponse>>();
        Assert.NotNull(list);
        Assert.All(list!, m => Assert.Equal(ChannelMediaType.Movies, m.Type));
    }

    [Fact]
    public async Task Delete_Media_RemovesIt()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var created = await (await client.PostAsJsonAsync("/api/media", NewMovieRequest("Delete Me")))
                                 .Content.ReadFromJsonAsync<ChannelMediaResponse>();

        var del = await client.DeleteAsync($"/api/media/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var get = await client.GetAsync($"/api/media/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task AttachAndDetach_Tag_OnMedia_Roundtrip()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var media = await (await client.PostAsJsonAsync("/api/media", NewMovieRequest("Tagged")))
                                 .Content.ReadFromJsonAsync<ChannelMediaResponse>();
        var tag = await (await client.PostAsJsonAsync("/api/tags", new CreateTagRequest("action", "Action")))
                                 .Content.ReadFromJsonAsync<TagResponse>();

        var attach = await client.PostAsync($"/api/media/{media!.Id}/tags/{tag!.Id}", content: null);
        Assert.Equal(HttpStatusCode.NoContent, attach.StatusCode);

        var attachAgain = await client.PostAsync($"/api/media/{media.Id}/tags/{tag.Id}", content: null);
        Assert.Equal(HttpStatusCode.Conflict, attachAgain.StatusCode);

        var detach = await client.DeleteAsync($"/api/media/{media.Id}/tags/{tag.Id}");
        Assert.Equal(HttpStatusCode.NoContent, detach.StatusCode);
    }
}
