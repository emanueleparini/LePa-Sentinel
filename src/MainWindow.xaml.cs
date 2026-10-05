using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace LePaSentinel;

/// <summary>
/// The panel. By default it opens from the tray icon and hides when it loses focus;
/// when pinned it stays on screen (always on top) where the user dragged it.
/// </summary>
public partial class MainWindow : Window
{
    const double PanelWidth = 380;
    static readonly TimeSpan StaleAfter = TimeSpan.FromHours(12);
    static readonly TimeSpan SilentWorkingHint = TimeSpan.FromMinutes(5);

    readonly ObservableCollection<CardViewModel> _cards = new();
    readonly Dictionary<string, string> _lastStatus = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    bool _firstLoad = true;
    bool _exiting;
    int _ticks;
    DateTime _hiddenAt;
    DateTime _shownAt;

    public Settings Settings { get; }

    /// <summary>Whether Claude Code's hooks point at this app (kept up to date by the tray).</summary>
    public bool Connected { get; set; } = true;

    /// <summary>Raised on every refresh: (aggregate status, active task count, tooltip text).</summary>
    public event Action<string, int, string>? AggregateChanged;

    public MainWindow(Settings settings)
    {
        Settings = settings;
        InitializeComponent();
        Width = PanelWidth;
        List.ItemsSource = _cards;
        UpdateSoundButton();
        UpdatePinButton();

        // Runs even while the panel is hidden: the tray icon and the sounds depend on it.
        _timer.Tick += (_, _) =>
        {
            Refresh();
            if (++_ticks % 2 == 0) KeepOnTop();
        };
        Store.CleanOrphans();
        Refresh();
        _timer.Start();

        Deactivated += (_, _) =>
        {
            // Windows may bounce the focus right after opening: only a later click elsewhere closes the panel.
            if (!Settings.Pinned && DateTime.UtcNow - _shownAt > TimeSpan.FromMilliseconds(600)) HidePanel();
        };
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        };
        Closing += (_, e) =>
        {
            if (_exiting) return;
            e.Cancel = true;
            HidePanel();
        };
    }

    // ---------- state refresh ----------

    void Refresh()
    {
        List<SessionState> states;
        try { states = Store.LoadAll(); }
        catch { return; }

        var now = DateTime.UtcNow;
        var visible = new List<SessionState>();

        foreach (var s in states)
        {
            bool dead = s.ClaudePid is { } pid && !ClaudeProcess.IsAlive(pid, s.ClaudeStartedAt);
            if (dead || now - s.UpdatedAt > StaleAfter)
            {
                Store.Delete(s.SessionId);
                continue;
            }
            // Sessions that never received a prompt (e.g. the spare process VS Code keeps ready) are not shown.
            if (!string.IsNullOrEmpty(s.Prompt)) visible.Add(s);
        }

        // One process hosts one live session: after /clear the old session id is obsolete.
        foreach (var group in visible.Where(s => s.ClaudePid != null).GroupBy(s => s.ClaudePid).Where(g => g.Count() > 1))
        {
            foreach (var old in group.OrderByDescending(s => s.UpdatedAt).Skip(1))
            {
                Store.Delete(old.SessionId);
                visible.Remove(old);
            }
        }

        int working = 0, waiting = 0, done = 0, idle = 0;
        var seen = new HashSet<string>();
        foreach (var s in visible)
        {
            seen.Add(s.SessionId);

            _lastStatus.TryGetValue(s.SessionId, out var previous);
            if (!_firstLoad && previous != s.Status && Settings.Sound)
            {
                if (s.Status == "done") Sounds.PlayDone();
                else if (s.Status == "waiting") Sounds.PlayWaiting();
            }
            _lastStatus[s.SessionId] = s.Status;

            var display = DisplayStatus(s, now);
            switch (display)
            {
                case "working": working++; break;
                case "waiting": waiting++; break;
                case "done": done++; break;
                default: idle++; break;
            }

            var card = _cards.FirstOrDefault(c => c.SessionId == s.SessionId);
            if (card == null)
            {
                card = new CardViewModel(s.SessionId);
                _cards.Add(card);
            }
            Fill(card, s, display, now);
        }

        foreach (var gone in _cards.Where(c => !seen.Contains(c.SessionId)).ToList())
            _cards.Remove(gone);
        foreach (var key in _lastStatus.Keys.Where(k => !seen.Contains(k)).ToList())
            _lastStatus.Remove(key);

        Sort();
        _firstLoad = false;
        UpdateHeader(working, waiting, done, idle);
    }

    string DisplayStatus(SessionState s, DateTime now)
    {
        if (s.Status == "done" && s.FinishedAt is { } finished
            && now - finished > TimeSpan.FromMinutes(Settings.DoneToIdleMinutes))
            return "idle";
        return s.Status;
    }

    void Fill(CardViewModel card, SessionState s, string display, DateTime now)
    {
        card.Cwd = s.Cwd;
        card.ClaudePid = s.ClaudePid;
        card.Project = ProjectName(s.Cwd);
        card.StatusBrush = BrushFor(display);
        card.IsWorking = display == "working";
        card.IsWaiting = display == "waiting";

        var prompt = string.IsNullOrEmpty(s.Prompt) ? "" : $"“{s.Prompt}”";
        switch (display)
        {
            case "working":
                card.Rank = 1;
                card.Subtitle = prompt;
                card.TimeText = Elapsed(now - s.StartedAt);
                card.TimeBrush = BrushFor("fg");
                if (SilentFor(s, now) is { } silent && silent > SilentWorkingHint)
                {
                    card.TimeText += L.Stalled;
                    card.TimeBrush = BrushFor("idle");
                }
                break;
            case "waiting":
                card.Rank = 0;
                card.Subtitle = s.Message ?? L.WaitingForInput;
                card.TimeText = Elapsed(now - s.StartedAt);
                card.TimeBrush = BrushFor("waiting");
                break;
            case "done":
                card.Rank = 2;
                card.Subtitle = prompt;
                card.TimeText = L.DoneAt($"{s.FinishedAt!.Value.ToLocalTime():HH:mm}");
                card.TimeBrush = BrushFor("done");
                break;
            default:
                card.Rank = 3;
                card.Subtitle = prompt;
                card.TimeText = L.IdleFor(Short(now - (s.FinishedAt ?? s.UpdatedAt)));
                card.TimeBrush = BrushFor("idle");
                break;
        }

        var tip = new List<string>();
        if (!string.IsNullOrEmpty(s.Prompt)) tip.Add(s.Prompt);
        if (s.Status == "done" && s.FinishedAt is { } end) tip.Add(L.Duration(Short(end - s.StartedAt)));
        tip.Add(s.Cwd);
        tip.Add(L.CardHint);
        card.Tooltip = string.Join("\n", tip);
    }

    static TimeSpan? SilentFor(SessionState s, DateTime now)
    {
        try
        {
            if (string.IsNullOrEmpty(s.TranscriptPath) || !File.Exists(s.TranscriptPath)) return null;
            var last = File.GetLastWriteTimeUtc(s.TranscriptPath);
            return now - (last > s.UpdatedAt ? last : s.UpdatedAt);
        }
        catch { return null; }
    }

    void Sort()
    {
        var ordered = _cards.OrderBy(c => c.Rank)
                            .ThenBy(c => c.Project, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(c => c.SessionId)
                            .ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            int current = _cards.IndexOf(ordered[i]);
            if (current != i) _cards.Move(current, i);
        }
    }

    void UpdateHeader(int working, int waiting, int done, int idle)
    {
        SetCount(CountWaitingPanel, CountWaiting, waiting);
        SetCount(CountWorkingPanel, CountWorking, working);
        SetCount(CountDonePanel, CountDone, done);
        SetCount(CountIdlePanel, CountIdle, idle);
        CountNone.Visibility = _cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Visibility = _cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = Connected ? L.Empty : L.NotConnected;

        var aggregate = waiting > 0 ? "waiting" : working > 0 ? "working" : done > 0 ? "done" : "idle";
        HeaderDot.Fill = BrushFor(aggregate);

        var parts = new List<string>();
        if (waiting > 0) parts.Add(L.SummaryWaiting(waiting));
        if (working > 0) parts.Add(L.SummaryWorking(working));
        if (done > 0) parts.Add(L.SummaryDone(done));
        var tooltip = L.Title + (parts.Count > 0 ? " — " + string.Join(", ", parts) : "");

        AggregateChanged?.Invoke(aggregate, working + waiting, tooltip);
    }

    static void SetCount(FrameworkElement panel, System.Windows.Controls.TextBlock text, int value)
    {
        panel.Visibility = value > 0 ? Visibility.Visible : Visibility.Collapsed;
        text.Text = value.ToString();
    }

    // ---------- formatting ----------

    static string ProjectName(string cwd)
    {
        if (string.IsNullOrWhiteSpace(cwd)) return L.Unknown;
        var name = Path.GetFileName(cwd.TrimEnd('\\', '/'));
        return string.IsNullOrEmpty(name) ? cwd : name;
    }

    static string Elapsed(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes}:{t.Seconds:00}";
    }

    static string Short(TimeSpan t)
    {
        if (t < TimeSpan.FromMinutes(1)) return "<1m";
        if (t < TimeSpan.FromHours(1)) return $"{(int)t.TotalMinutes}m";
        return $"{(int)t.TotalHours}h{t.Minutes:00}";
    }

    Brush BrushFor(string key) => (Brush)FindResource(key switch
    {
        "working" => "Working",
        "waiting" => "Waiting",
        "done" => "Done",
        "fg" => "Fg",
        "muted" => "Muted",
        _ => "Idle",
    });

    // ---------- showing / hiding ----------

    /// <summary>Tray click: open the panel, or close it if it is open.</summary>
    public void TogglePanel()
    {
        if (IsVisible)
        {
            HidePanel();
            return;
        }
        // The click on the tray icon has just deactivated (and hidden) the panel: don't reopen it.
        if (DateTime.UtcNow - _hiddenAt < TimeSpan.FromMilliseconds(400)) return;
        ShowPanel();
    }

    public void ShowPanel()
    {
        if (!IsVisible)
        {
            _shownAt = DateTime.UtcNow;
            Opacity = 0;
            Show();
            UpdateLayout();
            if (Settings.Pinned) PlacePinned();
            else PlaceNearTray();
            Opacity = 1;
        }
        Activate();
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero) SetForegroundWindow(hwnd);
        KeepOnTop();
    }

    void HidePanel()
    {
        if (!IsVisible) return;
        Hide();
        _hiddenAt = DateTime.UtcNow;
    }

    /// <summary>Above the notification area, on whichever edge the taskbar is.</summary>
    void PlaceNearTray()
    {
        var area = SystemParameters.WorkArea;
        double screenW = SystemParameters.PrimaryScreenWidth, screenH = SystemParameters.PrimaryScreenHeight;
        bool taskbarTop = area.Top > 0;
        bool taskbarLeft = area.Left > 0;

        Left = taskbarLeft ? area.Left : area.Right - ActualWidth;
        Top = taskbarTop ? area.Top : area.Bottom - ActualHeight;
        // With an auto-hide or unusual taskbar the work area is the whole screen: keep a small gap.
        if (area.Bottom >= screenH && !taskbarTop) Top -= 40;
        if (area.Right >= screenW && !taskbarLeft) Left -= 4;
    }

    void PlacePinned()
    {
        var area = SystemParameters.WorkArea;
        double left = Settings.Left ?? area.Right - ActualWidth;
        double top = Settings.Top ?? area.Top;
        bool onScreen = left > SystemParameters.VirtualScreenLeft - 100
                     && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 60
                     && top > SystemParameters.VirtualScreenTop - 10
                     && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 40;
        Left = onScreen ? left : area.Right - ActualWidth;
        Top = onScreen ? top : area.Top;
    }

    public void SetPinned(bool pinned)
    {
        Settings.Pinned = pinned;
        if (pinned)
        {
            Settings.Left = Left;
            Settings.Top = Top;
        }
        Settings.Save();
        UpdatePinButton();
        if (pinned && !IsVisible) ShowPanel();
    }

    public void ToggleSound()
    {
        Settings.Sound = !Settings.Sound;
        Settings.Save();
        UpdateSoundButton();
    }

    void UpdateSoundButton()
    {
        SoundButton.Content = Settings.Sound ? "" : "";
        SoundButton.ToolTip = Settings.Sound ? L.SoundOnTip : L.SoundOffTip;
        SoundButton.Opacity = Settings.Sound ? 1 : 0.55;
    }

    void UpdatePinButton()
    {
        PinButton.Content = Settings.Pinned ? "" : "";
        PinButton.ToolTip = Settings.Pinned ? L.PinnedTip : L.UnpinnedTip;
        PinButton.Foreground = Settings.Pinned ? BrushFor("working") : BrushFor("muted");
    }

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);
    static readonly IntPtr HwndTopmost = new(-1);
    const uint SwpNoSize = 0x1, SwpNoMove = 0x2, SwpNoActivate = 0x10;

    /// <summary>Other topmost windows can end up above us: re-assert our place at the top.</summary>
    void KeepOnTop()
    {
        if (!IsVisible) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    public void ClearFinished()
    {
        foreach (var s in Store.LoadAll().Where(s => s.Status is "done" or "idle"))
            Store.Delete(s.SessionId);
        Refresh();
    }

    public void ExitApp()
    {
        _exiting = true;
        Settings.Save();
        Close();
        Application.Current.Shutdown();
    }

    void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try { DragMove(); } catch (InvalidOperationException) { }
        // Moving the panel means "keep it here".
        if (!Settings.Pinned) SetPinned(true);
        Settings.Left = Left;
        Settings.Top = Top;
        Settings.Save();
    }

    void SoundButton_Click(object sender, RoutedEventArgs e) => ToggleSound();
    void PinButton_Click(object sender, RoutedEventArgs e) => SetPinned(!Settings.Pinned);
    void HideButton_Click(object sender, RoutedEventArgs e) => HidePanel();

    // ---------- card actions ----------

    static CardViewModel? CardOf(object sender) => (sender as FrameworkElement)?.DataContext as CardViewModel;

    void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (CardOf(sender) is { } card) GoToSession(card);
    }

    void MenuOpenCode_Click(object sender, RoutedEventArgs e)
    {
        if (CardOf(sender) is { } card) GoToSession(card);
    }

    void MenuOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (CardOf(sender) is { } card && Directory.Exists(card.Cwd))
            Start("explorer.exe", $"\"{card.Cwd}\"");
    }

    void MenuRemove_Click(object sender, RoutedEventArgs e)
    {
        if (CardOf(sender) is not { } card) return;
        Store.Delete(card.SessionId);
        Refresh();
    }

    void GoToSession(CardViewModel card)
    {
        if (!Settings.Pinned) HidePanel();
        // Bring the existing VS Code / terminal window to the front; launch `code` only if the
        // project is not open anywhere (it would open a new window for a subfolder).
        if (!WindowFocus.FocusSession(card.Cwd, card.ClaudePid) && Directory.Exists(card.Cwd))
            Start("cmd.exe", $"/c code \"{card.Cwd}\"");
    }

    static void Start(string file, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false, CreateNoWindow = true });
        }
        catch { }
    }
}
