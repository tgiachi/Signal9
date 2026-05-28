using SignalNine.Core.Data.Config.Schema;
using SignalNine.Web.Interfaces;

namespace SignalNine.Web.Services.Pipeline;

public class ProbeMediaTaskConfigSchemaProvider : IPipelineTaskConfigSchemaProvider
{
    public string TaskName => "Probe";

    public string DisplayName => "Probe";

    public int Order => 100;

    public ConfigSchemaNode CreateSchema()
        => new()
        {
            Type = "object",
            Title = DisplayName,
            Ui = new ConfigSchemaUiMetadata
            {
                Group = DisplayName,
                Order = Order
            },
            Properties = new Dictionary<string, ConfigSchemaNode>
            {
                ["Enabled"] = new()
                {
                    Type = "boolean",
                    Title = "Probe task enabled",
                    Default = true,
                    Ui = new ConfigSchemaUiMetadata
                    {
                        Order = 100
                    }
                },
                ["OverwriteExisting"] = new()
                {
                    Type = "boolean",
                    Title = "Overwrite existing probe",
                    Default = false,
                    Description = "When enabled, probe metadata is refreshed even if duration already exists.",
                    Ui = new ConfigSchemaUiMetadata
                    {
                        Order = 110
                    }
                },
                ["AllowJellyfinStreamProbe"] = new()
                {
                    Type = "boolean",
                    Title = "Probe Jellyfin streams",
                    Default = false,
                    Description = "When enabled, missing Jellyfin duration metadata is probed through the remote stream URL.",
                    Ui = new ConfigSchemaUiMetadata
                    {
                        Order = 120
                    }
                }
            }
        };
}
