using System.Collections.Concurrent;
using System.Diagnostics;
using SignalNine.Core.Interfaces;

namespace SignalNine.Tests.Core.Services.Ffmpeg;

internal sealed class FakeProcessLauncher : IProcessLauncher
{
    private int _nextPid = 1000;
    public ConcurrentQueue<FakeRunningProcess> Started { get; } = new();
    public Action<FakeRunningProcess>? OnStart { get; set; }

    public IRunningProcess Start(ProcessStartInfo psi)
    {
        var p = new FakeRunningProcess(_nextPid++, psi.FileName, psi.ArgumentList.ToList());
        Started.Enqueue(p);
        OnStart?.Invoke(p);
        return p;
    }
}

internal sealed class FakeRunningProcess : IRunningProcess
{
    private readonly TaskCompletionSource<int> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _lock = new();
    private readonly List<string> _pendingStdout = new();
    private readonly List<string> _pendingStderr = new();
    private EventHandler<string>? _stdoutHandler;
    private EventHandler<string>? _stderrHandler;
    private bool _disposed;

    public FakeRunningProcess(int pid, string fileName, IReadOnlyList<string> args)
    {
        Pid = pid;
        FileName = fileName;
        Args = args;
    }

    public int Pid { get; }
    public string FileName { get; }
    public IReadOnlyList<string> Args { get; }
    public bool Killed { get; private set; }
    public bool KilledTree { get; private set; }

    public event EventHandler<string>? StdoutLineReceived
    {
        add
        {
            List<string>? toReplay = null;
            lock (_lock)
            {
                _stdoutHandler += value;
                if (_pendingStdout.Count > 0)
                {
                    toReplay = new List<string>(_pendingStdout);
                    _pendingStdout.Clear();
                }
            }
            if (toReplay is not null)
            {
                foreach (var line in toReplay) value?.Invoke(this, line);
            }
        }
        remove
        {
            lock (_lock) _stdoutHandler -= value;
        }
    }

    public event EventHandler<string>? StderrLineReceived
    {
        add
        {
            List<string>? toReplay = null;
            lock (_lock)
            {
                _stderrHandler += value;
                if (_pendingStderr.Count > 0)
                {
                    toReplay = new List<string>(_pendingStderr);
                    _pendingStderr.Clear();
                }
            }
            if (toReplay is not null)
            {
                foreach (var line in toReplay) value?.Invoke(this, line);
            }
        }
        remove
        {
            lock (_lock) _stderrHandler -= value;
        }
    }

    public Task<int> WaitForExitAsync(CancellationToken ct)
    {
        return _exit.Task.WaitAsync(ct);
    }

    public void Kill(bool entireProcessTree)
    {
        Killed = true;
        if (entireProcessTree) KilledTree = true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    public void EmitStdout(string line)
    {
        EventHandler<string>? handler;
        lock (_lock)
        {
            handler = _stdoutHandler;
            if (handler is null)
            {
                _pendingStdout.Add(line);
                return;
            }
        }
        handler.Invoke(this, line);
    }

    public void EmitStderr(string line)
    {
        EventHandler<string>? handler;
        lock (_lock)
        {
            handler = _stderrHandler;
            if (handler is null)
            {
                _pendingStderr.Add(line);
                return;
            }
        }
        handler.Invoke(this, line);
    }

    public void Exit(int code)
    {
        _exit.TrySetResult(code);
    }
}
