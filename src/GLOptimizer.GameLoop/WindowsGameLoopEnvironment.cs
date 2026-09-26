using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using GLOptimizer.Core.Detection;

namespace GLOptimizer.GameLoop;

public sealed class WindowsGameLoopEnvironment : IGameLoopEnvironment
{
    private const int ShortcutCap = 400;

    public IReadOnlyList<UninstallHint> ReadUninstallHints()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        return ReadUninstallHintsOnWindows();
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<UninstallHint> ReadUninstallHintsOnWindows()
    {
        var hints = new List<UninstallHint>();
        foreach (var hive in new[] { Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
            {
                try
                {
                    using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, view);
                    ReadUninstallKey(baseKey, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", hints);
                    ReadUninstallKey(baseKey, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", hints);
                }
                catch (Exception)
                {
                    // This hive or view is unavailable. Other locations can still match.
                }
            }
        }

        return hints;
    }

    public IReadOnlyList<string> CandidateDirectories()
    {
        var candidates = new List<string>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                {
                    continue;
                }

                foreach (var relative in GameLoopLayout.RelativeRoots)
                {
                    candidates.Add(Path.Combine(drive.RootDirectory.FullName, relative));
                }
            }
        }
        catch (Exception)
        {
            // Drive enumeration is optional. Registry, processes, and known folders still run.
        }

        AddSpecial(candidates, Environment.SpecialFolder.ProgramFiles, "TxGameAssistant");
        AddSpecial(candidates, Environment.SpecialFolder.ProgramFiles, "GameLoop");
        AddSpecial(candidates, Environment.SpecialFolder.ProgramFilesX86, "TxGameAssistant");
        AddSpecial(candidates, Environment.SpecialFolder.ProgramFilesX86, "GameLoop");
        AddSpecial(candidates, Environment.SpecialFolder.LocalApplicationData, "TxGameAssistant");
        AddSpecial(candidates, Environment.SpecialFolder.LocalApplicationData, "GameLoop");
        AddSpecial(candidates, Environment.SpecialFolder.CommonApplicationData, "TxGameAssistant");
        return candidates;
    }

    public IReadOnlyList<string> StartMenuTargets()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        return StartMenuTargetsOnWindows();
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> StartMenuTargetsOnWindows()
    {
        var targets = new List<string>();
        var examined = 0;
        foreach (var folder in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                     Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
                 })
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                continue;
            }

            IEnumerable<string> shortcuts;
            try
            {
                shortcuts = Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var shortcut in shortcuts)
            {
                examined++;
                if (examined > ShortcutCap)
                {
                    return targets;
                }

                var name = Path.GetFileNameWithoutExtension(shortcut);
                if (!GameLoopNames.IsProduct(name))
                {
                    continue;
                }

                var target = ResolveShortcut(shortcut);
                if (!string.IsNullOrWhiteSpace(target))
                {
                    targets.Add(target);
                }
            }
        }

        return targets;
    }

    public IReadOnlyList<string> MobileDataRoots()
    {
        var roots = new List<string>();
        AddSpecial(roots, Environment.SpecialFolder.LocalApplicationData, "Tencent");
        AddSpecial(roots, Environment.SpecialFolder.ApplicationData, "Tencent");
        AddSpecial(roots, Environment.SpecialFolder.MyDocuments, "GameLoop");
        AddSpecial(roots, Environment.SpecialFolder.MyDocuments, "Tencent");
        return roots;
    }

    public ProcessQueryResult QueryProcesses()
    {
        var processes = new List<ProcessObservation>();
        var unreadable = false;
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    string name;
                    try
                    {
                        name = process.ProcessName;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (!GameLoopNames.IsProcess(name))
                    {
                        continue;
                    }

                    string? path = null;
                    try
                    {
                        path = process.MainModule?.FileName;
                    }
                    catch (Exception)
                    {
                        unreadable = true;
                    }

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        unreadable = true;
                        continue;
                    }

                    processes.Add(new ProcessObservation
                    {
                        ProcessId = process.Id,
                        ProcessName = name,
                        ExecutablePath = path
                    });
                }
            }
        }
        catch (Exception)
        {
            return new ProcessQueryResult { Available = false };
        }

        return new ProcessQueryResult
        {
            Available = true,
            HadUnreadableMatch = unreadable,
            Processes = processes
        };
    }

    public bool FileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool DirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public DirectorySearchResult FindDirectoriesNamed(string root, IReadOnlyCollection<string> names, int maxDepth, int maxNodes, CancellationToken cancellationToken) =>
        DirectoryWalker.FindNamed(root, names, maxDepth, maxNodes, cancellationToken);

    public string? ReadSmallText(string path, int maxBytes)
    {
        try
        {
            if (maxBytes <= 0 || !File.Exists(path))
            {
                return null;
            }

            var info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > maxBytes)
            {
                return null;
            }

            return File.ReadAllText(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public string? TryReadFileVersion(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var info = FileVersionInfo.GetVersionInfo(path);
            return VersionText.Normalize(info.ProductVersion) ?? VersionText.Normalize(info.FileVersion);
        }
        catch (Exception)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ReadUninstallKey(Microsoft.Win32.RegistryKey baseKey, string subKey, List<UninstallHint> hints)
    {
        using var uninstall = baseKey.OpenSubKey(subKey);
        if (uninstall is null)
        {
            return;
        }

        foreach (var childName in uninstall.GetSubKeyNames())
        {
            try
            {
                using var child = uninstall.OpenSubKey(childName);
                if (child is null)
                {
                    continue;
                }

                var displayName = child.GetValue("DisplayName") as string;
                if (!GameLoopNames.IsProduct(displayName))
                {
                    continue;
                }

                hints.Add(new UninstallHint
                {
                    DisplayName = displayName,
                    InstallLocation = child.GetValue("InstallLocation") as string,
                    DisplayIcon = child.GetValue("DisplayIcon") as string,
                    DisplayVersion = child.GetValue("DisplayVersion") as string
                });
            }
            catch (Exception)
            {
                // Skip an unreadable uninstall entry.
            }
        }
    }

    private static void AddSpecial(List<string> target, Environment.SpecialFolder folder, string child)
    {
        try
        {
            var root = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(root))
            {
                target.Add(Path.Combine(root, child));
            }
        }
        catch (Exception)
        {
            // The special folder is unavailable on this host.
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? ResolveShortcut(string shortcutPath)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null)
            {
                return null;
            }

            var shell = Activator.CreateInstance(type);
            if (shell is null)
            {
                return null;
            }

            var shortcut = type.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                [shortcutPath]);
            if (shortcut is null)
            {
                return null;
            }

            var target = shortcut.GetType().InvokeMember(
                "TargetPath",
                BindingFlags.GetProperty,
                null,
                shortcut,
                null) as string;
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
