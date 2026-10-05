using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading.Tasks;
using WF = System.Windows.Forms;

namespace LePaSentinel;

/// <summary>
/// The app's home: a tray icon coloured by the most urgent state, showing how many tasks are active.
/// Left click opens the panel.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    readonly WF.NotifyIcon _icon;
    readonly MainWindow _window;
    readonly Dictionary<string, Icon> _icons = new();
    string _currentKey = "";

    public TrayIcon(MainWindow window)
    {
        _window = window;
        _icon = new WF.NotifyIcon { Text = L.Title, Icon = IconFor("idle", 0), Visible = true };

        var open = new WF.ToolStripMenuItem(L.TrayOpen, null, (_, _) => _window.ShowPanel())
        {
            Font = new Font(WF.Control.DefaultFont, FontStyle.Bold),
        };
        var pinned = new WF.ToolStripMenuItem(L.TrayPin, null, (_, _) => _window.SetPinned(!_window.Settings.Pinned));
        var sound = new WF.ToolStripMenuItem(L.TraySound, null, (_, _) => _window.ToggleSound());
        var testSound = new WF.ToolStripMenuItem(L.TrayTestSound, null, (_, _) =>
        {
            Sounds.PlayDone();
            Task.Delay(1400).ContinueWith(_ => Sounds.PlayWaiting());
        });
        var autostart = new WF.ToolStripMenuItem(L.TrayAutostart, null, async (_, _) => await Autostart.SetAsync(!Autostart.IsEnabled));
        var connected = new WF.ToolStripMenuItem(L.TrayConnected, null, (_, _) => SetConnected(!HookInstaller.IsInstalled()));
        var clear = new WF.ToolStripMenuItem(L.TrayClear, null, (_, _) => _window.ClearFinished());
        var about = new WF.ToolStripMenuItem(L.TrayAbout, null, (_, _) => ShowAbout());
        var exit = new WF.ToolStripMenuItem(L.TrayExit, null, (_, _) => _window.ExitApp());

        var menu = new WF.ContextMenuStrip();
        menu.Items.AddRange(new WF.ToolStripItem[]
        {
            open, pinned, new WF.ToolStripSeparator(),
            sound, testSound, autostart, connected, new WF.ToolStripSeparator(),
            clear, about, exit,
        });
        menu.Opening += (_, _) =>
        {
            pinned.Checked = _window.Settings.Pinned;
            sound.Checked = _window.Settings.Sound;
            autostart.Checked = Autostart.IsEnabled;
            connected.Checked = HookInstaller.IsInstalled();
        };
        _icon.ContextMenuStrip = menu;
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WF.MouseButtons.Left) _window.TogglePanel();
        };

        _window.AggregateChanged += (status, active, tooltip) =>
        {
            var key = $"{status}:{active}";
            if (key != _currentKey)
            {
                _currentKey = key;
                _icon.Icon = IconFor(status, active);
            }
            _icon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
        };
    }

    /// <summary>
    /// At start-up: keep the hooks pointing at this executable (the app may have been updated or moved)
    /// and, the very first time, ask the user whether to connect to Claude Code.
    /// </summary>
    public async void EnsureConnected()
    {
        var settings = _window.Settings;
        try
        {
            if (HookInstaller.IsInstalled())
            {
                settings.HooksWanted = settings.HooksPrompted = true;
            }
            else if (settings.HooksWanted || HookInstaller.HasAnyHook())
            {
                // Installed before (by us or by the setup) but with an old path: refresh silently.
                HookInstaller.Install();
                settings.HooksWanted = settings.HooksPrompted = true;
            }
            else if (!settings.HooksPrompted)
            {
                settings.HooksPrompted = true;
                var answer = System.Windows.MessageBox.Show(L.ConsentText(HookInstaller.SettingsPath), AppInfo.Name,
                    System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question,
                    System.Windows.MessageBoxResult.Yes, System.Windows.MessageBoxOptions.DefaultDesktopOnly);
                if (answer == System.Windows.MessageBoxResult.Yes) SetConnected(true);
            }
        }
        catch (Exception ex) { ShowError(ex); }
        settings.Save();
        _window.Connected = HookInstaller.IsInstalled();
        await Autostart.RefreshAsync();
    }

    void SetConnected(bool connect)
    {
        try
        {
            if (connect) HookInstaller.Install();
            else HookInstaller.Uninstall();
            _window.Settings.HooksWanted = connect;
            _window.Settings.HooksPrompted = true;
            _window.Settings.Save();
            if (connect) _icon.ShowBalloonTip(5000, AppInfo.Name, L.ConnectedBalloon, WF.ToolTipIcon.Info);
        }
        catch (Exception ex) { ShowError(ex); }
        _window.Connected = HookInstaller.IsInstalled();
    }

    static void ShowError(Exception ex) =>
        System.Windows.MessageBox.Show(L.HookError(HookInstaller.SettingsPath, ex.Message), AppInfo.Name,
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.OK, System.Windows.MessageBoxOptions.DefaultDesktopOnly);

    static void ShowAbout()
    {
        var answer = System.Windows.MessageBox.Show(L.About, AppInfo.Name,
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Information);
        if (answer != System.Windows.MessageBoxResult.Yes) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppInfo.ProjectUrl) { UseShellExecute = true }); }
        catch { }
    }

    /// <summary>Coloured disc with the number of active tasks (working + waiting) in it.</summary>
    Icon IconFor(string status, int active)
    {
        var key = $"{status}:{active}";
        if (_icons.TryGetValue(key, out var cached)) return cached;

        var color = status switch
        {
            "working" => Color.FromArgb(0x4C, 0x9A, 0xFF),
            "waiting" => Color.FromArgb(0xFF, 0xA2, 0x3E),
            "done" => Color.FromArgb(0x3D, 0xD6, 0x8C),
            _ => Color.FromArgb(0x8E, 0x90, 0x9A),
        };
        var dark = Color.FromArgb(0x1C, 0x1D, 0x22);

        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            using var fill = new SolidBrush(color);
            g.FillEllipse(fill, 1, 1, size - 2, size - 2);

            if (active > 0)
            {
                var text = active > 9 ? "9+" : active.ToString();
                using var font = new Font("Segoe UI", text.Length > 1 ? 13f : 17f, FontStyle.Bold, GraphicsUnit.Pixel);
                using var ink = new SolidBrush(dark);
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(text, font, ink, new RectangleF(0, 1, size, size), format);
            }
            else
            {
                using var core = new SolidBrush(dark);
                g.FillEllipse(core, 11, 11, 10, 10);
            }
        }
        var icon = Icon.FromHandle(bmp.GetHicon());
        _icons[key] = icon;
        return icon;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
