using System.Globalization;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace VanillaNotes.App.Services;

/// <summary>
/// Tells when a private profile is to be closed: after the time with nothing done in the window
/// that its settings chose, or at once when Windows is locked. Pressing a key, moving or clicking
/// the mouse, or touching the screen anywhere in the window is something done. Only one profile is
/// watched at a time, the one open.
/// </summary>
public sealed class IdleWatcher : IDisposable
{
    /// <summary>The setting that never closes the profile for the time gone by, only when Windows is locked.</summary>
    public const string Never = "never";

    /// <summary>How often the time with nothing done is looked at: often enough that a profile is closed within a quarter of a minute of its time.</summary>
    private static readonly TimeSpan _checkEvery = TimeSpan.FromSeconds(15);

    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    private readonly DispatcherTimer _timer;

    /// <summary>When the person last did something in the window.</summary>
    private DateTime _lastInput = DateTime.UtcNow;

    /// <summary>How long with nothing done closes the profile, or nothing when only locking Windows does.</summary>
    private TimeSpan? _after;

    /// <summary>What closes the profile watched, or nothing when none is.</summary>
    private Action? _onIdle;

    public IdleWatcher()
    {
        _timer = new DispatcherTimer(_checkEvery, DispatcherPriority.Background, (_, _) => Check(), _dispatcher);
        _timer.Stop();
        InputManager.Current.PreProcessInput += OnInput;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    /// <summary>
    /// Starts watching the profile open, in place of any other, from now.
    /// </summary>
    /// <param name="lockAfter">The minutes with nothing done that close it, as the setting keeps them, or never.</param>
    /// <param name="onIdle">What closes it, done once.</param>
    public void Watch(string lockAfter, Action onIdle)
    {
        _onIdle = onIdle;
        _after = lockAfter == Never ? null : TimeSpan.FromMinutes(int.Parse(lockAfter, CultureInfo.InvariantCulture));
        _lastInput = DateTime.UtcNow;
        _timer.Start();
    }

    /// <summary>Stops watching, once the profile is closed or has no password.</summary>
    public void Stop()
    {
        _onIdle = null;
        _timer.Stop();
    }

    public void Dispose()
    {
        _timer.Stop();
        InputManager.Current.PreProcessInput -= OnInput;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }

    private void OnInput(object sender, PreProcessInputEventArgs input)
    {
        if (input.StagingItem.Input is KeyboardEventArgs or MouseEventArgs or TouchEventArgs or StylusEventArgs)
        {
            _lastInput = DateTime.UtcNow;
        }
    }

    /// <summary>Windows tells of a lock on a thread of its own; the profile is closed on the thread of the window.</summary>
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs change)
    {
        if (change.Reason == SessionSwitchReason.SessionLock)
        {
            _dispatcher.BeginInvoke(Fire);
        }
    }

    private void Check()
    {
        if (_after is TimeSpan after && DateTime.UtcNow - _lastInput >= after)
        {
            Fire();
        }
    }

    private void Fire()
    {
        Action? onIdle = _onIdle;
        Stop();
        onIdle?.Invoke();
    }
}
