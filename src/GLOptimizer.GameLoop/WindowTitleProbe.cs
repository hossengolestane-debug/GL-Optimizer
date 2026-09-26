using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Results;

namespace GLOptimizer.GameLoop;

public sealed class WindowTitleProbe : IWindowTitleSource
{
    public OperationResult<IReadOnlyList<WindowTitle>> List()
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult<IReadOnlyList<WindowTitle>>.NotImplemented("Window title enumeration");
        }

        return ListOnWindows();
    }

    [SupportedOSPlatform("windows")]
    private static OperationResult<IReadOnlyList<WindowTitle>> ListOnWindows()
    {
        try
        {
            var found = new List<WindowTitle>();
            EnumWindows((handle, unused) =>
            {
                var length = GetWindowTextLength(handle);
                if (length <= 0)
                {
                    return true;
                }

                var buffer = new char[length + 1];
                GetWindowText(handle, buffer, buffer.Length);
                GetWindowThreadProcessId(handle, out var processId);
                var title = new string(buffer).TrimEnd('\0').Trim();
                if (title.Length > 0 && processId != 0)
                {
                    found.Add(new WindowTitle
                    {
                        ProcessId = (int)processId,
                        Title = title
                    });
                }

                return true;
            }, IntPtr.Zero);
            return OperationResult<IReadOnlyList<WindowTitle>>.Success(found);
        }
        catch (Exception)
        {
            return OperationResult<IReadOnlyList<WindowTitle>>.Failure("Window titles could not be read.");
        }
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
