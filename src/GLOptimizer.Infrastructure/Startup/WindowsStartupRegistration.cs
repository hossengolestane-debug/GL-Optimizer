using System.Runtime.Versioning;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Infrastructure.Startup;

public sealed class WindowsStartupRegistration : IStartupRegistration
{
    public bool IsSupported => OperatingSystem.IsWindows();

    public OperationResult<string?> ReadCommand(string valueName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult<string?>.Failure("Start with Windows is only available on Windows.");
        }

        try
        {
            return OperationResult<string?>.Success(ReadOnWindows(valueName));
        }
        catch (Exception)
        {
            return OperationResult<string?>.Failure("The startup registration could not be read.");
        }
    }

    public OperationResult SetCommand(string valueName, string command)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("Start with Windows is only available on Windows.");
        }

        if (!string.Equals(valueName, StartupRegistrationRules.ValueName, StringComparison.Ordinal))
        {
            return OperationResult.Failure("Only the GL Optimizer startup value can be written.");
        }

        try
        {
            SetOnWindows(valueName, command);
            return OperationResult.Success();
        }
        catch (Exception)
        {
            return OperationResult.Failure("The startup registration could not be written.");
        }
    }

    public OperationResult Remove(string valueName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Success();
        }

        if (!string.Equals(valueName, StartupRegistrationRules.ValueName, StringComparison.Ordinal))
        {
            return OperationResult.Failure("Only the GL Optimizer startup value can be removed.");
        }

        try
        {
            RemoveOnWindows(valueName);
            return OperationResult.Success();
        }
        catch (Exception)
        {
            return OperationResult.Failure("The startup registration could not be removed.");
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadOnWindows(string valueName)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: false);
        return key?.GetValue(valueName) as string;
    }

    [SupportedOSPlatform("windows")]
    private static void SetOnWindows(string valueName, string command)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.SetValue(valueName, command);
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveOnWindows(string valueName)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
