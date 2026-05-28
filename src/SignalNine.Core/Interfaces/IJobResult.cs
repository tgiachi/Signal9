namespace SignalNine.Core.Interfaces;

/// <summary>
/// Marker interface for typed job results. Each handler returns a concrete IJobResult
/// (or EmptyJobResult) which is serialized into JobResultEvent.ResultJson and dispatched
/// on the web side to the matching IJobResultProcessor.
/// </summary>
public interface IJobResult
{
    /// <summary>Discriminator matching the job type (e.g. "media.pipeline").</summary>
    string Type { get; }
}
