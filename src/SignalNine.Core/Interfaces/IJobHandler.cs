using SignalNine.Core.Data.Jobs;

namespace SignalNine.Core.Interfaces;

public interface IJobHandler
{
    string Type { get; }

    Task<IJobResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken);
}
