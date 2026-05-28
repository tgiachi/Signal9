using SignalNine.Core.Data.Config.Schema;
using SignalNine.Web.Interfaces;

namespace SignalNine.Web.Services.Pipeline;

public class JellyfinPreviewTaskConfigSchemaProvider : IPipelineTaskConfigSchemaProvider
{
    public string TaskName => "JellyfinPreview";

    public string DisplayName => "Jellyfin preview";

    public int Order => 150;

    public ConfigSchemaNode CreateSchema()
        => new()
        {
            Type = "object",
            Title = DisplayName,
            Description = "Downloads Jellyfin item images into local preview thumbnails.",
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
                    Title = "Jellyfin preview enabled",
                    Default = true,
                    Ui = new ConfigSchemaUiMetadata
                    {
                        Order = 100
                    }
                },
                ["OverwriteExisting"] = new()
                {
                    Type = "boolean",
                    Title = "Overwrite Jellyfin previews",
                    Default = false,
                    Description = "When enabled, existing local preview files are replaced with Jellyfin images.",
                    Ui = new ConfigSchemaUiMetadata
                    {
                        Order = 110
                    }
                },
                ["MaxImages"] = new()
                {
                    Type = "integer",
                    Title = "Max Jellyfin images",
                    Default = 3,
                    Minimum = 1,
                    Maximum = 5,
                    Description = "Maximum number of Jellyfin images saved as local preview thumbnails.",
                    Ui = new ConfigSchemaUiMetadata
                    {
                        Order = 120
                    }
                }
            }
        };
}
