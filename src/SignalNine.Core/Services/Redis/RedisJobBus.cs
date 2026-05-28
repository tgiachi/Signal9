// src/SignalNine.Core/Services/Redis/RedisJobBus.cs
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using StackExchange.Redis;

namespace SignalNine.Core.Services.Redis;

public sealed class RedisJobBus : IJobBus
{
    private readonly IConnectionMultiplexer _mux;
    private readonly RedisStreamKeys _keys;

    public RedisJobBus(IConnectionMultiplexer mux, RedisStreamKeys keys)
    {
        _mux = mux;
        _keys = keys;
    }

    public Task PublishProgressAsync(JobProgressEvent e, CancellationToken ct = default)
        => PublishAsync(_keys.ChannelProgress, e);
    public Task PublishLogAsync(JobLogEvent e, CancellationToken ct = default)
        => PublishAsync(_keys.ChannelLog, e);
    public Task PublishResultAsync(JobResultEvent e, CancellationToken ct = default)
        => PublishAsync(_keys.ChannelResult, e);
    public Task PublishCancelAsync(Guid jobId, CancellationToken ct = default)
        => PublishAsync(_keys.ChannelCancel, jobId);
    public Task PublishHeartbeatAsync(WorkerHeartbeat h, CancellationToken ct = default)
        => PublishAsync(_keys.ChannelHeartbeat, h);

    public IAsyncEnumerable<JobProgressEvent> SubscribeProgressAsync(CancellationToken ct)
        => SubscribeAsync<JobProgressEvent>(_keys.ChannelProgress, ct);
    public IAsyncEnumerable<JobLogEvent> SubscribeLogAsync(CancellationToken ct)
        => SubscribeAsync<JobLogEvent>(_keys.ChannelLog, ct);
    public IAsyncEnumerable<JobResultEvent> SubscribeResultAsync(CancellationToken ct)
        => SubscribeAsync<JobResultEvent>(_keys.ChannelResult, ct);
    public IAsyncEnumerable<Guid> SubscribeCancelAsync(CancellationToken ct)
        => SubscribeAsync<Guid>(_keys.ChannelCancel, ct);
    public IAsyncEnumerable<WorkerHeartbeat> SubscribeHeartbeatAsync(CancellationToken ct)
        => SubscribeAsync<WorkerHeartbeat>(_keys.ChannelHeartbeat, ct);

    private async Task PublishAsync<T>(string channel, T payload)
    {
        var subscriber = _mux.GetSubscriber();
        var json = JsonSerializer.Serialize(payload);
        await subscriber.PublishAsync(RedisChannel.Literal(channel), json);
    }

    private async IAsyncEnumerable<T> SubscribeAsync<T>(string channel, [EnumeratorCancellation] CancellationToken ct)
    {
        var subscriber = _mux.GetSubscriber();
        var queue = await subscriber.SubscribeAsync(RedisChannel.Literal(channel));
        var ch = Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true });

        queue.OnMessage(msg =>
        {
            var json = msg.Message.ToString();
            try
            {
                var payload = JsonSerializer.Deserialize<T>(json);
                if (payload is not null) ch.Writer.TryWrite(payload);
            }
            catch (JsonException) { /* drop malformed message */ }
        });

        try
        {
            await foreach (var item in ch.Reader.ReadAllAsync(ct))
            {
                yield return item;
            }
        }
        finally
        {
            await subscriber.UnsubscribeAsync(RedisChannel.Literal(channel));
            ch.Writer.TryComplete();
        }
    }
}
