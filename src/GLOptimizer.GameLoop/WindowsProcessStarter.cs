using System.Diagnostics;

namespace GLOptimizer.GameLoop;

public sealed class WindowsProcessStarter : IProcessStarter
{
    public void Start(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        Process.Start(new ProcessStartInfo(executablePath) { UseShellExecute = true });
    }
}
