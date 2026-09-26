using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Monitoring;
using GLOptimizer.Core.Navigation;
using GLOptimizer.Core.Settings;

namespace GLOptimizer.App.ViewModels;

public partial class MonitoringViewModel : PageViewModel, IRefreshable, IDisposable
{
    private readonly IHardwareService _hardware;
    private readonly IMonitoringCoordinator _monitoring;
    private readonly ISettingsStore _settings;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly ScanSession _session = new();

    public MonitoringViewModel(IHardwareService hardware, IMonitoringCoordinator monitoring, ISettingsStore settings)
        : base(AppPage.Monitoring)
    {
        _hardware = hardware;
        _monitoring = monitoring;
        _settings = settings;
        _monitoring.Updated += OnMonitoringUpdated;
        SelectInterval(AppSettingsRules.NormalizeSampleInterval(settings.Current.SampleIntervalMilliseconds));
        ApplySample();
    }

    public void Dispose() => _monitoring.Updated -= OnMonitoringUpdated;

    public ObservableCollection<DiagnosticRowModel> HardwareRows { get; } = new();

    public MetricTile Cpu { get; } = new("CPU");

    public MetricTile Gpu { get; } = new("GPU");

    public MetricTile Ram { get; } = new("RAM");

    public MetricTile Disk { get; } = new("Disk");

    public string FpsNotice => Phase0Notices.NoFrameMetrics;

    public string SafetyNote => Phase0Notices.Safety;

    public string ChartSubtitle => "Last 5 minutes, at most 120 points.";

    [ObservableProperty]
    private string _statusLine = "Monitoring is off.";

    [ObservableProperty]
    private string _gameLoopCpu = HardwareText.Unknown;

    [ObservableProperty]
    private string _gameLoopRam = HardwareText.Unknown;

    [ObservableProperty]
    private string _gameLoopState = "Unknown";

    [ObservableProperty]
    private string _summaryText = "No samples yet.";

    [ObservableProperty]
    private string _spikeText = "No spikes.";

    [ObservableProperty]
    private double[] _cpuSeries = [];

    [ObservableProperty]
    private double[] _gpuSeries = [];

    [ObservableProperty]
    private double[] _ramSeries = [];

    [ObservableProperty]
    private double[] _diskSeries = [];

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private bool _interval500;

    [ObservableProperty]
    private bool _interval1000;

    [ObservableProperty]
    private bool _interval2000;

    [RelayCommand]
    private void Start() => _monitoring.Start();

    [RelayCommand]
    private void Stop() => _monitoring.Stop();

    [RelayCommand]
    private void CancelScan() => _session.Cancel();

    [RelayCommand]
    private void UseInterval(string milliseconds)
    {
        if (!int.TryParse(milliseconds, out var value))
        {
            return;
        }

        var settings = _settings.Current;
        settings.SampleIntervalMilliseconds = value;
        var saved = _settings.Save(settings);
        if (!saved.Succeeded)
        {
            StatusLine = saved.Error ?? "The sample interval could not be saved.";
            return;
        }

        SelectInterval(AppSettingsRules.NormalizeSampleInterval(_settings.Current.SampleIntervalMilliseconds));
        _monitoring.NotifyIntervalChanged();
    }

    public void Refresh() => _ = RunScanAsync();

    private void OnMonitoringUpdated(object? sender, EventArgs e)
    {
        if (_ui is null)
        {
            ApplySample();
            return;
        }

        _ui.Post(_ => ApplySample(), null);
    }

