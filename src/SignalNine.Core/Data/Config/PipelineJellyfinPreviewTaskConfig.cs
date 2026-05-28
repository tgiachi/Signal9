namespace SignalNine.Core.Data.Config;

public class PipelineJellyfinPreviewTaskConfig
{
    private const int DefaultMaxImages = 3;

    public bool Enabled { get; set; } = true;

    public bool OverwriteExisting { get; set; } = false;

    public int MaxImages { get; set; } = DefaultMaxImages;
}
