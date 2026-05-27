namespace SignalNine.Core.Data.Jobs;

public class JobProgressSnapshot
{
    public int Percent { get; set; }

    public string Message { get; set; } = "";

    public DateTimeOffset UpdatedAt { get; set; }
}
