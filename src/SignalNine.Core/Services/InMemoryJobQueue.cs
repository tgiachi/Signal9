// src/SignalNine.Core/Services/InMemoryJobQueue.cs
using System.Collections.Concurrent;
using System.Threading.Channels;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;

namespace SignalNine.Core.Services;

public sealed class InMemoryJobQueue : IJobQueue
{
    private readonly ConcurrentDictionary<JobStreamTarget, Channel<QueuedJob>> _channels = new();

    private Channel<QueuedJob> GetChannel(JobStreamTarget target)
        => _channels.GetOrAdd(target, _ => Channel.CreateUnbounded<QueuedJob>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        }));

    public Task PushAsync(JobEnvelope envelope, JobStreamTarget target, CancellationToken cancellationToken = default)
    {
        var queued = new QueuedJob(Guid.NewGuid().ToString("N"), envelope);
        return GetChannel(target).Writer.WriteAsync(queued, cancellationToken).AsTask();
    }

    public async Task<QueuedJob?> PullAsync(string consumerName, JobStreamTarget target, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetChannel(target).Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public Task AckAsync(string streamId, JobStreamTarget target, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RequeueLaterAsync(JobEnvelope envelope, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                await PushAsync(envelope, JobStreamTarget.Workers, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }, CancellationToken.None);
        return Task.CompletedTask;
    }
}
