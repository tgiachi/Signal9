// src/SignalNine.Core/Services/Redis/RedisJobQueue.cs
using System.Text.Json;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using StackExchange.Redis;

namespace SignalNine.Core.Services.Redis;

public sealed class RedisJobQueue : IJobQueue
{
    private const string EnvelopeField = "envelope";
    private static readonly TimeSpan PullBlock = TimeSpan.FromSeconds(5);

    private readonly IConnectionMultiplexer _mux;
    private readonly RedisStreamKeys _keys;

    public RedisJobQueue(IConnectionMultiplexer mux, RedisStreamKeys keys)
    {
        _mux = mux;
        _keys = keys;
    }

    /// <summary>
    /// Lazy creation of consumer groups (idempotent — ignores BUSYGROUP).
    /// Call once at startup or before the first Pull.
    /// </summary>
    public async Task EnsureConsumerGroupsAsync()
    {
        var db = _mux.GetDatabase();
        foreach (var target in new[] { JobStreamTarget.Internal, JobStreamTarget.Workers, JobStreamTarget.Scheduled })
        {
            var stream = _keys.Stream(target);
            var group = _keys.ConsumerGroup(target);
            try
            {
                await db.StreamCreateConsumerGroupAsync(stream, group, "0-0", createStream: true);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
            {
                // already exists — ignore
            }
        }
    }

    public async Task PushAsync(JobEnvelope envelope, JobStreamTarget target, CancellationToken cancellationToken = default)
    {
        var db = _mux.GetDatabase();
        var json = JsonSerializer.Serialize(envelope);
        await db.StreamAddAsync(_keys.Stream(target),
            new[] { new NameValueEntry(EnvelopeField, json) });
    }

    public async Task<QueuedJob?> PullAsync(string consumerName, JobStreamTarget target, CancellationToken cancellationToken = default)
    {
        await EnsureConsumerGroupsAsync();
        var db = _mux.GetDatabase();
        var stream = _keys.Stream(target);
        var group = _keys.ConsumerGroup(target);

        // StackExchange.Redis doesn't expose XREADGROUP BLOCK directly; emulate via polling
        // with short waits. Acceptable because Phase 2 single-binary is not throughput-critical.
        var deadline = DateTimeOffset.UtcNow.Add(PullBlock);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entries = await db.StreamReadGroupAsync(stream, group, consumerName, ">", count: 1, noAck: false);
            if (entries is { Length: > 0 })
            {
                var entry = entries[0];
                var envelopeJson = entry.Values.First(v => v.Name == EnvelopeField).Value.ToString();
                var envelope = JsonSerializer.Deserialize<JobEnvelope>(envelopeJson)
                               ?? throw new InvalidOperationException("Failed to deserialize envelope");
                return new QueuedJob(entry.Id.ToString(), envelope);
            }
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }
            catch (OperationCanceledException) { return null; }
        }
        return null;
    }

    public async Task AckAsync(string streamId, JobStreamTarget target, CancellationToken cancellationToken = default)
    {
        var db = _mux.GetDatabase();
        await db.StreamAcknowledgeAsync(_keys.Stream(target), _keys.ConsumerGroup(target), streamId);
    }

    public async Task RequeueLaterAsync(JobEnvelope envelope, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        // Phase 2 simple impl: fire-and-forget Task.Delay then push to workers.
        // Phase 6 will replace with a real scheduled stream + dispatcher.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cancellationToken);
                await PushAsync(envelope, JobStreamTarget.Workers, cancellationToken);
            }
            catch (OperationCanceledException) { }
        }, CancellationToken.None);
    }
}
