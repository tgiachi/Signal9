using SignalNine.Core.Types;

namespace SignalNine.Core.Data.Jobs;

public class JobLogEntry
{
    public long Sequence { get; set; }

    public Guid JobId { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public JobLogLevelType Level { get; set; }

    public string Message { get; set; } = "";
}
