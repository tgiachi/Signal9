namespace SignalNine.Core.Interfaces;

public interface IRunningProcess : IDisposable
{
    int Pid { get; }

    Task<int> WaitForExitAsync(CancellationToken ct);

    event EventHandler<string> StdoutLineReceived;
    event EventHandler<string> StderrLineReceived;

    void Kill(bool entireProcessTree);
}
