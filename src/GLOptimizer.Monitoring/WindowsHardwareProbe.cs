using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using GLOptimizer.Core.Models;
using Microsoft.Win32;

namespace GLOptimizer.Monitoring;

/// <summary>
/// Reads hardware through WMI and the Windows version registry key.
/// Each query is isolated. A failure leaves that field empty and adds a warning.
/// WMI calls themselves cannot be aborted; cancellation is checked between queries.
/// </summary>
public sealed class WindowsHardwareProbe : IHardwareProbe
{
    public HardwareProbeSnapshot Capture(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var warnings = new List<string>();
        var architecture = RuntimeInformation.OSArchitecture.ToString();
        if (!OperatingSystem.IsWindows())
        {
            warnings.Add("CPU, GPU, memory, storage, and display queries require Windows.");
            return new HardwareProbeSnapshot
            {
                Architecture = architecture,
                Warnings = warnings
            };
        }

        return CaptureOnWindows(cancellationToken, architecture, warnings);
    }

    [SupportedOSPlatform("windows")]
    private static HardwareProbeSnapshot CaptureOnWindows(CancellationToken cancellationToken, string architecture, List<string> warnings)
    {
        string? cpuName = null;
        int? physical = null;
        int? logical = null;
        bool? virtualization = null;
        var gpus = new List<GpuReading>();
        int? refresh = null;
        long? memory = null;
        bool? hypervisor = null;
        var media = new List<int>();
        string? product = null;
        string? display = null;
        string? build = null;
        string? ubr = null;

        Query(
            null,
            "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, VirtualizationFirmwareEnabled FROM Win32_Processor",
            cancellationToken,
            warnings,
            "The processor query failed.",
            entry =>
            {
                cpuName ??= StringOf(entry, "Name");
                physical = Add(physical, IntOf(entry, "NumberOfCores"));
                logical = Add(logical, IntOf(entry, "NumberOfLogicalProcessors"));
                virtualization = Or(virtualization, BoolOf(entry, "VirtualizationFirmwareEnabled"));
            });

        Query(
            null,
            "SELECT Name, AdapterRAM, CurrentRefreshRate FROM Win32_VideoController",
            cancellationToken,
            warnings,
            "The display adapter query failed.",
            entry =>
            {
                gpus.Add(new GpuReading
                {
                    Name = StringOf(entry, "Name"),
                    AdapterRamBytes = LongOf(entry, "AdapterRAM")
                });
                var rate = IntOf(entry, "CurrentRefreshRate");
                if (rate is not null && (refresh is null || rate > refresh))
                {
                    refresh = rate;
                }
            });

        Query(
            null,
            "SELECT TotalPhysicalMemory, HypervisorPresent FROM Win32_ComputerSystem",
            cancellationToken,
            warnings,
            "The computer system query failed.",
            entry =>
            {
                memory ??= LongOf(entry, "TotalPhysicalMemory");
                hypervisor = Or(hypervisor, BoolOf(entry, "HypervisorPresent"));
            });

        Query(
            @"root\Microsoft\Windows\Storage",
            "SELECT MediaType FROM MSFT_PhysicalDisk",
            cancellationToken,
            warnings,
            "The physical disk query failed.",
            entry =>
            {
                var mediaType = IntOf(entry, "MediaType");
                if (mediaType is not null)
                {
                    media.Add(mediaType.Value);
                }
            });

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var current = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (current is null)
            {
                warnings.Add("The Windows version key was not found.");
            }
            else
            {
                product = Reg(current, "ProductName");
                display = Reg(current, "DisplayVersion");
                build = Reg(current, "CurrentBuild");
                ubr = Reg(current, "UBR");
            }
        }
        catch (Exception)
        {
            warnings.Add("The Windows version could not be read.");
        }

        return new HardwareProbeSnapshot
        {
            CpuName = cpuName,
            PhysicalCores = physical,
            LogicalCores = logical,
            Gpus = gpus,
            TotalMemoryBytes = memory,
            StorageMediaTypes = media,
            MonitorRefreshHz = refresh,
            WindowsProductName = product,
            WindowsDisplayVersion = display,
            WindowsCurrentBuild = build,
            WindowsUbr = ubr,
            Architecture = architecture,
            VirtualizationFirmwareEnabled = virtualization,
            HypervisorPresent = hypervisor,
            Warnings = warnings
        };
    }

    [SupportedOSPlatform("windows")]
    private static void Query(
        string? scope,
        string query,
        CancellationToken cancellationToken,
        List<string> warnings,
        string warning,
        Action<ManagementBaseObject> consume)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var searcher = scope is null
                ? new ManagementObjectSearcher(query)
                : new ManagementObjectSearcher(scope, query);
            using var results = searcher.Get();
            foreach (ManagementBaseObject entry in results)
            {
                using (entry)
                {
                    consume(entry);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            warnings.Add(warning);
        }
    }

    private static int? Add(int? current, int? value)
    {
        if (value is null or <= 0)
        {
            return current;
        }

        return (current ?? 0) + value.Value;
    }

    private static bool? Or(bool? current, bool? value)
    {
        if (value is null)
        {
            return current;
        }

        if (current is null)
        {
            return value;
        }

        return current.Value || value.Value;
    }

    [SupportedOSPlatform("windows")]
    private static string? StringOf(ManagementBaseObject entry, string name)
    {
        try
        {
            var text = entry[name]?.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static int? IntOf(ManagementBaseObject entry, string name)
    {
        try
        {
            var value = entry[name];
            return value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static long? LongOf(ManagementBaseObject entry, string name)
    {
        try
        {
            var value = entry[name];
            return value is null ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool? BoolOf(ManagementBaseObject entry, string name)
    {
        try
        {
            return entry[name] switch
            {
                bool flag => flag,
                null => null,
                var other => Convert.ToBoolean(other, CultureInfo.InvariantCulture)
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? Reg(RegistryKey key, string name)
    {
        try
        {
            var text = Convert.ToString(key.GetValue(name), CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
