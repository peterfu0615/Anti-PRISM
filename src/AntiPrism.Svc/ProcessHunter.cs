using System.Diagnostics;
using System.Management;
using AntiPrism.Core.Models;
using AntiPrism.Core.Storage;
using Microsoft.Win32;
using Serilog;

namespace AntiPrism.Svc;

public class ProcessHunter : IDisposable
{
    private readonly BlacklistStore _blacklistStore;
    private ManagementEventWatcher? _processWatcher;
    private readonly System.Timers.Timer _pollingTimer;
    private int _processesKilledCount;

    public int ProcessesKilledCount => _processesKilledCount;

    public ProcessHunter(BlacklistStore blacklistStore)
    {
        _blacklistStore = blacklistStore;
        _pollingTimer = new System.Timers.Timer(1000); // 1-second fallback poll
        _pollingTimer.Elapsed += (_, _) => PollRunningProcesses();
    }

    public void Start()
    {
        try
        {
            var query = new WqlEventQuery("Win32_ProcessStartTrace");
            _processWatcher = new ManagementEventWatcher(query);
            _processWatcher.EventArrived += OnProcessStarted;
            _processWatcher.Start();
            Log.Information("ProcessHunter Win32_ProcessStartTrace event watcher started");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to start Win32_ProcessStartTrace watcher, relying on 1s polling");
        }

        _pollingTimer.Start();
    }

    public void Stop()
    {
        _pollingTimer.Stop();

        if (_processWatcher != null)
        {
            try
            {
                _processWatcher.Stop();
                _processWatcher.Dispose();
            }
            catch { }
            _processWatcher = null;
        }
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var pid = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
            var processName = e.NewEvent.Properties["ProcessName"].Value?.ToString();

            EvaluateAndHunt(pid, processName);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error handling process start trace event");
        }
    }

    private void PollRunningProcesses()
    {
        try
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    EvaluateAndHunt(proc.Id, proc.ProcessName);
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error during process poll execution");
        }
    }

    private void EvaluateAndHunt(int pid, string? processName)
    {
        if (pid <= 4) return; // Ignore Idle & System

        try
        {
            using var proc = Process.GetProcessById(pid);
            string? fullPath = null;
            try
            {
                fullPath = proc.MainModule?.FileName;
            }
            catch { }

            string testName = !string.IsNullOrEmpty(processName) ? processName : proc.ProcessName;
            if (!testName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                testName += ".exe";
            }

            if (_blacklistStore.IsBlacklisted(fullPath, testName, out var matchedEntry) && matchedEntry != null)
            {
                if (matchedEntry.Mode == BlacklistMode.KillWin32)
                {
                    try
                    {
                        proc.Kill(entireProcessTree: true);
                        Interlocked.Increment(ref _processesKilledCount);
                        Log.Warning("HUNTED: Blacklisted process killed! PID: {Pid}, Name: {Name}, Path: {Path}",
                            pid, testName, fullPath ?? "N/A");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to kill blacklisted process {Pid} ({Name})", pid, testName);
                    }
                }
                else if (matchedEntry.Mode == BlacklistMode.DenyUwp && !string.IsNullOrEmpty(matchedEntry.PackageFamilyName))
                {
                    ApplyUwpDenyPolicy(matchedEntry.PackageFamilyName);
                }
            }
        }
        catch (ArgumentException)
        {
            // Process already exited
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Unable to inspect PID {Pid}", pid);
        }
    }

    public static void ApplyUwpDenyPolicy(string packageFamilyName)
    {
        try
        {
            // HKCU\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam\<PackageFamilyName>
            var subKeyPath = $@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam\{packageFamilyName}";
            using var key = Registry.CurrentUser.CreateSubKey(subKeyPath, writable: true);
            if (key != null)
            {
                key.SetValue("Value", "Deny", RegistryValueKind.String);
                Log.Information("Applied UWP webcam Deny policy for {Package}", packageFamilyName);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to set UWP Deny policy for {Package}", packageFamilyName);
        }
    }

    public void Dispose()
    {
        Stop();
        _pollingTimer.Dispose();
    }
}
