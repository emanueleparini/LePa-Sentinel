using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LePaSentinel;

/// <summary>
/// Links a session to the claude.exe process that runs it, so the GUI can drop sessions
/// whose process is gone and collapse several session ids hosted by the same process.
/// </summary>
public static class ClaudeProcess
{
    static readonly string[] Names = { "claude.exe", "node.exe" };

    /// <summary>Walks up from this hook process (hook ← shell ← claude.exe) to the Claude Code process.</summary>
    public static (int Pid, DateTime StartedAt)? FindAncestor()
    {
        try
        {
            var table = Snapshot();
            foreach (var name in Names)
            {
                int pid = Environment.ProcessId;
                for (int depth = 0; depth < 12 && table.TryGetValue(pid, out var entry); depth++)
                {
                    if (pid != Environment.ProcessId && entry.Exe.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        using var p = Process.GetProcessById(pid);
                        return (pid, p.StartTime.ToUniversalTime());
                    }
                    if (entry.Parent == 0 || entry.Parent == pid) break;
                    pid = entry.Parent;
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>Parent, grandparent, … of a process (the process itself excluded).</summary>
    public static IEnumerable<int> Ancestors(int pid)
    {
        var table = Snapshot();
        for (int depth = 0; depth < 12 && table.TryGetValue(pid, out var entry); depth++)
        {
            if (entry.Parent == 0 || entry.Parent == pid) yield break;
            pid = entry.Parent;
            yield return pid;
        }
    }

    public static bool IsAlive(int pid, DateTime? startedAt)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (p.HasExited) return false;
            // Guard against the pid having been reused by a different process.
            return startedAt is not { } start || Math.Abs((p.StartTime.ToUniversalTime() - start).TotalSeconds) < 2;
        }
        catch (ArgumentException) { return false; }      // no such process
        catch { return true; }                            // access denied etc.: assume alive
    }

    static Dictionary<int, (int Parent, string Exe)> Snapshot()
    {
        var table = new Dictionary<int, (int, string)>();
        var snap = CreateToolhelp32Snapshot(0x2 /* TH32CS_SNAPPROCESS */, 0);
        if (snap == new IntPtr(-1)) return table;
        try
        {
            var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf<ProcessEntry32>() };
            for (bool ok = Process32FirstW(snap, ref entry); ok; ok = Process32NextW(snap, ref entry))
                table[(int)entry.ProcessId] = ((int)entry.ParentProcessId, entry.ExeFile);
        }
        finally { CloseHandle(snap); }
        return table;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry32 entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry32 entry);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);
}
