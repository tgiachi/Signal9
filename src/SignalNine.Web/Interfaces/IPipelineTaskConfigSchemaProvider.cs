using SignalNine.Core.Data.Config.Schema;

namespace SignalNine.Web.Interfaces;

/// <summary>
/// Provides the configuration schema for a single media pipeline task.
/// </summary>
public interface IPipelineTaskConfigSchemaProvider
{
    /// <summary>
    /// Gets the configuration key used under <c>Pipeline.Tasks</c>.
    /// </summary>
    string TaskName { get; }

    /// <summary>
    /// Gets the display name shown by generated configuration UIs.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the task ordering value used for schema ordering.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Creates the JSON Schema node for this task's configuration.
    /// </summary>
    ConfigSchemaNode CreateSchema();
}
