using SignalNine.Worker.Services;

namespace SignalNine.Tests.Worker;

public class WorkerIdentityTests : IDisposable
{
    private readonly string _tempStateFile;

    public WorkerIdentityTests()
    {
        _tempStateFile = Path.Combine(Path.GetTempPath(), $"signal9-worker-id-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (File.Exists(_tempStateFile)) File.Delete(_tempStateFile);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Load_creates_new_id_when_file_missing()
    {
        var identity = WorkerIdentity.LoadOrCreate(_tempStateFile, name: "test-worker");
        Assert.NotEqual(Guid.Empty, identity.Id);
        Assert.True(File.Exists(_tempStateFile));
        Assert.Equal("test-worker", identity.Name);
    }

    [Fact]
    public void Load_returns_same_id_on_subsequent_calls()
    {
        var first = WorkerIdentity.LoadOrCreate(_tempStateFile, name: "n");
        var second = WorkerIdentity.LoadOrCreate(_tempStateFile, name: "n");
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public void Explicit_id_via_env_overrides_file()
    {
        var explicitId = Guid.NewGuid();
        var identity = WorkerIdentity.LoadOrCreate(_tempStateFile, name: "n", explicitId: explicitId);
        Assert.Equal(explicitId, identity.Id);
        // Persistence still happens for consistency, but explicit always wins
    }

    [Fact]
    public void Default_name_falls_back_to_hostname()
    {
        var identity = WorkerIdentity.LoadOrCreate(_tempStateFile, name: null);
        Assert.False(string.IsNullOrWhiteSpace(identity.Name));
    }
}
