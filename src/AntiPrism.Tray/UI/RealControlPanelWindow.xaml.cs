using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AntiPrism.Core.Models;
using AntiPrism.Core.Process;
using AntiPrism.Core.Storage;
using AntiPrism.Tray.Services;
using Microsoft.Win32;
using Serilog;

namespace AntiPrism.Tray.UI;

public partial class RealControlPanelWindow : Window
{
    private readonly TrayServiceCoordinator _coordinator;
    private readonly ObservableCollection<CameraDevice> _cameras = new();
    private readonly ObservableCollection<ProcessInfo> _activeProcesses = new();
    private readonly ObservableCollection<BlacklistEntry> _blacklist = new();
    private List<ProcessInfo> _allProcessesCache = new();

    public RealControlPanelWindow(TrayServiceCoordinator coordinator)
    {
        InitializeComponent();
        _coordinator = coordinator;

        CameraGrid.ItemsSource = _cameras;
        ProcessGrid.ItemsSource = _activeProcesses;
        BlacklistGrid.ItemsSource = _blacklist;

        Loaded += async (_, _) => await InitializeDataAsync();
    }

    private async Task InitializeDataAsync()
    {
        LoadStealthSettings();
        await RefreshStatusAndCamerasAsync();
        RefreshProcesses();
        RefreshBlacklistUi();
        RefreshLogs();
    }

