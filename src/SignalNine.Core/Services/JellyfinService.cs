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
            dto.Overview
        );
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
    }
}
