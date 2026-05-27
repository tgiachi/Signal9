namespace SignalNine.Core.Data.Config;

public class PipelineTasksConfig
{
    public PipelineTaskToggleConfig Probe { get; set; } = new();

    public PipelineTaskToggleConfig Preview { get; set; } = new();
}
