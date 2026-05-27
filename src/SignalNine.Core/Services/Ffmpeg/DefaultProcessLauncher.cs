using System.ComponentModel;
using System.Diagnostics;
using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Interfaces;

namespace SignalNine.Core.Services.Ffmpeg;

public class DefaultProcessLauncher : IProcessLauncher
{
    public IRunningProcess Start(ProcessStartInfo psi)
    {
        ArgumentNullException.ThrowIfNull(psi);

        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

        Process process;
        try
        {
            process = Process.Start(psi) ?? throw new InvalidOperationException("Process.Start returned null.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            throw new FfmpegNotFoundException(psi.FileName);
        }

        return new DefaultRunningProcess(process);
    }

    private sealed class DefaultRunningProcess : IRunningProcess
    {
        private readonly Process _process;
        private bool _disposed;

        public DefaultRunningProcess(Process process)
        {
            _process = process;
            _process.OutputDataReceived += OnStdout;
            _process.ErrorDataReceived += OnStderr;
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public int Pid
        {
            get { return _process.Id; }
        }

        public event EventHandler<string>? StdoutLineReceived;
        public event EventHandler<string>? StderrLineReceived;

        public async Task<int> WaitForExitAsync(CancellationToken ct)
        {
            await _process.WaitForExitAsync(ct).ConfigureAwait(false);
            return _process.ExitCode;
        }

        public void Kill(bool entireProcessTree)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree);
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited; benign.
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _process.OutputDataReceived -= OnStdout;
            _process.ErrorDataReceived -= OnStderr;
            _process.Dispose();
            _disposed = true;
        }

        private void OnStdout(object? sender, DataReceivedEventArgs e)
        {
            if (e.Data is not null) StdoutLineReceived?.Invoke(this, e.Data);
        }

        private void OnStderr(object? sender, DataReceivedEventArgs e)
        {
            if (e.Data is not null) StderrLineReceived?.Invoke(this, e.Data);
        }
    }
}
