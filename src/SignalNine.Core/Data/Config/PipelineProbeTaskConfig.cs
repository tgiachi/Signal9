namespace SignalNine.Core.Data.Config;

public class PipelineProbeTaskConfig
{
    public bool Enabled { get; set; } = true;

    public bool OverwriteExisting { get; set; } = false;

    public bool AllowJellyfinStreamProbe { get; set; } = false;
}
