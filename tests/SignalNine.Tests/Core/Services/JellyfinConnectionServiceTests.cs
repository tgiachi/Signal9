using FreeSql;
using Microsoft.AspNetCore.DataProtection;
using SignalNine.Persistence.Entities.Jellyfin;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Services;

namespace SignalNine.Tests.Core.Services;

public class JellyfinConnectionServiceTests : IDisposable
{
    private readonly string _databasePath;
    private readonly IFreeSql _freeSql;
    private readonly IDataAccess<JellyfinConnectionEntity> _dataAccess;
    private readonly StubDataProtectionProvider _protectionProvider;
    private readonly JellyfinConnectionService _service;

    public JellyfinConnectionServiceTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}.db");
        _freeSql = new FreeSqlBuilder()
                   .UseConnectionString(DataType.Sqlite, $"Data Source={_databasePath}")
                   .UseAutoSyncStructure(true)
                   .Build();

        _freeSql.CodeFirst.SyncStructure<JellyfinConnectionEntity>();
        _dataAccess = new FreeSqlDataAccess<JellyfinConnectionEntity>(_freeSql);
        _protectionProvider = new StubDataProtectionProvider();
        _service = new JellyfinConnectionService(_dataAccess, _protectionProvider);
    }

    public void Dispose()
    {
        _freeSql.Dispose();
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetStatus_NotConfigured_ReturnsNotConfigured()
    {
        var status = await _service.GetStatusAsync();
        Assert.False(status.IsConfigured);
        Assert.Null(status.BaseUrl);
        Assert.Null(status.LastVerifiedAt);
    }

    [Fact]
    public async Task SetAsync_EncryptsKeyAndPersists()
    {
        await _service.SetAsync("https://jellyfin.example.com", "raw-api-key");

        var status = await _service.GetStatusAsync();
        Assert.True(status.IsConfigured);
        Assert.Equal("https://jellyfin.example.com", status.BaseUrl);

        var row = _dataAccess.List().Single();
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row.EncryptedApiKey));
        Assert.Equal("ENC(raw-api-key)", decoded);
    }

    [Fact]
    public async Task SetAsync_Upsert_ReplacesExistingRow()
    {
        await _service.SetAsync("https://first.local", "key-1");
        await _service.SetAsync("https://second.local", "key-2");

        var rows = _dataAccess.List();
        Assert.Single(rows);
        Assert.Equal("https://second.local", rows[0].BaseUrl);
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(rows[0].EncryptedApiKey));
        Assert.Equal("ENC(key-2)", decoded);
    }

    [Fact]
    public async Task SetAsync_InvalidUrl_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetAsync("not a url", "k")
        );
    }

    [Fact]
    public async Task GetCredentials_ReturnsDecryptedPair()
    {
        await _service.SetAsync("https://jf.local", "secret-key");

        var creds = await _service.GetCredentialsAsync();
        Assert.NotNull(creds);
        Assert.Equal("https://jf.local", creds!.Value.BaseUrl);
        Assert.Equal("secret-key", creds.Value.ApiKey);
    }

    [Fact]
    public async Task GetCredentials_NoRow_ReturnsNull()
    {
        var creds = await _service.GetCredentialsAsync();
        Assert.Null(creds);
    }

    [Fact]
    public async Task ClearAsync_RemovesAllRows()
    {
        await _service.SetAsync("https://jf.local", "k");
        await _service.ClearAsync();

        Assert.Empty(_dataAccess.List());
        var status = await _service.GetStatusAsync();
        Assert.False(status.IsConfigured);
    }

    [Fact]
    public async Task MarkVerifiedAsync_UpdatesTimestamp()
    {
        await _service.SetAsync("https://jf.local", "k");

        var before = (await _service.GetStatusAsync()).LastVerifiedAt;
        Assert.Null(before);

        await _service.MarkVerifiedAsync();

        var after = (await _service.GetStatusAsync()).LastVerifiedAt;
        Assert.NotNull(after);
    }

    [Fact]
    public async Task MarkVerifiedAsync_NoRow_NoOp()
    {
        await _service.MarkVerifiedAsync();

        var status = await _service.GetStatusAsync();
        Assert.False(status.IsConfigured);
    }

    private sealed class StubDataProtectionProvider : IDataProtectionProvider
    {
        public IDataProtector CreateProtector(string purpose) => new StubProtector();

        private sealed class StubProtector : IDataProtector
        {
            public IDataProtector CreateProtector(string purpose) => this;
            public byte[] Protect(byte[] plaintext)
                => System.Text.Encoding.UTF8.GetBytes($"ENC({System.Text.Encoding.UTF8.GetString(plaintext)})");
            public byte[] Unprotect(byte[] protectedData)
            {
                var s = System.Text.Encoding.UTF8.GetString(protectedData);
                if (!s.StartsWith("ENC(") || !s.EndsWith(")"))
                    throw new InvalidOperationException("Stub: not an ENC(...) wrapper.");
                return System.Text.Encoding.UTF8.GetBytes(s[4..^1]);
            }
        }
    }
}
