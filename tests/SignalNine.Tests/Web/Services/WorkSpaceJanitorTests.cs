using SignalNine.Core.Data.Config;
using SignalNine.Web.Services;

namespace SignalNine.Tests.Web.Services;

public class WorkSpaceJanitorTests : IDisposable
{
    private readonly string _root;

    public WorkSpaceJanitorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"signalnine-janitor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private WorkSpaceJanitor CreateJanitor(int orphanCleanupHours = 24)
    {
        var config = new SignalNineConfig
        {
            WorkSpace =
            {
                Path = _root,
                OrphanCleanupHours = orphanCleanupHours
            }
        };
        return new WorkSpaceJanitor(config, TimeSpan.FromMilliseconds(50));
    }

    private string CreateJobDir(string name)
    {
        var path = Path.Combine(_root, "jobs", name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SetAge(string dir, double hoursOld)
    {
        var mtime = DateTime.UtcNow.AddHours(-hoursOld);
        Directory.SetLastWriteTimeUtc(dir, mtime);
    }

    [Fact]
    public void RunOnce_JobsRootMissing_DoesNotThrow()
    {
        // _root/jobs does not exist
        var janitor = CreateJanitor();

        var exception = Record.Exception(() => janitor.RunOnce());

        Assert.Null(exception);
    }

    [Fact]
    public void RunOnce_FreshDirs_AreNotDeleted()
    {
        var a = CreateJobDir("a");
        var b = CreateJobDir("b");
        SetAge(a, 1);
        SetAge(b, 1);

        var janitor = CreateJanitor(orphanCleanupHours: 24);
        janitor.RunOnce();

        Assert.True(Directory.Exists(a));
        Assert.True(Directory.Exists(b));
    }

    [Fact]
    public void RunOnce_OldDirs_AreReaped()
    {
        var old1 = CreateJobDir("old1");
        var old2 = CreateJobDir("old2");
        SetAge(old1, 30);
        SetAge(old2, 30);

        var janitor = CreateJanitor(orphanCleanupHours: 24);
        janitor.RunOnce();

        Assert.False(Directory.Exists(old1));
        Assert.False(Directory.Exists(old2));
    }

    [Fact]
    public void RunOnce_MixedDirs_OnlyOldReaped()
    {
        var fresh = CreateJobDir("fresh");
        var old = CreateJobDir("old");
        SetAge(fresh, 1);
        SetAge(old, 30);

        var janitor = CreateJanitor(orphanCleanupHours: 24);
        janitor.RunOnce();

        Assert.True(Directory.Exists(fresh));
        Assert.False(Directory.Exists(old));
    }

    [Fact]
    public async Task ExecuteAsync_Disabled_OldDirsAreNotReaped()
    {
        var old = CreateJobDir("stale");
        SetAge(old, 30);

        // OrphanCleanupHours = 0 means disabled — ExecuteAsync returns immediately without reaping.
        var janitor = CreateJanitor(orphanCleanupHours: 0);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await janitor.StartAsync(cts.Token);
        await Task.Delay(200, CancellationToken.None);
        await janitor.StopAsync(CancellationToken.None);

        Assert.True(Directory.Exists(old));
    }
}
