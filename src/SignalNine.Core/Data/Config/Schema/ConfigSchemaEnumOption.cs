using System.Text.Json.Serialization;

namespace SignalNine.Core.Data.Config.Schema;

public class ConfigSchemaEnumOption
{
    [JsonPropertyName("const")]
    public object Const { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
}
