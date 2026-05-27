using System.Text;
using Microsoft.AspNetCore.DataProtection;
using SignalNine.Core.Data.Jellyfin;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Jellyfin;
using SignalNine.Persistence.Interfaces;

namespace SignalNine.Persistence.Services;

public class JellyfinConnectionService : IJellyfinConnectionService
{
    private const string ProtectorPurpose = "SignalNine.Jellyfin.ApiKey";

    private readonly IDataAccess<JellyfinConnectionEntity> _dataAccess;
    private readonly IDataProtector _protector;

    public JellyfinConnectionService(
        IDataAccess<JellyfinConnectionEntity> dataAccess,
        IDataProtectionProvider protectionProvider
    )
    {
        ArgumentNullException.ThrowIfNull(dataAccess);
        ArgumentNullException.ThrowIfNull(protectionProvider);

        _dataAccess = dataAccess;
        _protector = protectionProvider.CreateProtector(ProtectorPurpose);
    }

    public Task<JellyfinConnectionStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var row = FindActive();
        var status = row is null
            ? new JellyfinConnectionStatus(false, null, null)
            : new JellyfinConnectionStatus(true, row.BaseUrl, row.LastVerifiedAt);

        return Task.FromResult(status);
    }

    public Task SetAsync(string baseUrl, string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("BaseUrl must be a well-formed absolute URI.", nameof(baseUrl));
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key cannot be empty.", nameof(apiKey));
        }

        var encrypted = Convert.ToBase64String(_protector.Protect(Encoding.UTF8.GetBytes(apiKey)));

        var existing = FindActive();
        if (existing is null)
        {
            _dataAccess.Insert(
                new JellyfinConnectionEntity
                {
                    Id = Guid.NewGuid(),
                    BaseUrl = baseUrl,
                    EncryptedApiKey = encrypted,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            );
        }
        else
        {
            existing.BaseUrl = baseUrl;
            existing.EncryptedApiKey = encrypted;
            existing.UpdatedAt = DateTime.UtcNow;
            _dataAccess.Update(existing);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        foreach (var row in _dataAccess.List())
        {
            _dataAccess.Delete(row.Id);
        }

        return Task.CompletedTask;
    }

    public Task<(string BaseUrl, string ApiKey)?> GetCredentialsAsync(CancellationToken ct = default)
    {
        var row = FindActive();
        if (row is null)
        {
            return Task.FromResult<(string, string)?>(null);
        }

        var cipher = Convert.FromBase64String(row.EncryptedApiKey);
        var apiKey = Encoding.UTF8.GetString(_protector.Unprotect(cipher));
        return Task.FromResult<(string, string)?>((row.BaseUrl, apiKey));
    }

    public Task MarkVerifiedAsync(CancellationToken ct = default)
    {
        var row = FindActive();
        if (row is null) return Task.CompletedTask;

        row.LastVerifiedAt = DateTime.UtcNow;
        row.UpdatedAt = DateTime.UtcNow;
        _dataAccess.Update(row);

        return Task.CompletedTask;
    }

    private JellyfinConnectionEntity? FindActive()
        => _dataAccess.List().FirstOrDefault(r => r.IsActive);
}
