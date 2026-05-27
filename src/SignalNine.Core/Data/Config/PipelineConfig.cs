namespace SignalNine.Core.Data.Config;

public class PipelineConfig
{
    private const int DefaultPreviewCount = 5;

    public int PreviewCount { get; set; } = DefaultPreviewCount;

    public bool OverwriteExistingProbe { get; set; } = false;

    public PipelineTasksConfig Tasks { get; set; } = new();
}
