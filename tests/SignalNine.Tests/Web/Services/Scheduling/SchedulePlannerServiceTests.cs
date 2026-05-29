using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Types;
using SignalNine.Web.Services.Scheduling;

namespace SignalNine.Tests.Web.Services.Scheduling;

public class SchedulePlannerServiceTests
{
    [Fact]
    public void FindBlockCovering_BlockMatchesDayAndTime_ReturnsBlock()
    {
        var block = new ScheduleBlockEntity
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(20, 0, 0),
            DurationMinutes = 120,
            IsActive = true
        };
        // 2026-06-01 is a Monday; 21:00 falls inside 20:00–22:00.
        var cursor = new DateTime(2026, 6, 1, 21, 0, 0, DateTimeKind.Utc);

        var hit = SchedulePlannerService.FindBlockCovering(new[] { block }, cursor);

        Assert.Same(block, hit);
    }

    [Fact]
    public void FindBlockCovering_CursorBeforeStart_ReturnsNull()
    {
        var block = new ScheduleBlockEntity
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(20, 0, 0),
            DurationMinutes = 120,
            IsActive = true
        };
        var cursor = new DateTime(2026, 6, 1, 19, 59, 59, DateTimeKind.Utc);

        Assert.Null(SchedulePlannerService.FindBlockCovering(new[] { block }, cursor));
    }

    [Fact]
    public void FindBlockCovering_CursorPastEnd_ReturnsNull()
    {
        var block = new ScheduleBlockEntity
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(20, 0, 0),
            DurationMinutes = 120,
            IsActive = true
        };
        var cursor = new DateTime(2026, 6, 1, 22, 0, 0, DateTimeKind.Utc);

        Assert.Null(SchedulePlannerService.FindBlockCovering(new[] { block }, cursor));
    }

    [Fact]
    public void FindBlockCovering_InactiveBlock_ReturnsNull()
    {
        var block = new ScheduleBlockEntity
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(20, 0, 0),
            DurationMinutes = 120,
            IsActive = false
        };
        var cursor = new DateTime(2026, 6, 1, 21, 0, 0, DateTimeKind.Utc);

        Assert.Null(SchedulePlannerService.FindBlockCovering(new[] { block }, cursor));
    }
}
