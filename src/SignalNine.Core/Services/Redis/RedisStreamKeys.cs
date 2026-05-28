using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;

namespace SignalNine.Core.Services.Redis;

/// <summary>
/// Builds Redis stream/channel keys based on the configured prefix.
/// Centralized so we never sprinkle string literals across the codebase.
/// </summary>
public sealed class RedisStreamKeys
{
    private readonly string _prefix;

    public RedisStreamKeys(RedisConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _prefix = string.IsNullOrEmpty(config.KeyPrefix) ? "signal9:" : config.KeyPrefix;
    }

    public string Stream(JobStreamTarget target) => target switch
    {
        JobStreamTarget.Internal  => $"{_prefix}jobs:internal",
        JobStreamTarget.Workers   => $"{_prefix}jobs:workers",
        JobStreamTarget.Scheduled => $"{_prefix}jobs:scheduled",
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
    };

    public string ConsumerGroup(JobStreamTarget target) => target switch
    {
        JobStreamTarget.Internal => "web",
        JobStreamTarget.Workers  => "workers",
        JobStreamTarget.Scheduled => "scheduler",
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
    };

    public string ChannelProgress => $"{_prefix}job:progress";
    public string ChannelLog => $"{_prefix}job:log";
    public string ChannelResult => $"{_prefix}job:result";
    public string ChannelCancel => $"{_prefix}job:cancel";
    public string ChannelHeartbeat => $"{_prefix}worker:heartbeat";
}