    private void ApplySample()
    {
        IsMonitoring = _monitoring.IsRunning;
        var latest = _monitoring.Latest;
        var history = _monitoring.History;
        var summary = _monitoring.Summary;
        StatusLine = IsMonitoring
            ? "Sampling every " + IntervalLabel() + "."
            : latest is null ? "Monitoring is off." : "Monitoring is stopped. The last sample is still shown.";

        Cpu.Set(MetricText.Percent(latest?.CpuPercent), "Processor time", latest?.CpuPercent is null ? "Unknown" : "Live", latest?.CpuPercent is null ? StatusKind.Unavailable : StatusKind.Ready);
        var gpuDetail = latest?.GpuVramUsedBytes is null
            ? "GPU utilization was not reported."
            : "VRAM " + MetricText.Bytes(latest.GpuVramUsedBytes);
        Gpu.Set(MetricText.Percent(latest?.GpuPercent), gpuDetail, latest?.GpuPercent is null ? "Unknown" : "Live", latest?.GpuPercent is null ? StatusKind.Unavailable : StatusKind.Ready);
        Ram.Set(MetricText.RamPair(latest?.RamUsedBytes, latest?.RamTotalBytes), MetricText.Percent(latest?.RamPercent) + " used", latest?.RamPercent is null ? "Unknown" : "Live", latest?.RamPercent is null ? StatusKind.Unavailable : StatusKind.Ready);
        Disk.Set(MetricText.Percent(latest?.DiskPercent), "Disk activity", latest?.DiskPercent is null ? "Unknown" : "Live", latest?.DiskPercent is null ? StatusKind.Unavailable : StatusKind.Ready);

        GameLoopCpu = MetricText.Percent(latest?.GameLoopCpuPercent);
        GameLoopRam = MetricText.Bytes(latest?.GameLoopRamBytes);
        GameLoopState = latest is null ? "Unknown" : DetectionText.RunStatus(latest.GameLoopState);
        SummaryText = "CPU avg " + MetricText.Percent(summary.AverageCpuPercent)
            + " / peak " + MetricText.Percent(summary.PeakCpuPercent)
            + "    GPU avg " + MetricText.Percent(summary.AverageGpuPercent)
            + " / peak " + MetricText.Percent(summary.PeakGpuPercent)
            + "    RAM avg " + MetricText.Percent(summary.AverageRamPercent);
        SpikeText = summary.Spikes.Count == 0
            ? "No spikes at or above 90%."
            : summary.Spikes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " spike(s) at or above 90%.";

        CpuSeries = ChartSeries.Downsample(history.Select(sample => sample.CpuPercent).ToArray());
        GpuSeries = ChartSeries.Downsample(history.Select(sample => sample.GpuPercent).ToArray());
        RamSeries = ChartSeries.Downsample(history.Select(sample => sample.RamPercent).ToArray());
        DiskSeries = ChartSeries.Downsample(history.Select(sample => sample.DiskPercent).ToArray());
    }

    private string IntervalLabel()
    {
        var interval = AppSettingsRules.NormalizeSampleInterval(_settings.Current.SampleIntervalMilliseconds);
        return interval switch
        {
            500 => "0.5 s",
            2000 => "2 s",
            _ => "1 s"
        };
    }

    private void SelectInterval(int milliseconds)
    {
        Interval500 = milliseconds == 500;
        Interval1000 = milliseconds == 1000;
        Interval2000 = milliseconds == 2000;
    }

    private async Task RunScanAsync()
    {
        var (generation, token) = _session.Start();
        try
        {
            var hardware = await _hardware.GetReportAsync(token);
            if (!_session.IsCurrent(generation))
            {
                return;
            }

            var report = hardware.Succeeded ? hardware.Value : null;
            HardwareRows.Clear();
            HardwareRows.Add(Row("CPU", HardwareText.Text(report?.CpuName), report?.CpuName is not null));
            HardwareRows.Add(Row("GPU", HardwareText.Text(report?.GpuName), report?.GpuName is not null));
            HardwareRows.Add(Row("VRAM", HardwareText.Memory(report?.GpuMemoryBytes), report?.GpuMemoryBytes is not null));
            HardwareRows.Add(Row("Memory", HardwareText.Memory(report?.TotalMemoryBytes), report?.TotalMemoryBytes is not null));
            HardwareRows.Add(Row("Storage", HardwareText.Text(report?.StorageType), report?.StorageType is not null));
            HardwareRows.Add(Row("Refresh", HardwareText.Hertz(report?.MonitorRefreshHz), report?.MonitorRefreshHz is not null));
            HardwareRows.Add(Row("Windows", HardwareText.Text(report?.WindowsVersion), report?.WindowsVersion is not null));
            HardwareRows.Add(Row("Architecture", HardwareText.Text(report?.Architecture), report?.Architecture is not null));
        }
        catch (Exception ex)
        {
            if (_session.IsCurrent(generation))
            {
                StatusLine = "The hardware scan could not finish. " + ex.Message;
            }
        }
    }

    private static DiagnosticRowModel Row(string title, string value, bool known) =>
        new(title, value, known ? "Reported" : "Unknown", known ? StatusKind.Ready : StatusKind.Unavailable);
}
