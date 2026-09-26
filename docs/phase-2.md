# Phase 2 — Real-time monitoring

Phase 2 samples the PC and GameLoop on a timer. It does not write GameLoop config, repair App Market, inject into a process, or invent FPS.

## What runs

`PerformanceSampler` waits on `PeriodicTimer` (500, 1000, or 2000 ms; default 1000). It does not spin. The first counter read is discarded so the leading zero from a Windows performance counter is not shown.

`WindowsSystemMonitor` reads:

- Processor `% Processor Time` (`_Total`) for CPU
- `GlobalMemoryStatusEx` for used and total RAM
- PhysicalDisk `% Disk Time` (`_Total`) for disk activity. Values above 100 are capped at 100 because that counter can overshoot. If the counter is missing, disk stays Unknown.
- GPU Engine `Utilization Percentage` for `engtype_3d`, at most 48 instances. The instance list is checked at most every 10 seconds and counters are kept until that list changes. The sample keeps the highest engine, which is the same idea as Task Manager. A counter that was just created is primed and that sample's GPU % stays Unknown, because the first read of a rate counter is not a measurement. If the category is missing, GPU % stays Unknown for at least a minute before another lookup.
- GPU Adapter Memory `Dedicated Usage` for VRAM in use, summed across the reported adapters. Total VRAM is not guessed. If the category is missing, used VRAM stays Unknown. Phase 1 WMI totals for very large adapters can still be Unknown.

`GameLoopMonitor` asks the Phase 1 detector for install roots once (and at most once a minute if nothing was found). It does not walk the disk on every tick. `WindowsProcessProbe` refreshes the process list every five seconds and only for known GameLoop process names. Each tick reads CPU time and working set from those handles. A process counts only when its path is inside a verified install. State is Running, Stopped, or Unknown. CPU needs two samples; the first one stays empty.

The dashboard samples while it is visible. The Monitoring page samples until Stop, and Stop keeps it off if you leave and come back. Any other page stops the timer. App exit disposes the sampler. GL Optimizer does not keep a timer running on Settings, Logs, or the other pages.

History is an in-memory 5-minute buffer (capped at 720 samples). The charts draw at most 120 averaged points. Spikes are samples that cross 90% CPU or GPU; a value that stays high is one spike, not one per tick. Nothing is written to disk for the history.

## FPS

`IFrameMetricsProvider` is still the only frame API. The current implementation returns not implemented and a null sample. The Monitoring page and the dashboard chart show: "FPS monitoring unavailable with current safe monitoring method." No FPS number is fabricated, and nothing is injected.

## Not in this phase

OPTIMIZE NOW stays disabled. Close and Restart still do not kill processes. App Market files are not opened. GameLoop config files are not written.
