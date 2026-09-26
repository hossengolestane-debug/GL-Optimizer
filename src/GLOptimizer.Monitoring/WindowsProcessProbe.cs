using System.Diagnostics;
using GLOptimizer.Core.Detection;

namespace GLOptimizer.Monitoring;

/// <summary>
/// Reads GameLoop process CPU and memory. Membership is refreshed every five seconds; each capture only touches those handles.
/// </summary>
public sealed class WindowsProcessProbe : IProcessProbe, IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<int, Process> _handles = [];
    private readonly Dictionary<int, string> _paths = [];
    private DateTimeOffset _nextMembership = DateTimeOffset.MinValue;
    private bool _membershipFailed;
    private bool _unreadable;

    public ProcessProbeResult Capture()
    {
        lock (_gate)
        {
            try
            {
                if (DateTimeOffset.UtcNow >= _nextMembership)
                {
                    RefreshMembership();
                    _nextMembership = DateTimeOffset.UtcNow.AddSeconds(5);
                }

                if (_membershipFailed)
                {
                    return new ProcessProbeResult { Available = false };
                }

                return new ProcessProbeResult
                {
                    Available = true,
                    HadUnreadableMatch = _unreadable,
                    Processes = ReadHandles()
                };
            }
            catch (Exception)
            {
                return new ProcessProbeResult { Available = false };
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var process in _handles.Values)
            {
                process.Dispose();
            }

            _handles.Clear();
            _paths.Clear();
        }
    }

    private void RefreshMembership()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
            _membershipFailed = false;
        }
        catch (Exception)
        {
            _membershipFailed = true;
            return;
        }

        var seen = new HashSet<int>();
        var unreadable = false;
        foreach (var process in processes)
        {
            try
            {
                if (!GameLoopNames.IsProcess(process.ProcessName))
                {
                    process.Dispose();
                    continue;
                }

                string? path;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch (Exception)
                {
                    unreadable = true;
                    process.Dispose();
                    continue;
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    unreadable = true;
                    process.Dispose();
                    continue;
                }

                seen.Add(process.Id);
                if (_handles.ContainsKey(process.Id))
                {
                    process.Dispose();
                }
                else
                {
                    _handles[process.Id] = process;
                    _paths[process.Id] = path;
                }
            }
            catch (Exception)
            {
                process.Dispose();
            }
        }

        foreach (var id in _handles.Keys.ToArray())
        {
            if (seen.Contains(id))
            {
                continue;
            }

            _handles[id].Dispose();
            _handles.Remove(id);
            _paths.Remove(id);
        }

        _unreadable = unreadable;
    }

    private List<ProbedProcess> ReadHandles()
    {
        var list = new List<ProbedProcess>();
        foreach (var pair in _handles)
        {
            var process = pair.Value;
            try
            {
                if (process.HasExited)
                {
                    continue;
                }

                list.Add(new ProbedProcess
                {
                    ProcessId = pair.Key,
                    ProcessName = process.ProcessName,
                    ExecutablePath = _paths[pair.Key],
                    TotalProcessorTime = process.TotalProcessorTime,
                    WorkingSetBytes = process.WorkingSet64
                });
            }
            catch (Exception)
            {
                _unreadable = true;
            }
        }

        return list;
    }
}
