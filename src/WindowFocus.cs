using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace LePaSentinel;

/// <summary>
/// Brings to the front the window where a session lives, instead of launching `code`
/// (which opens a new window when the session's folder is a subfolder of the open workspace).
/// </summary>
public static class WindowFocus
{
    /// <returns>true if an existing window was brought to the front.</returns>
    public static bool FocusSession(string cwd, int? claudePid)
    {
        try
        {
            if (FindVsCodeWindow(cwd) is { } code) return Activate(code);
            if (claudePid is { } pid && FindHostWindow(pid) is { } host) return Activate(host);
        }
        catch { }
        return false;
    }

    /// <summary>
    /// VS Code titles look like "file - workspace - Visual Studio Code": match the folder of the session,
    /// then its parents (the session may run in a subfolder of the open workspace).
    /// </summary>
    static IntPtr? FindVsCodeWindow(string cwd)
    {
        var codePids = Process.GetProcessesByName("Code")
            .Concat(Process.GetProcessesByName("Code - Insiders"))
            .Select(p => { using (p) return (uint)p.Id; })
            .ToHashSet();
        if (codePids.Count == 0) return null;

        var windows = TopLevelWindows().Where(w => codePids.Contains(w.Pid)).ToList();
        foreach (var folder in FolderNames(cwd))
        {
            foreach (var w in windows)
            {
                var parts = w.Title.Split(" - ").Select(s => s.Trim());
                if (parts.Any(part => part.Equals(folder, StringComparison.OrdinalIgnoreCase)
                                   || part.Equals($"{folder} (Workspace)", StringComparison.OrdinalIgnoreCase)))
                    return w.Handle;
            }
        }
        return null;
    }

    static IEnumerable<string> FolderNames(string cwd)
    {
        var dir = cwd.TrimEnd('\\', '/');
        for (int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
        {
            var name = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(name)) yield break;
            yield return name;
            dir = Path.GetDirectoryName(dir) ?? "";
        }
    }

    /// <summary>Claude run from a standalone terminal: the first ancestor process that owns a window.</summary>
    static IntPtr? FindHostWindow(int claudePid)
    {
        var windows = TopLevelWindows();
        foreach (var pid in ClaudeProcess.Ancestors(claudePid))
        {
            var w = windows.FirstOrDefault(x => x.Pid == (uint)pid);
            if (w.Handle != IntPtr.Zero) return w.Handle;
        }
        return null;
    }

    static bool Activate(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, 9 /* SW_RESTORE */);
        return SetForegroundWindow(hwnd);
    }

    static List<(IntPtr Handle, uint Pid, string Title)> TopLevelWindows()
    {
        var list = new List<(IntPtr, uint, string)>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            var title = new StringBuilder(512);
            if (GetWindowText(hwnd, title, title.Capacity) == 0) return true;
            GetWindowThreadProcessId(hwnd, out var pid);
            list.Add((hwnd, pid, title.ToString()));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
}
