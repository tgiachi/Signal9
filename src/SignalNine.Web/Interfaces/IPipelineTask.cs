using SignalNine.Web.Data.Pipeline;

namespace SignalNine.Web.Interfaces;

public interface IPipelineTask
{
    string Name { get; }

    int Order { get; }

    bool IsEnabled { get; }

    Task ExecuteAsync(PipelineContext context, CancellationToken ct);
}
