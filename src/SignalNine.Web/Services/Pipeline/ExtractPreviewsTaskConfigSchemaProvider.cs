using SignalNine.Core.Data.Config.Schema;
using SignalNine.Web.Interfaces;

namespace SignalNine.Web.Services.Pipeline;

public class ExtractPreviewsTaskConfigSchemaProvider : IPipelineTaskConfigSchemaProvider
{
    public string TaskName => "Preview";

    public string DisplayName => "Preview";

    public int Order => 200;

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
                    Title = "Preview task enabled",
                    Default = true,
                    Ui = new ConfigSchemaUiMetadata
                    {
                        Order = 100
                    }
                },
                ["OverwriteExisting"] = new()
                {
                    Type = "boolean",
                    Title = "Overwrite existing previews",
                    Default = false,
                    Description = "When enabled, existing preview thumbnails are regenerated.",
                    Ui = new ConfigSchemaUiMetadata
                    {
                        Order = 110
                    }
                },
                ["AllowJellyfinStreamFallback"] = new()
                {
                    Type = "boolean",
                    Title = "Use FFmpeg for Jellyfin previews",
                    Default = false,
                    Description = "When enabled, Jellyfin items without downloaded images can fall back to FFmpeg stream extraction.",
                    Ui = new ConfigSchemaUiMetadata
                    {
                        Order = 120
                    }
                },
                ["PreviewCount"] = new()
                {
                    Type = "integer",
                    Title = "Preview count",
                    Default = 5,
                    Minimum = 1,
                    Maximum = 20,
                    Description = "Number of thumbnails extracted for each media item.",
                    Ui = new ConfigSchemaUiMetadata
                    {
                        Order = 130
                    }
                }
            }
        };
}
