namespace SignalNine.Worker.Services;

public sealed record WorkerIdentity(Guid Id, string Name)
{
    public static WorkerIdentity LoadOrCreate(string stateFilePath, string? name, Guid? explicitId = null)
    {
        Guid id;
        if (explicitId is { } given)
        {
            id = given;
        }
        else if (File.Exists(stateFilePath) && Guid.TryParse(File.ReadAllText(stateFilePath).Trim(), out var existing))
        {
            id = existing;
        }
        else
        {
            id = Guid.NewGuid();
            var dir = Path.GetDirectoryName(stateFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }

        // Best-effort persistence (also for explicit, so the next call without env still uses same id)
        try { File.WriteAllText(stateFilePath, id.ToString()); } catch { /* tolerate read-only fs */ }

        var resolvedName = string.IsNullOrWhiteSpace(name) ? Environment.MachineName : name;
        return new WorkerIdentity(id, resolvedName);
    }
}
