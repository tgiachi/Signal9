using SignalNine.Jobs.Data.Pipeline;

namespace SignalNine.Jobs.Interfaces;

public interface IPipelineTask
{
    string Name { get; }

    int Order { get; }

    bool IsEnabled { get; }

    Task ExecuteAsync(PipelineContext context, CancellationToken ct);
}
