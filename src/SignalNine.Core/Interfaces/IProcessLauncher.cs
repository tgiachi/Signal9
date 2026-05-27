using System.Diagnostics;

namespace SignalNine.Core.Interfaces;

public interface IProcessLauncher
{
    IRunningProcess Start(ProcessStartInfo psi);
}
