using SignalNine.Core.Data.Config.Schema;
using SignalNine.Core.Types;
using SignalNine.Jobs.Interfaces;

namespace SignalNine.Web.Services.Config;

public class ConfigSchemaService
{
    private readonly IEnumerable<IPipelineTaskConfigSchemaProvider> _pipelineTaskSchemas;

    public ConfigSchemaService(IEnumerable<IPipelineTaskConfigSchemaProvider> pipelineTaskSchemas)
    {
        ArgumentNullException.ThrowIfNull(pipelineTaskSchemas);

        _pipelineTaskSchemas = pipelineTaskSchemas;
    }

    public ConfigSchemaDocument CreateSchema()
    {
        var document = new ConfigSchemaDocument();
        AddRuntimeSchema(document);
        AddJwtSchema(document);
        AddJobSystemSchema(document);
        AddFfmpegPoolSchema(document);
        AddPipelineSchema(document);

        return document;
    }

    private void AddPipelineSchema(ConfigSchemaDocument document)
    {
        var tasks = ObjectNode("Pipeline tasks");
        var taskProperties = tasks.Properties ?? new Dictionary<string, ConfigSchemaNode>();
        tasks.Properties = taskProperties;

        foreach (var provider in _pipelineTaskSchemas.OrderBy(provider => provider.Order))
        {
            taskProperties[provider.TaskName] = provider.CreateSchema();
        }

        document.Properties["Pipeline"] = ObjectNode(
            "Media pipeline",
            Ui("pipeline", "Media pipeline", 500),
            new Dictionary<string, ConfigSchemaNode>
            {
                ["Tasks"] = tasks
            }
        );
    }

    private static void AddRuntimeSchema(ConfigSchemaDocument document)
    {
        document.Properties["LogLevel"] = SelectNode(
            "Log level",
            (int)LogLevelType.Information,
            new[]
            {
                Option((int)LogLevelType.Trace, "Trace"),
                Option((int)LogLevelType.Debug, "Debug"),
                Option((int)LogLevelType.Information, "Information"),
                Option((int)LogLevelType.Warning, "Warning"),
                Option((int)LogLevelType.Error, "Error"),
                Option((int)LogLevelType.Critical, "Critical")
            },
            Ui("runtime", "Runtime", 100)
        );
        document.Properties["LogToFile"] = BooleanNode("Log to file", true, Ui("runtime", "Runtime", 110));
        document.Properties["DatabaseType"] = SelectNode(
            "Database type",
            (int)DatabaseType.Sqlite,
            new[]
            {
                Option((int)DatabaseType.Sqlite, "Sqlite"),
                Option((int)DatabaseType.PostgreSql, "PostgreSQL")
            },
            Ui("runtime", "Runtime", 120),
            "Storage backend used by FreeSql."
        );
        document.Properties["DatabaseUrl"] = StringNode(
            "Database URL",
            "sqlite://{ROOT_DIRECTORY}/db/signalnine.db",
            Ui("runtime", "Runtime", 130)
        );
    }

    private static void AddJwtSchema(ConfigSchemaDocument document)
    {
        document.Properties["Jwt"] = ObjectNode(
            "JWT",
            Ui("jwt", "JWT", 200),
            new Dictionary<string, ConfigSchemaNode>
            {
                ["Issuer"] = StringNode("Issuer", "SignalNine", Ui(order: 100)),
                ["Audience"] = StringNode("Audience", "SignalNine", Ui(order: 110)),
                ["Secret"] = StringNode(
                    "Secret",
                    "signalnine-development-secret-change-before-production",
                    Ui(order: 120, widget: "password", secret: true)
                ),
                ["ExpirationMinutes"] = IntegerNode("Expiration (minutes)", 60, Ui(order: 130), minimum: 1)
            }
        );
    }

    private static void AddJobSystemSchema(ConfigSchemaDocument document)
    {
        document.Properties["JobSystem"] = ObjectNode(
            "Job system",
            Ui("jobs", "Job system", 300),
            new Dictionary<string, ConfigSchemaNode>
            {
                ["MaxConcurrentJobs"] = IntegerNode(
                    "Max concurrent jobs",
                    2,
                    Ui(order: 100),
                    "Controls how many queued jobs may run in parallel.",
                    1
                ),
                ["MaxLogEntriesPerJob"] = IntegerNode("Max log entries per job", 500, Ui(order: 110), minimum: 1)
            }
        );
    }

    private static void AddFfmpegPoolSchema(ConfigSchemaDocument document)
    {
        document.Properties["FfmpegPool"] = ObjectNode(
            "FFmpeg pool",
            Ui("ffmpeg", "FFmpeg pool", 400),
            new Dictionary<string, ConfigSchemaNode>
            {
                ["MaxConcurrent"] = IntegerNode(
                    "Max concurrent processes",
                    2,
                    Ui(order: 100),
                    "Controls how many FFmpeg or FFprobe processes can run at once.",
                    1
                ),
                ["FfmpegPath"] = StringNode("FFmpeg path", "ffmpeg", Ui(order: 110)),
                ["FfprobePath"] = StringNode("FFprobe path", "ffprobe", Ui(order: 120)),
                ["KillGraceSeconds"] = IntegerNode("Kill grace seconds", 5, Ui(order: 130), minimum: 1),
                ["OutputBufferLines"] = IntegerNode("Output buffer lines", 200, Ui(order: 140), minimum: 1),
                ["RegistryRetention"] = IntegerNode("Registry retention", 100, Ui(order: 150), minimum: 1)
            }
        );
    }

    private static ConfigSchemaNode ObjectNode(
        string title,
        ConfigSchemaUiMetadata? ui = null,
        Dictionary<string, ConfigSchemaNode>? properties = null
    )
        => new()
        {
            Type = "object",
            Title = title,
            Ui = ui,
            Properties = properties ?? new Dictionary<string, ConfigSchemaNode>()
        };

    private static ConfigSchemaNode StringNode(string title, string defaultValue, ConfigSchemaUiMetadata ui)
        => new()
        {
            Type = "string",
            Title = title,
            Default = defaultValue,
            Ui = ui
        };

    private static ConfigSchemaNode BooleanNode(string title, bool defaultValue, ConfigSchemaUiMetadata ui)
        => new()
        {
            Type = "boolean",
            Title = title,
            Default = defaultValue,
            Ui = ui
        };

    private static ConfigSchemaNode IntegerNode(
        string title,
        int defaultValue,
        ConfigSchemaUiMetadata ui,
        string? description = null,
        decimal? minimum = null,
        decimal? maximum = null
    )
        => new()
        {
            Type = "integer",
            Title = title,
            Description = description,
            Default = defaultValue,
            Minimum = minimum,
            Maximum = maximum,
            Ui = ui
        };

    private static ConfigSchemaNode SelectNode(
        string title,
        int defaultValue,
        IReadOnlyList<ConfigSchemaEnumOption> options,
        ConfigSchemaUiMetadata ui,
        string? description = null
    )
        => new()
        {
            Type = "integer",
            Title = title,
            Description = description,
            Default = defaultValue,
            OneOf = options,
            Ui = ui
        };

    private static ConfigSchemaEnumOption Option(int value, string title)
        => new()
        {
            Const = value,
            Title = title
        };

    private static ConfigSchemaUiMetadata Ui(
        string? section = null,
        string? sectionTitle = null,
        int? order = null,
        string? widget = null,
        bool? secret = null
    )
        => new()
        {
            Section = section,
            SectionTitle = sectionTitle,
            Order = order,
            Widget = widget,
            Secret = secret
        };
}