    private async Task RefreshStatusAndCamerasAsync()
    {
        var status = await _coordinator.GetStatusAsync();
        if (status != null)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 124, 65)); // Green
            StatusText.Text = $"Service Connected (Killed: {status.ProcessesKilledCount})";

            _cameras.Clear();
            foreach (var cam in status.Cameras)
            {
                cam.IsOccupied = status.OccupiedCameraIds.Contains(cam.Id) || status.OccupiedCameraIds.Contains(cam.SymbolicLink);
                _cameras.Add(cam);
            }

            RadioOccupy.IsChecked = status.ProtectionMode == CameraProtectionMode.Occupy;
            RadioDisable.IsChecked = status.ProtectionMode == CameraProtectionMode.Disable;
            RadioNone.IsChecked = status.ProtectionMode == CameraProtectionMode.None;
        }
        else
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(200, 30, 30)); // Red
            StatusText.Text = "Service Disconnected";
        }
    }

    private void RefreshProcesses()
    {
        Task.Run(() =>
        {
            var list = ProcessLister.GetActiveProcesses();
            Dispatcher.Invoke(() =>
            {
                _allProcessesCache = list;
                ApplyProcessFilter();
            });
        });
    }

    private void ApplyProcessFilter()
    {
        var filter = TxtProcessSearch.Text?.Trim().ToLowerInvariant() ?? string.Empty;
        _activeProcesses.Clear();

        foreach (var p in _allProcessesCache)
        {
            if (string.IsNullOrEmpty(filter) ||
                p.ProcessName.ToLowerInvariant().Contains(filter) ||
                (p.Description != null && p.Description.ToLowerInvariant().Contains(filter)) ||
                (p.ExecutablePath != null && p.ExecutablePath.ToLowerInvariant().Contains(filter)))
            {
                _activeProcesses.Add(p);
            }
        }
    }

    private void RefreshBlacklistUi()
    {
        _blacklist.Clear();
        foreach (var item in _coordinator.Config.Blacklist)
        {
            _blacklist.Add(item);
        }
    }

    private void LoadStealthSettings()
    {
        var g = _coordinator.Config.Gesture;
        TxtSecretValue.Text = g.SecretValue.ToString();
        TxtSecretCount.Text = g.SecretCount.ToString();
        TxtSecretWindow.Text = g.SecretWindowSec.ToString();
        ChkEnableHotkey.IsChecked = g.EnableEmergencyHotkey;
        ChkDisguiseNotifications.IsChecked = _coordinator.Config.DisguiseNotifications;
    }

    private async void BtnApplyCameraMode_Click(object sender, RoutedEventArgs e)
    {
        var mode = CameraProtectionMode.None;
        if (RadioOccupy.IsChecked == true) mode = CameraProtectionMode.Occupy;
        else if (RadioDisable.IsChecked == true) mode = CameraProtectionMode.Disable;

        bool ok = await _coordinator.SetProtectionModeAsync(mode);
        MessageBox.Show(ok ? $"Mode switched to {mode}" : "Failed to change mode in service.",
            "Anti-PRISM", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        await RefreshStatusAndCamerasAsync();
    }

    private async void BtnRefreshCameras_Click(object sender, RoutedEventArgs e)
    {
        await RefreshStatusAndCamerasAsync();
    }

    private void BtnRefreshProcesses_Click(object sender, RoutedEventArgs e)
    {
        RefreshProcesses();
    }

    private void TxtProcessSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyProcessFilter();
    }

    private async void BtnAddSelectedToBlacklist_Click(object sender, RoutedEventArgs e)
    {
        var selected = ProcessGrid.SelectedItems.Cast<ProcessInfo>().ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Please select one or more processes to blacklist.", "Anti-PRISM", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        foreach (var p in selected)
        {
            if (string.IsNullOrEmpty(p.ExecutablePath))
            {
                // Add by process name if path denied
                if (!_blacklist.Any(b => b.FileName.Equals(p.ProcessName + ".exe", StringComparison.OrdinalIgnoreCase)))
                {
                    _blacklist.Add(new BlacklistEntry
                    {
                        FileName = p.ProcessName + ".exe",
                        ExePath = string.Empty,
                        Mode = BlacklistMode.KillWin32
                    });
                }
            }
            else
            {
                var fileName = Path.GetFileName(p.ExecutablePath);
                if (!_blacklist.Any(b => b.ExePath.Equals(p.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                {
                    _blacklist.Add(new BlacklistEntry
                    {
                        FileName = fileName,
                        ExePath = p.ExecutablePath,
                        Mode = BlacklistMode.KillWin32
                    });
                }
            }
        }

        await _coordinator.UpdateBlacklistAsync(_blacklist.ToList());
        MessageBox.Show($"Added {selected.Count} processes to Blacklist.", "Anti-PRISM", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnAddManualExe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
            Title = "Select Application to Blacklist"
        };

        if (dialog.ShowDialog() == true)
        {
            var fullPath = dialog.FileName;
            var fileName = Path.GetFileName(fullPath);

            if (!_blacklist.Any(b => b.ExePath.Equals(fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                _blacklist.Add(new BlacklistEntry
                {
                    FileName = fileName,
                    ExePath = fullPath,
                    Mode = BlacklistMode.KillWin32
                });

                await _coordinator.UpdateBlacklistAsync(_blacklist.ToList());
            }
        }
    }

    private async void BtnRemoveBlacklist_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is BlacklistEntry entry)
        {
            _blacklist.Remove(entry);
            await _coordinator.UpdateBlacklistAsync(_blacklist.ToList());
        }
    }

    private async void BtnSaveStealthSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var g = _coordinator.Config.Gesture;
            if (double.TryParse(TxtSecretValue.Text, out var val)) g.SecretValue = val;
            if (int.TryParse(TxtSecretCount.Text, out var cnt)) g.SecretCount = cnt;
            if (int.TryParse(TxtSecretWindow.Text, out var win)) g.SecretWindowSec = win;

            g.EnableEmergencyHotkey = ChkEnableHotkey.IsChecked == true;
            _coordinator.Config.DisguiseNotifications = ChkDisguiseNotifications.IsChecked == true;

            await _coordinator.SaveConfigAsync(_coordinator.Config);
            MessageBox.Show("Stealth and Gesture settings saved successfully.", "Anti-PRISM", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Anti-PRISM", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnEmergencyUnbrick_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to restore and enable all camera hardware devices?",
            "Anti-PRISM Emergency Rescue",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            var msg = await _coordinator.EmergencyRestoreAsync();
            MessageBox.Show(msg, "Anti-PRISM", MessageBoxButton.OK, MessageBoxImage.Information);
            await RefreshStatusAndCamerasAsync();
        }
    }

    private void BtnRefreshLog_Click(object sender, RoutedEventArgs e)
    {
        RefreshLogs();
    }

    private void RefreshLogs()
    {
        try
        {
            var logDir = Path.Combine(ConfigManager.GetConfigDirectory(), "logs");
            if (Directory.Exists(logDir))
            {
                var files = Directory.GetFiles(logDir, "*.log").OrderByDescending(File.GetLastWriteTime).ToList();
                if (files.Count > 0)
                {
                    using var stream = new FileStream(files[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    TxtDiagnosticsLog.Text = reader.ReadToEnd();
                    TxtDiagnosticsLog.ScrollToEnd();
                    return;
                }
            }
            TxtDiagnosticsLog.Text = "No service log file found yet. (Logs will appear in %ProgramData%\\Anti-PRISM\\logs\\)";
        }
        catch (Exception ex)
        {
            TxtDiagnosticsLog.Text = $"Failed to read logs: {ex.Message}";
        }
    }
}
