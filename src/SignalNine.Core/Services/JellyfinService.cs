using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using SignalNine.Core.Data.Jellyfin;
using SignalNine.Core.Interfaces;

namespace SignalNine.Core.Services;

public class JellyfinService : IJellyfinService
{
    private const string HttpClientName = "jellyfin";
    private const string CredentialsCacheKey = "jellyfin:cred";
    private static readonly TimeSpan CredentialsCacheTtl = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJellyfinConnectionService _connectionService;
    private readonly IMemoryCache _cache;

    public JellyfinService(
        IHttpClientFactory httpClientFactory,
        IJellyfinConnectionService connectionService,
        IMemoryCache cache
    )
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(connectionService);
        ArgumentNullException.ThrowIfNull(cache);

        _httpClientFactory = httpClientFactory;
        _connectionService = connectionService;
        _cache = cache;
    }

    public async Task<JellyfinServerInfo> GetServerInfoAsync(CancellationToken ct = default)
    {
        var dto = await GetAsync<SystemInfoDto>("System/Info", ct).ConfigureAwait(false)
                  ?? throw new JellyfinUnreachableException("Empty response for System/Info.");
        return new JellyfinServerInfo(dto.ServerName ?? "", dto.Version ?? "", dto.Id ?? "");
    }

    public async Task<IReadOnlyList<JellyfinLibrarySummary>> ListLibrariesAsync(CancellationToken ct = default)
    {
        var dto = await GetAsync<ItemsResultDto>("Library/MediaFolders", ct).ConfigureAwait(false);
        if (dto?.Items is null) return Array.Empty<JellyfinLibrarySummary>();

        return dto.Items
                  .Select(i => new JellyfinLibrarySummary(i.Id ?? "", i.Name ?? "", i.CollectionType))
                  .ToList();
    }

    public async Task<JellyfinItem?> GetItemAsync(string itemId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        var dto = await GetAsync<ItemDto>($"Items/{itemId}", ct, allowNotFound: true).ConfigureAwait(false);
        if (dto is null) return null;

        return new JellyfinItem(
            dto.Id ?? "",
            dto.Name ?? "",
            dto.Type ?? "",
            dto.RunTimeTicks,
            dto.ProductionYear,
            dto.Overview,
            dto.SeriesName,
            dto.ParentIndexNumber,
            dto.IndexNumber
        );
    }

    public async Task<IReadOnlyList<JellyfinItem>> ListItemsAsync(string libraryId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryId);

        const int pageSize = 500;
        var all = new List<JellyfinItem>();
        var start = 0;
        int? total = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var path =
                $"Items?ParentId={Uri.EscapeDataString(libraryId)}" +
                "&Recursive=true" +
                "&Fields=Overview,RunTimeTicks,ProductionYear,SeriesName,ParentIndexNumber,IndexNumber" +
                $"&StartIndex={start}" +
                $"&Limit={pageSize}";

            var page = await GetAsync<ItemsResultDto>(path, ct).ConfigureAwait(false);
            if (page?.Items is null || page.Items.Count == 0) break;

            foreach (var dto in page.Items)
            {
                all.Add(new JellyfinItem(
                    dto.Id ?? "",
                    dto.Name ?? "",
                    dto.Type ?? "",
                    dto.RunTimeTicks,
                    dto.ProductionYear,
                    dto.Overview,
                    dto.SeriesName,
                    dto.ParentIndexNumber,
                    dto.IndexNumber
                ));
            }

            total ??= page.TotalRecordCount ?? all.Count;
            start += page.Items.Count;

            if (start >= total) break;
        }

        return all;
    }

    public async Task<IReadOnlyList<JellyfinPreviewImage>> GetPreviewImagesAsync(
        string itemId,
        int maxImages,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        if (maxImages <= 0) return Array.Empty<JellyfinPreviewImage>();

        var results = await FetchImagesForAsync(itemId, maxImages, ct).ConfigureAwait(false);
        if (results.Count > 0) return results;

        // Fallback: episode (or any item) with no own images — try the series/parent.
        var parentId = await ResolveSeriesIdAsync(itemId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(parentId)) return results;

        return await FetchImagesForAsync(parentId, maxImages, ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<JellyfinPreviewImage>> FetchImagesForAsync(
        string itemId,
        int maxImages,
        CancellationToken ct)
    {
        var infos = await GetAsync<List<ImageInfoDto>>($"Items/{Uri.EscapeDataString(itemId)}/Images", ct)
            .ConfigureAwait(false);
        if (infos is null || infos.Count == 0) return Array.Empty<JellyfinPreviewImage>();

        var ordered = infos
            .Where(i => !string.IsNullOrWhiteSpace(i.ImageType) && IsSupportedImageType(i.ImageType!))
            .OrderBy(i => i.ImageType switch
            {
                "Primary" => 0,
                "Thumb" => 1,
                "Screenshot" => 2,
                "Backdrop" => 3,
                "Art" => 4,
                "Banner" => 5,
                "Logo" => 6,
                "Disc" => 7,
                "Box" => 8,
                "BoxRear" => 9,
                _ => 99
            })
            .ThenBy(i => i.ImageIndex ?? 0)
            .Take(maxImages)
            .ToList();

        var results = new List<JellyfinPreviewImage>(ordered.Count);
        foreach (var info in ordered)
        {
            ct.ThrowIfCancellationRequested();
            var index = info.ImageIndex ?? 0;
            var path = $"Items/{Uri.EscapeDataString(itemId)}/Images/{info.ImageType}/{index}";
            var bytes = await DownloadBytesAsync(path, ct).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0) continue;

            var sourceName = $"{info.ImageType}-{index}";
            results.Add(new JellyfinPreviewImage(sourceName, bytes));
        }

        return results;
    }

    private async Task<string?> ResolveSeriesIdAsync(string itemId, CancellationToken ct)
    {
        // Use the search-style endpoint — some Jellyfin streamers reject
        // /Items/{id}?Fields=... with HTTP 400 but accept /Items?Ids={id}&Fields=...
        var result = await GetAsync<ItemsResultDto>(
            $"Items?Ids={Uri.EscapeDataString(itemId)}&Fields=SeriesId,ParentId",
            ct,
            allowNotFound: true
        ).ConfigureAwait(false);
        var dto = result?.Items?.FirstOrDefault();
        if (dto is null) return null;
        if (!string.IsNullOrWhiteSpace(dto.SeriesId)) return dto.SeriesId;
        if (!string.IsNullOrWhiteSpace(dto.ParentId)) return dto.ParentId;
        return null;
    }

    private static bool IsSupportedImageType(string imageType)
    {
        return imageType is "Primary"
            or "Thumb"
            or "Screenshot"
            or "Backdrop"
            or "Art"
            or "Banner"
            or "Logo"
            or "Disc"
            or "Box"
            or "BoxRear";
    }

    public async Task<JellyfinItemTags> GetItemTagsAsync(string itemId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        var dto = await GetAsync<ItemDto>(
            $"Items/{Uri.EscapeDataString(itemId)}?Fields=Tags,Genres",
            ct,
            allowNotFound: true
        ).ConfigureAwait(false);
        if (dto is null)
        {
            return new JellyfinItemTags(Array.Empty<string>(), Array.Empty<string>());
        }

        return new JellyfinItemTags(
            (IReadOnlyList<string>?)dto.Genres ?? Array.Empty<string>(),
            (IReadOnlyList<string>?)dto.Tags ?? Array.Empty<string>()
        );
    }

    private async Task<byte[]?> DownloadBytesAsync(string relativePath, CancellationToken ct)
    {
        var creds = await ResolveCredentialsAsync(ct).ConfigureAwait(false);
        var http = _httpClientFactory.CreateClient(HttpClientName);

        var requestUri = new Uri(new Uri(creds.BaseUrl.TrimEnd('/') + "/"), relativePath);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("MediaBrowser", $"Token=\"{creds.ApiKey}\"");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new JellyfinUnreachableException(ex.Message);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new JellyfinUnreachableException("Request timed out.", null);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new JellyfinAuthException("Jellyfin rejected the credentials (HTTP 401).");
            }
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
            {
                throw new JellyfinUnreachableException(
                    $"Jellyfin returned HTTP {(int)response.StatusCode}.",
                    (int)response.StatusCode
                );
            }

            return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task<T?> GetAsync<T>(string relativePath, CancellationToken ct, bool allowNotFound = false)
    {
        var creds = await ResolveCredentialsAsync(ct).ConfigureAwait(false);
        var http = _httpClientFactory.CreateClient(HttpClientName);

        var requestUri = new Uri(new Uri(creds.BaseUrl.TrimEnd('/') + "/"), relativePath);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("MediaBrowser", $"Token=\"{creds.ApiKey}\"");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new JellyfinUnreachableException(ex.Message);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new JellyfinUnreachableException("Request timed out.", null);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new JellyfinAuthException("Jellyfin rejected the credentials (HTTP 401).");
            }

            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new JellyfinUnreachableException(
                    $"Jellyfin returned HTTP {(int)response.StatusCode}.",
                    (int)response.StatusCode
                );
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
        }
    }

    private async Task<(string BaseUrl, string ApiKey)> ResolveCredentialsAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CredentialsCacheKey, out (string BaseUrl, string ApiKey) cached))
        {
            return cached;
        }

        var creds = await _connectionService.GetCredentialsAsync(ct).ConfigureAwait(false)
                    ?? throw new JellyfinNotConfiguredException();

        _cache.Set(CredentialsCacheKey, creds, CredentialsCacheTtl);
        return creds;
    }

    private sealed class SystemInfoDto
    {
        public string? ServerName { get; set; }
        public string? Version { get; set; }
        public string? Id { get; set; }
    }

    private sealed class ItemsResultDto
    {
        public List<ItemDto>? Items { get; set; }
        public int? TotalRecordCount { get; set; }
    }

    private sealed class ItemDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? CollectionType { get; set; }
        public long? RunTimeTicks { get; set; }
        public int? ProductionYear { get; set; }
        public string? Overview { get; set; }
        public string? SeriesName { get; set; }
        public int? ParentIndexNumber { get; set; }
        public int? IndexNumber { get; set; }
        public List<string>? Tags { get; set; }
        public List<string>? Genres { get; set; }
        public string? SeriesId { get; set; }
        public string? ParentId { get; set; }
    }

    private sealed class ImageInfoDto
    {
        public string? ImageType { get; set; }
        public int? ImageIndex { get; set; }
        public string? ImageTag { get; set; }
    }
}
