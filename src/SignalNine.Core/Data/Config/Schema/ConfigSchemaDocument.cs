using System.Text.Json.Serialization;

namespace SignalNine.Core.Data.Config.Schema;

public class ConfigSchemaDocument
{
    private const string JsonSchemaUri = "https://json-schema.org/draft/2020-12/schema";
    private const string SchemaId = "https://signalnine.local/schemas/config.json";

    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = JsonSchemaUri;

    [JsonPropertyName("$id")]
    public string Id { get; set; } = SchemaId;

    [JsonPropertyName("title")]
    public string Title { get; set; } = "SignalNine configuration";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, ConfigSchemaNode> Properties { get; set; } = new();
}
