using SignalNine.Core.Data.Config.Schema;
using SignalNine.Jobs.Interfaces;

namespace SignalNine.Jobs.Services.Pipeline;

public class TagMediaTaskConfigSchemaProvider : IPipelineTaskConfigSchemaProvider
{
    public string TaskName => "Tagger";

    public string DisplayName => "Tagger";

    public int Order => 50;

    public ConfigSchemaNode CreateSchema()
        => new()
        {
            Type = "object",
            Title = DisplayName,
            Description = "Assigns standard tags from the media type before metadata processing.",
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
                    Title = "Tagger task enabled",
                    Default = true,
                    Ui = new ConfigSchemaUiMetadata
                    {
                        Order = 100
                    }
                }
            }
        };
}
