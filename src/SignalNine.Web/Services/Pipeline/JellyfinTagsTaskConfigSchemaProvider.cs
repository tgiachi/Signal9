using SignalNine.Core.Data.Config.Schema;
using SignalNine.Web.Interfaces;

namespace SignalNine.Web.Services.Pipeline;

public class JellyfinTagsTaskConfigSchemaProvider : IPipelineTaskConfigSchemaProvider
{
    public string TaskName => "JellyfinTags";

    public string DisplayName => "Jellyfin tags";

    public int Order => 75;

    public ConfigSchemaNode CreateSchema()
        => new()
        {
            Type = "object",
            Title = DisplayName,
            Description = "Assigns Jellyfin genres and tags to movie and TV show media.",
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
                    Title = "Jellyfin tags enabled",
                    Default = true,
                    Ui = new ConfigSchemaUiMetadata
                    {
                        Order = 100
                    }
                }
            }
        };
}
