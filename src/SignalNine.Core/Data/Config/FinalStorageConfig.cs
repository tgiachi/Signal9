namespace SignalNine.Core.Data.Config;

public class FinalStorageConfig
{
    /// <summary>"filesystem" (today) or "minio"/"s3" (future).</summary>
    public string Type { get; set; } = "filesystem";

    /// <summary>For filesystem type: root path under which previews live.</summary>
    public string Path { get; set; } = "";
}
