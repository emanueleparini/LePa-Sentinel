using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace LePaSentinel;

public sealed class Settings
{
    /// <summary>Position of the pinned window.</summary>
    public double? Left { get; set; }
    public double? Top { get; set; }
    /// <summary>false = panel opens from the tray and closes on click elsewhere; true = stays on screen.</summary>
    public bool Pinned { get; set; }
    public bool Sound { get; set; } = true;
    /// <summary>After this many minutes a completed task is shown as idle.</summary>
    public int DoneToIdleMinutes { get; set; } = 30;
    /// <summary>The user has been asked (once) whether to connect to Claude Code.</summary>
    public bool HooksPrompted { get; set; }
    /// <summary>The user wants the hooks: keep their path up to date if the app moves.</summary>
    public bool HooksWanted { get; set; }

    static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.Name);
    static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

/// <summary>
/// "Start with Windows": a Run registry value for the classic install,
/// the package's StartupTask for the Microsoft Store (MSIX) install.
/// </summary>
public static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled { get; private set; }

    public static async Task RefreshAsync()
    {
        try
        {
            if (AppInfo.IsPackaged)
            {
                var task = await StartupTask.GetAsync(AppInfo.StartupTaskId);
                IsEnabled = task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                IsEnabled = key?.GetValue(AppInfo.Name) is string;
            }
        }
        catch { IsEnabled = false; }
    }

    public static async Task SetAsync(bool enabled)
    {
        try
        {
            if (AppInfo.IsPackaged)
            {
                var task = await StartupTask.GetAsync(AppInfo.StartupTaskId);
                if (enabled) await task.RequestEnableAsync();
                else task.Disable();
            }
            else
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (enabled) key.SetValue(AppInfo.Name, $"\"{Environment.ProcessPath}\"");
                else key.DeleteValue(AppInfo.Name, false);
            }
        }
        catch { }
        await RefreshAsync();
    }
}
