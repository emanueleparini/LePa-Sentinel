using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace LePaSentinel;

/// <summary>
/// LePaSentinel.exe              tray app
/// LePaSentinel.exe --hook       handle one Claude Code hook event (stdin JSON)
/// LePaSentinel.exe --install-hooks / --uninstall-hooks   connect to / disconnect from Claude Code (used by the installer)
/// </summary>
public static class Program
{
    const string MutexName = @"Local\LePaSentinel.GUI";
    const string ShowEventName = @"Local\LePaSentinel.Show";

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int processId);

    [STAThread]
    public static int Main(string[] args)
    {
        switch (args.Length > 0 ? args[0] : "")
        {
            case "--hook":
                // Must be fast and never fail: Claude Code runs it on every event.
                HookHandler.Run();
                return 0;
            case "--install-hooks":
                return Try(HookInstaller.Install);
            case "--uninstall-hooks":
                return Try(HookInstaller.Uninstall);
            default:
                return RunGui();
        }
    }

    static int Try(Action action)
    {
        try { action(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }

    // Kept separate so hook mode never loads the WPF assemblies.
    [MethodImpl(MethodImplOptions.NoInlining)]
    static int RunGui()
    {
        using var mutex = new Mutex(true, MutexName, out bool created);
        if (!created)
        {
            // Let the running instance take the foreground we were given, then ask it to open the panel.
            AllowSetForegroundWindow(-1);
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            return 0;
        }

        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(Settings.Load());
        using var tray = new TrayIcon(window);

        var listener = new Thread(() =>
        {
            while (showEvent.WaitOne())
                window.Dispatcher.BeginInvoke(window.ShowPanel);
        }) { IsBackground = true };
        listener.Start();

        // Unpinned, the app lives in the tray: the panel opens from the tray icon.
        if (window.Settings.Pinned) window.ShowPanel();
        window.Dispatcher.BeginInvoke(tray.EnsureConnected);
        app.Run();
        return 0;
    }
}
