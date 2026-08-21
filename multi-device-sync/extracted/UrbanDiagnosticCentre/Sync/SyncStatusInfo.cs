using System.ComponentModel;

namespace UrbanDiagnosticCentre.Sync;

public enum SyncState { Idle, Syncing, Offline, Retrying, Error }

/// <summary>
/// Observable singleton that ViewModels can bind to for sync status display.
/// </summary>
public sealed class SyncStatusInfo : INotifyPropertyChanged
{
    public static readonly SyncStatusInfo Instance = new();

    private SyncState _state = SyncState.Idle;
    private string    _message = "Sync idle";
    private int       _pendingCount;
    private DateTime? _lastSyncAt;
    private int       _consecutiveFailures;
    private TimeSpan  _currentRetryDelay;

    private SyncStatusInfo() { }

    public SyncState State
    {
        get => _state;
        set { _state = value; OnPropertyChanged(nameof(State)); OnPropertyChanged(nameof(StateLabel)); }
    }

    public string Message
    {
        get => _message;
        set { _message = value; OnPropertyChanged(nameof(Message)); }
    }

    public int PendingCount
    {
        get => _pendingCount;
        set { _pendingCount = value; OnPropertyChanged(nameof(PendingCount)); }
    }

    public DateTime? LastSyncAt
    {
        get => _lastSyncAt;
        set { _lastSyncAt = value; OnPropertyChanged(nameof(LastSyncAt)); OnPropertyChanged(nameof(LastSyncAtDisplay)); }
    }

    public int ConsecutiveFailures
    {
        get => _consecutiveFailures;
        set { _consecutiveFailures = value; OnPropertyChanged(nameof(ConsecutiveFailures)); }
    }

    public TimeSpan CurrentRetryDelay
    {
        get => _currentRetryDelay;
        set { _currentRetryDelay = value; OnPropertyChanged(nameof(CurrentRetryDelay)); OnPropertyChanged(nameof(RetryDelayDisplay)); }
    }

    public string LastSyncAtDisplay => _lastSyncAt.HasValue
        ? _lastSyncAt.Value.ToString("hh:mm:ss tt")
        : "Never";

    public string RetryDelayDisplay => _currentRetryDelay.TotalSeconds > 0
        ? $"Retry in {_currentRetryDelay.TotalSeconds:0}s"
        : string.Empty;

    public string StateLabel => State switch
    {
        SyncState.Idle     => "Synced",
        SyncState.Syncing  => "Syncing…",
        SyncState.Offline  => "Offline",
        SyncState.Retrying => "Retry scheduled",
        SyncState.Error    => "Sync error",
        _                  => "Unknown"
    };

    public void SetOn(Action action)
    {
        // Dispatch to UI thread if needed (WPF).
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            System.Windows.Application.Current.Dispatcher.Invoke(action);
        else
            action();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        SetOn(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));
}
