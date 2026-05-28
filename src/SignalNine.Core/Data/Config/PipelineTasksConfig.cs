namespace SignalNine.Core.Data.Config;

public class PipelineTasksConfig
{
    public PipelineProbeTaskConfig Probe { get; set; } = new();

    public PipelinePreviewTaskConfig Preview { get; set; } = new();

    public PipelineJellyfinPreviewTaskConfig JellyfinPreview { get; set; } = new();

    public PipelineJellyfinTagsTaskConfig JellyfinTags { get; set; } = new();

    public PipelineTaggerTaskConfig Tagger { get; set; } = new();
}
