namespace SignalNine.Core.Data.Config;

public class PipelinePreviewTaskConfig
{
    private const int DefaultPreviewCount = 5;

    public bool Enabled { get; set; } = true;

    public bool OverwriteExisting { get; set; } = false;

    public bool AllowJellyfinStreamFallback { get; set; } = false;

    public int PreviewCount { get; set; } = DefaultPreviewCount;
}
