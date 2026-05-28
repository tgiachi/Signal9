using SignalNine.Core.Data.Jobs;

namespace SignalNine.Core.Services;

public sealed class JobTypeRouter
{
    private static readonly HashSet<string> InternalTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "library.scan"
    };

    public JobStreamTarget ResolveTarget(string jobType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobType);
        return InternalTypes.Contains(jobType) ? JobStreamTarget.Internal : JobStreamTarget.Workers;
    }
}
