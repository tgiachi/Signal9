namespace SignalNine.Core.Data.Jobs;

public class EnqueueJobCommand
{
    public string Type { get; set; } = "";

    public string PayloadJson { get; set; } = "{}";
}
