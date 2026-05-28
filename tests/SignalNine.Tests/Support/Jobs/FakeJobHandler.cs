using System.Threading.Tasks;

using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Jobs.Results;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;

namespace SignalNine.Tests.Support.Jobs;

public class FakeJobHandler : IJobHandler
{
    public string Type { get; init; } = "test.fake";

    private int _startedCount;

    public int StartedCount => Volatile.Read(ref _startedCount);

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<IJobResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _startedCount);
        Started.TrySetResult();
        await context.WriteLogAsync(JobLogLevelType.Information, "Started", cancellationToken);
        await Release.Task.WaitAsync(cancellationToken);
        await context.ReportProgressAsync(100, "Done", cancellationToken);
        return new EmptyJobResult(Type);
    }

    public async Task WaitForStartedCountAsync(int expectedCount, CancellationToken cancellationToken)
    {
        if (expectedCount <= 0)
        {
            return;
        }

        while (StartedCount < expectedCount)
        {
            await Task.Delay(10, cancellationToken);
        }
    }
}
