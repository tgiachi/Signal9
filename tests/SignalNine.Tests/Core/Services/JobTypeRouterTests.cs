using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Services;

namespace SignalNine.Tests.Core.Services;

public class JobTypeRouterTests
{
    [Fact]
    public void LibraryScan_RoutesToInternal()
    {
        var router = new JobTypeRouter();
        Assert.Equal(JobStreamTarget.Internal, router.ResolveTarget("library.scan"));
    }

    [Fact]
    public void MediaPipeline_RoutesToWorkers()
    {
        var router = new JobTypeRouter();
        Assert.Equal(JobStreamTarget.Workers, router.ResolveTarget("media.pipeline"));
    }

    [Fact]
    public void UnknownType_DefaultsToWorkers()
    {
        var router = new JobTypeRouter();
        Assert.Equal(JobStreamTarget.Workers, router.ResolveTarget("custom.unknown"));
    }

    [Fact]
    public void IsCaseInsensitive()
    {
        var router = new JobTypeRouter();
        Assert.Equal(JobStreamTarget.Internal, router.ResolveTarget("LIBRARY.SCAN"));
    }
}
