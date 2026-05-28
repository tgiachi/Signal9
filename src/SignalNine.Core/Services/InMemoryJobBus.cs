// src/SignalNine.Core/Services/InMemoryJobBus.cs
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;

namespace SignalNine.Core.Services;

public sealed class InMemoryJobBus : IJobBus
{
    private readonly Broadcaster<JobProgressEvent> _progress = new();
    private readonly Broadcaster<JobLogEvent> _log = new();
    private readonly Broadcaster<JobResultEvent> _result = new();
    private readonly Broadcaster<Guid> _cancel = new();
    private readonly Broadcaster<WorkerHeartbeat> _heartbeat = new();

    public Task PublishProgressAsync(JobProgressEvent e, CancellationToken ct = default) => _progress.PublishAsync(e);
    public Task PublishLogAsync(JobLogEvent e, CancellationToken ct = default) => _log.PublishAsync(e);
    public Task PublishResultAsync(JobResultEvent e, CancellationToken ct = default) => _result.PublishAsync(e);
    public Task PublishCancelAsync(Guid jobId, CancellationToken ct = default) => _cancel.PublishAsync(jobId);
    public Task PublishHeartbeatAsync(WorkerHeartbeat h, CancellationToken ct = default) => _heartbeat.PublishAsync(h);

    public IAsyncEnumerable<JobProgressEvent> SubscribeProgressAsync(CancellationToken ct) => _progress.SubscribeAsync(ct);
    public IAsyncEnumerable<JobLogEvent> SubscribeLogAsync(CancellationToken ct) => _log.SubscribeAsync(ct);
    public IAsyncEnumerable<JobResultEvent> SubscribeResultAsync(CancellationToken ct) => _result.SubscribeAsync(ct);
    public IAsyncEnumerable<Guid> SubscribeCancelAsync(CancellationToken ct) => _cancel.SubscribeAsync(ct);
    public IAsyncEnumerable<WorkerHeartbeat> SubscribeHeartbeatAsync(CancellationToken ct) => _heartbeat.SubscribeAsync(ct);

    private sealed class Broadcaster<T>
    {
        private readonly ConcurrentBag<Channel<T>> _subscribers = new();

        public async Task PublishAsync(T item)
        {
            foreach (var sub in _subscribers)
            {
                if (!sub.Writer.TryWrite(item))
                {
                    await sub.Writer.WriteAsync(item).ConfigureAwait(false);
                }
            }
        }

        public async IAsyncEnumerable<T> SubscribeAsync([EnumeratorCancellation] CancellationToken ct)
        {
            var ch = Channel.CreateUnbounded<T>();
            _subscribers.Add(ch);
            try
            {
                await foreach (var item in ch.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    yield return item;
                }
            }
            finally
            {
                ch.Writer.TryComplete();
            }
        }
    }
}
