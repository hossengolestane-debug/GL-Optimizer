using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.Versioning;
using GLOptimizer.Core.Detection;

namespace GLOptimizer.GameLoop;

public sealed class WindowsGameLoopEnvironment : IGameLoopEnvironment
{
    private const int ShortcutCap = 400;
    private readonly IInstallOverrideSource? _override;

    public WindowsGameLoopEnvironment()
        : this(null)
    {
    }

    public WindowsGameLoopEnvironment(IInstallOverrideSource? overrides)
    {
        _override = overrides;
    }

    public IReadOnlyList<UninstallHint> ReadUninstallHints() => ReadUninstall().Hints;

    public UninstallRead ReadUninstall()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new UninstallRead([], []);
        }

        return ReadUninstallOnWindows();
    }

    public IReadOnlyList<ProductRegistration> ReadProductRegistrations()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        return ReadProductRegistrationsOnWindows();
    }

    public IReadOnlyList<string> MarketDirectories()
    {
        var roots = new List<string>();
        AddSpecial(roots, Environment.SpecialFolder.ApplicationData, Path.Combine("Tencent", "MobileGamePC"));
        return roots;
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<ProductRegistration> ReadProductRegistrationsOnWindows()
    {
        var registrations = new List<ProductRegistration>();
        foreach (var hive in new[] { Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
            {
                ReadProductKey(hive, view, @"SOFTWARE\Tencent\GameLoop", registrations);
                ReadProductKey(hive, view, @"SOFTWARE\WOW6432Node\Tencent\GameLoop", registrations);
            }
        }

        return registrations;
    }

    [SupportedOSPlatform("windows")]
    private static void ReadProductKey(
        Microsoft.Win32.RegistryHive hive,
        Microsoft.Win32.RegistryView view,
        string subKey,
        List<ProductRegistration> registrations)
    {
        var location = HiveLabel(hive) + "\\" + subKey + " (" + ViewLabel(view) + ")";
        try
        {
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(subKey);
            if (key is null)
            {
                registrations.Add(new ProductRegistration { Location = location, Found = false });
                return;
            }

            registrations.Add(new ProductRegistration
            {
                Location = location,
                Found = true,
                InstallPath = ReadString(key, "InstallPath"),
                DataPath = ReadString(key, "GameLoopData"),
                Version = ReadString(key, "Version")
            });
        }
        catch (Exception)
        {
            registrations.Add(new ProductRegistration
            {
                Location = location,
                Found = false,
                Detail = "could not be read"
            });
        }
    }

    [SupportedOSPlatform("windows")]
    private static UninstallRead ReadUninstallOnWindows()
    {
        var hints = new List<UninstallHint>();
        var attempts = new List<RegistryAttempt>();
        foreach (var hive in new[] { Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
            {
                ReadUninstallKey(hive, view, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", hints, attempts);
                ReadUninstallKey(hive, view, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", hints, attempts);
            }
        }

        return new UninstallRead(hints, attempts);
    }

    public IReadOnlyList<string> CandidateDirectories()
    {
        var candidates = new List<string>();
        var preferred = InstallOverrideRules.Candidate(_override?.OverridePath);
        if (preferred is not null)
        {
            candidates.Add(preferred);
        }
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

        AddSpecial(candidates, Environment.SpecialFolder.ProgramFiles, Path.Combine("Tencent", "GameLoop"));
        AddSpecial(candidates, Environment.SpecialFolder.ProgramFiles, "TxGameAssistant");
        AddSpecial(candidates, Environment.SpecialFolder.ProgramFiles, "GameLoop");
        AddSpecial(candidates, Environment.SpecialFolder.ProgramFilesX86, Path.Combine("Tencent", "GameLoop"));
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
                        processes.Add(new ProcessObservation
                        {
                            ProcessId = process.Id,
                            ProcessName = name,
                            ExecutablePath = null
                        });
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
    private static void ReadUninstallKey(
        Microsoft.Win32.RegistryHive hive,
        Microsoft.Win32.RegistryView view,
        string subKey,
        List<UninstallHint> hints,
        List<RegistryAttempt> attempts)
    {
        var location = HiveLabel(hive) + "\\" + subKey + " (" + ViewLabel(view) + ")";
        try
        {
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(subKey);
            if (uninstall is null)
            {
                attempts.Add(new RegistryAttempt { Location = location, Found = false });
                return;
            }

            var matches = 0;
            foreach (var childName in uninstall.GetSubKeyNames())
            {
                try
                {
                    using var child = uninstall.OpenSubKey(childName);
                    if (child is null)
                    {
                        continue;
                    }

                    var displayName = ReadString(child, "DisplayName");
                    if (!GameLoopNames.IsProduct(displayName))
                    {
                        continue;
                    }

                    matches++;
                    hints.Add(new UninstallHint
                    {
                        DisplayName = displayName,
                        InstallLocation = ReadString(child, "InstallLocation"),
                        DisplayIcon = ReadString(child, "DisplayIcon"),
                        UninstallString = ReadString(child, "UninstallString"),
                        DisplayVersion = ReadString(child, "DisplayVersion"),
                        Source = HiveLabel(hive) + "\\" + subKey + "\\" + childName + " (" + ViewLabel(view) + ")"
                    });
                }
                catch (Exception)
                {
                    // Skip an unreadable uninstall entry.
                }
            }

            attempts.Add(new RegistryAttempt
            {
                Location = location,
                Found = true,
                Detail = matches == 0 ? "opened, no GameLoop entry" : matches.ToString(CultureInfo.InvariantCulture) + " GameLoop entry"
            });
        }
        catch (Exception)
        {
            attempts.Add(new RegistryAttempt
            {
                Location = location,
                Found = false,
                Detail = "could not be read"
            });
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadString(Microsoft.Win32.RegistryKey key, string name)
    {
        try
        {
            var value = key.GetValue(name);
            if (value is null)
            {
                return null;
            }

            if (value is string text)
            {
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }

            var converted = Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(converted) ? null : converted;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string HiveLabel(Microsoft.Win32.RegistryHive hive) =>
        hive == Microsoft.Win32.RegistryHive.CurrentUser ? "HKCU" : "HKLM";

    private static string ViewLabel(Microsoft.Win32.RegistryView view) =>
        view == Microsoft.Win32.RegistryView.Registry64 ? "64-bit" : "32-bit";

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
