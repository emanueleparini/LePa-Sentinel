using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace LePaSentinel;

public sealed class CardViewModel : INotifyPropertyChanged
{
    public CardViewModel(string sessionId) => SessionId = sessionId;

    public string SessionId { get; }
    public string Cwd { get; set; } = "";
    public int? ClaudePid { get; set; }
    public int Rank { get; set; }

    string _project = "", _subtitle = "", _timeText = "", _tooltip = "";
    Brush _statusBrush = Brushes.Gray, _timeBrush = Brushes.Gray;
    bool _isWorking, _isWaiting;

    public string Project { get => _project; set => Set(ref _project, value); }
    public string Subtitle { get => _subtitle; set => Set(ref _subtitle, value); }
    public string TimeText { get => _timeText; set => Set(ref _timeText, value); }
    public string Tooltip { get => _tooltip; set => Set(ref _tooltip, value); }
    public Brush StatusBrush { get => _statusBrush; set => Set(ref _statusBrush, value); }
    public Brush TimeBrush { get => _timeBrush; set => Set(ref _timeBrush, value); }
    public bool IsWorking { get => _isWorking; set => Set(ref _isWorking, value); }
    public bool IsWaiting { get => _isWaiting; set => Set(ref _isWaiting, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
