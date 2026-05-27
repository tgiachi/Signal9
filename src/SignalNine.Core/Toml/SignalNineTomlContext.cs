using SignalNine.Core.Data.Config;
using Tomlyn.Serialization;

namespace SignalNine.Core.Toml;

[TomlSourceGenerationOptions(WriteIndented = true)]
[TomlSerializable(typeof(FfmpegPoolConfig))]
[TomlSerializable(typeof(JobSystemConfig))]
[TomlSerializable(typeof(JwtConfig))]
[TomlSerializable(typeof(PipelineConfig))]
[TomlSerializable(typeof(SignalNineConfig))]
internal sealed partial class SignalNineTomlContext : TomlSerializerContext
{
}
