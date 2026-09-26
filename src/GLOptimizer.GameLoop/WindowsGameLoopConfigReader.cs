using System.Globalization;
using System.Runtime.Versioning;
using GLOptimizer.Core.Configuration;
using Microsoft.Win32;

namespace GLOptimizer.GameLoop;

public sealed class WindowsGameLoopConfigReader : IGameLoopConfigReader
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ini", ".cfg", ".txt", ".conf", ".json", ".xml"
    };

    public ConfigProbe ProbeFile(string path, bool readText)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return ConfigProbe.Missing();
            }

            var info = new FileInfo(path);
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return new ConfigProbe
                {
                    Exists = true,
                    ReparsePoint = true,
                    SizeBytes = SafeLength(info),
                    LastWriteTime = new DateTimeOffset(DateTime.SpecifyKind(info.LastWriteTimeUtc, DateTimeKind.Utc))
                };
            }

            var size = info.Length;
            var written = new DateTimeOffset(DateTime.SpecifyKind(info.LastWriteTimeUtc, DateTimeKind.Utc));
            if (!readText)
            {
                return new ConfigProbe { Exists = true, SizeBytes = size, LastWriteTime = written };
            }

            if (size > GameLoopConfigCatalog.MaxTextBytes)
            {
                return new ConfigProbe { Exists = true, TooLarge = true, SizeBytes = size, LastWriteTime = written };
            }

            if (!TextExtensions.Contains(info.Extension))
            {
                return new ConfigProbe { Exists = true, SizeBytes = size, LastWriteTime = written };
            }

            return new ConfigProbe
            {
                Exists = true,
                SizeBytes = size,
                LastWriteTime = written,
                Text = File.ReadAllText(path)
            };
        }
        catch (Exception)
        {
            return ConfigProbe.Failed();
        }
    }

    public RegistryProbe ReadMobileGamePc()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new RegistryProbe();
        }

        return ReadMobileGamePcOnWindows();
    }

    public IReadOnlyList<string> KnownUserFiles()
    {
        var files = new List<string>();
        AddProfile(files, Environment.SpecialFolder.ApplicationData);
        AddProfile(files, Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(roaming))
        {
            files.Add(Path.Combine(roaming, "AndroidTbox", "TVM_100.xml"));
            files.Add(Path.Combine(roaming, "Tencent", "GameLoop", "config", "com.tencent.ig", "smk.conf"));
            files.Add(Path.Combine(roaming, "Tencent", "MobileGamePC", "AppMarket3", "apklocalpkgs.json"));
        }

        return files;
    }

    public IReadOnlyList<SupplementalRegistry> ReadSupplementalRegistries()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        return ReadSupplementalOnWindows();
    }

    public IReadOnlyList<string> ListSiblingConfigs(string directory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return [];
            }

            var info = new DirectoryInfo(directory);
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return [];
            }

            var files = new List<string>();
            foreach (var file in info.EnumerateFiles("*.conf"))
            {
                if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                files.Add(file.FullName);
                if (files.Count >= 40)
                {
                    break;
                }
            }

            return files;
        }
        catch (Exception)
        {
            return [];
        }
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<SupplementalRegistry> ReadSupplementalOnWindows()
    {
        var specs = new (RegistryHive Hive, string SubKey, string Display)[]
        {
            (RegistryHive.CurrentUser, @"Software\Tencent\Call-of-Duty-Mobile-PCLauncher", @"HKCU\Software\Tencent\Call-of-Duty-Mobile-PCLauncher"),
            (RegistryHive.CurrentUser, @"Software\Tencent\Call-of-Duty", @"HKCU\Software\Tencent\Call-of-Duty"),
            (RegistryHive.CurrentUser, @"Software\Tencent\GameLoop", @"HKCU\Software\Tencent\GameLoop"),
            (RegistryHive.LocalMachine, @"SOFTWARE\Tencent\GameLoop", @"HKLM\SOFTWARE\Tencent\GameLoop")
        };
        var results = new List<SupplementalRegistry>();
        foreach (var spec in specs)
        {
            results.Add(ReadSupplemental(spec.Hive, spec.SubKey, spec.Display));
        }

        return results;
    }

    [SupportedOSPlatform("windows")]
    private static SupplementalRegistry ReadSupplemental(RegistryHive hive, string subKey, string display)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = false;
        var failed = false;
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(subKey, writable: false);
                if (key is null)
                {
                    continue;
                }

                found = true;
                foreach (var name in key.GetValueNames())
                {
                    if (!string.IsNullOrWhiteSpace(name) && name.Length <= 80)
                    {
                        names.Add(name);
                    }

                    if (names.Count >= 24)
                    {
                        break;
                    }
                }
            }
            catch (Exception)
            {
                failed = true;
            }
        }

        return new SupplementalRegistry
        {
            Path = display,
            Found = found,
            Failed = failed && !found,
            ValueNames = names.Order(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    [SupportedOSPlatform("windows")]
    private static RegistryProbe ReadMobileGamePcOnWindows()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unmapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = false;
        var failed = false;
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
                using var key = root.OpenSubKey(@"Software\Tencent\MobileGamePC", writable: false);
                if (key is null)
                {
                    continue;
                }

                found = true;
                ReadKey(key, values, conflicts, unmapped);
            }
            catch (Exception)
            {
                failed = true;
            }
        }

        if (!found)
        {
            return new RegistryProbe { Failed = failed };
        }

        foreach (var name in conflicts)
        {
            values.Remove(name);
        }

        return new RegistryProbe
        {
            Found = true,
            Values = values,
            UnmappedRendererKeys = unmapped.Order(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    [SupportedOSPlatform("windows")]
    private static void ReadKey(
        RegistryKey key,
        Dictionary<string, string> values,
        HashSet<string> conflicts,
        HashSet<string> unmapped)
    {
        foreach (var name in key.GetValueNames())
        {
            if (GameLoopSettingsParser.IsUnmappedRendererKey(name))
            {
                unmapped.Add(name);
                continue;
            }

            if (!IsCopied(name))
            {
                continue;
            }

            var text = ReadValue(key, name);
            if (text is null)
            {
                continue;
            }

            if (values.TryGetValue(name, out var existing)
                && !string.Equals(existing, text, StringComparison.OrdinalIgnoreCase))
            {
                conflicts.Add(name);
                continue;
            }

            values[name] = text;
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadValue(RegistryKey key, string name)
    {
        var kind = key.GetValueKind(name);
        if (kind is RegistryValueKind.DWord or RegistryValueKind.QWord)
        {
            var raw = Convert.ToInt64(key.GetValue(name), CultureInfo.InvariantCulture);
            if (raw is < 0 or > int.MaxValue)
            {
                return null;
            }

            return raw.ToString(CultureInfo.InvariantCulture);
        }

        if (kind != RegistryValueKind.String)
        {
            return null;
        }

        var text = key.GetValue(name) as string;
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > 32)
        {
            return null;
        }

        return text.Trim();
    }

    private static bool IsCopied(string name) =>
        GameLoopSettingsParser.IsFpsKey(name)
        || name.Equals("VMDPI", StringComparison.OrdinalIgnoreCase)
        || name.Equals("DPI", StringComparison.OrdinalIgnoreCase)
        || name.Equals("VMCpuCount", StringComparison.OrdinalIgnoreCase)
        || name.Equals("CpuAllocation", StringComparison.OrdinalIgnoreCase)
        || name.Equals("VMResWidth", StringComparison.OrdinalIgnoreCase)
        || name.Equals("VMResHeight", StringComparison.OrdinalIgnoreCase)
        || name.Equals("VMMemorySizeInMB", StringComparison.OrdinalIgnoreCase)
        || name.Equals("MemoryAllocation", StringComparison.OrdinalIgnoreCase)
        || name.Equals("VSyncEnabled", StringComparison.OrdinalIgnoreCase)
        || name.Equals("VSync", StringComparison.OrdinalIgnoreCase)
        || name.Equals("FxaaQuality", StringComparison.OrdinalIgnoreCase)
        || name.Equals("AntiAliasing", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Resolution", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Renderer", StringComparison.OrdinalIgnoreCase)
        || name.Equals("RendererMode", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ScreenRenderingMode", StringComparison.OrdinalIgnoreCase);

    private static void AddProfile(List<string> files, Environment.SpecialFolder folder)
    {
        var root = Environment.GetFolderPath(folder);
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        foreach (var directory in GameLoopConfigCatalog.UserDirectoryNames)
        {
            foreach (var name in GameLoopConfigCatalog.UserFileNames)
            {
                files.Add(Path.Combine(root, directory, name));
            }
        }
    }

    private static long? SafeLength(FileInfo info)
    {
        try
        {
            return info.Length;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
