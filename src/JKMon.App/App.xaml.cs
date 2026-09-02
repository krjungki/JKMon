using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using JKMon.App.Interop;
using JKMon.App.Update;
using JKMon.Core;
using JKMon.Core.Metrics;
using JKMon.Core.Presentation;
using JKMon.Core.Settings;
using JKMon.Core.Sync;
using JKMon.Core.Update;

namespace JKMon.App;

public partial class App : System.Windows.Application
{
    private SystemMetricsCollector? _collector;
    private MonitorEngine? _engine;
    private SyncthingSyncProvider? _syncthing;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;

    private IReadOnlyList<string> _presentProviders = [];
    private TrayController? _tray;
    private DispatcherTimer? _timer;
    private SettingsStore? _store;
    private JkMonSettings _settings = new();
    private bool _refreshing;
    private bool _updating;

    /// <summary>When this run began, which is what keeps a launch from bypassing the start-up preference.</summary>
    private readonly DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;

    /// <summary>Not persisted: a restart is a deliberate act and may retry immediately.</summary>
    private DateTimeOffset _retryNotBefore = DateTimeOffset.MinValue;

    /// <summary>The start-up check ignores the interval, so it must only happen once per run.</summary>
    private bool _checkedThisRun;

    /// <summary>Whether the user wants the overlay shown. Full-screen suppression hides the window without
    /// changing this, so the tray toggle and the resume both know what to go back to.</summary>
    private bool _overlayVisible = true;

    private bool _fullscreenSuppressed;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Hardware rendering makes this process hold a Direct3D device and the display driver's user-mode DLLs for
        // as long as it runs. On a laptop with switchable graphics that is a resident claim on the current adapter,
        // and it is worth nothing here: the overlay is a per-pixel alpha layered window of text and a few shapes.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        var arguments = UpdateArguments.Parse(e.Args);
        if (arguments.IsApply)
        {
            // This instance is the staged build swapping files for the installed one; it shows no interface.
            Environment.Exit(UpdateApplier.Run(arguments));
            return;
        }

        // Leftovers from an earlier update are swept even when this start is not the one that followed a swap.
        UpdateDownloader.ScheduleCleanup(arguments.CleanupDirectory);

        _store = new SettingsStore();
        _settings = _store.Load();

        // Re-registering here repoints the entry when the portable folder has been moved since the last run.
        StartupRegistration.Apply(_settings.StartWithWindows);

        _collector = new SystemMetricsCollector();
        _syncthing = new SyncthingSyncProvider();

        // Architecture matters here: this build is x64, so on Windows on ARM it runs emulated and some system
        // libraries behave differently. A diagnostic report is useless without it.
        DiagnosticLog.Write(
            $"start {typeof(App).Assembly.GetName().Version} process={RuntimeInformation.ProcessArchitecture} " +
            $"os={RuntimeInformation.OSArchitecture} {RuntimeInformation.OSDescription}");

        _engine = new MonitorEngine(
            () => _collector.Read(),
            [new OneDriveSyncProvider(log: DiagnosticLog.Write), _syncthing, new GlobalSecureAccessSyncProvider()])
        {
            Settings = _settings
        };

        _overlay = new OverlayWindow();
        _overlay.Show();
        _overlay.ApplySettings(_settings);

        _tray = new TrayController();
        _tray.LayerChanged += layer => UpdateSettings(_settings with { Layer = layer });
        _tray.RefreshIntervalChanged += seconds => UpdateSettings(_settings with { RefreshSeconds = seconds });
        _tray.VisibilityToggled += ToggleOverlay;
        _tray.SettingsRequested += ShowSettings;
        _tray.UpdateCheckRequested += () => _ = CheckForUpdatesAsync(manual: true);
        _tray.ExitRequested += Shutdown;
        _tray.Sync(_settings, overlayVisible: true);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_settings.RefreshSeconds) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        _ = RefreshAsync();

        if (UpdateService.IsDue(_settings, _startedUtc, _checkedThisRun))
        {
            _ = CheckForUpdatesAsync(manual: false);
        }
    }

    /// <summary>A manual check always reports its result; a scheduled one stays silent unless an update exists.</summary>
    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (_updating)
        {
            return;
        }

        if (!manual && (DateTimeOffset.UtcNow < _retryNotBefore
            || !UpdateService.IsDue(_settings, _startedUtc, _checkedThisRun)))
        {
            return;
        }

        _updating = true;
        _checkedThisRun = true;
        try
        {
            using var service = new UpdateService();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));

            var outcome = await service.RunAsync(announceWhenCurrent: manual, timeout.Token);

            // Recording a failure as a completed check would postpone the next one by a whole interval, so a single
            // network blip could hide an update for a day. Failures back off in memory instead.
            if (outcome == UpdateOutcome.CheckFailed)
            {
                _retryNotBefore = DateTimeOffset.UtcNow + UpdateSchedule.RetryAfterFailure;
                DiagnosticLog.Write($"update: check failed, next attempt no earlier than {_retryNotBefore:O}");
            }
            else
            {
                _retryNotBefore = DateTimeOffset.MinValue;
                UpdateSettings(_settings with { LastUpdateCheckUtc = DateTimeOffset.UtcNow });
            }

            if (outcome == UpdateOutcome.Applying)
            {
                // The applier is waiting for this process to exit before it replaces the files.
                Shutdown();
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"update check failed: {ex}");
        }
        finally
        {
            _updating = false;
        }
    }

    private async Task RefreshAsync()
    {
        // A slow provider poll must not queue up overlapping refreshes.
        if (_refreshing || _engine is null || _overlay is null)
        {
            return;
        }

        _refreshing = true;
        try
        {
            if (UpdateFullscreenSuppression())
            {
                // Nothing is sampled while a full-screen app owns the display, which is the point of pausing.
                return;
            }

            var model = await _engine.RefreshAsync();
            _overlay.Update(model);
            _tray?.ShowStatus(model);
            _presentProviders = [.. model.Circles.Select(circle => circle.ProviderId)];
            _settingsWindow?.SetPresentProviders(_presentProviders);

            if (UpdateService.IsDue(_settings, _startedUtc, _checkedThisRun))
            {
                _ = CheckForUpdatesAsync(manual: false);
            }
        }
        catch (Exception ex)
        {
            // Never let a background failure tear down the UI thread.
            DiagnosticLog.Write($"refresh failed: {ex}");
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>Returns true while sampling should stay paused. Only the transitions touch the overlay.</summary>
    private bool UpdateFullscreenSuppression()
    {
        var (isShell, foregroundMonitor, window, bounds) = ForegroundWindowInterop.Foreground();
        var overlayMonitor = ForegroundWindowInterop.MonitorOf(OverlayWindowInterop.GetHandle(_overlay!));

        var suppress = FullscreenGate.ShouldSuppress(
            _settings.PauseWhenFullscreen,
            ForegroundWindowInterop.NotificationState(),
            isShell,
            overlayMonitor != IntPtr.Zero && foregroundMonitor == overlayMonitor,
            window,
            bounds);

        if (suppress == _fullscreenSuppressed)
        {
            return suppress;
        }

        _fullscreenSuppressed = suppress;
        if (suppress)
        {
            _overlay?.Hide();
        }
        else
        {
            // The counters accumulated through the pause, so they are re-primed before anything is shown again.
            _collector?.ResetBaseline();
            if (_overlayVisible)
            {
                _overlay?.Show();
            }
        }

        DiagnosticLog.Write($"fullscreen suppression {(suppress ? "on" : "off")}");
        return suppress;
    }

    private void UpdateSettings(JkMonSettings settings)
    {
        var wasAutoStart = _settings.StartWithWindows;
        _settings = settings.Normalized();
        _store?.Save(_settings);

        if (_settings.StartWithWindows != wasAutoStart)
        {
            StartupRegistration.Apply(_settings.StartWithWindows);
        }

        if (_engine is not null)
        {
            _engine.Settings = _settings;
        }

        if (_timer is not null)
        {
            _timer.Interval = TimeSpan.FromSeconds(_settings.RefreshSeconds);
        }

        _overlay?.ApplySettings(_settings);
        _tray?.Sync(_settings, _overlay?.IsVisible ?? false);
    }

    private void ToggleOverlay()
    {
        if (_overlay is null)
        {
            return;
        }

        _overlayVisible = !_overlayVisible;

        if (!_overlayVisible)
        {
            _overlay.Hide();
        }
        else if (!_fullscreenSuppressed)
        {
            _overlay.Show();
            _overlay.ApplySettings(_settings);
        }

        _tray?.Sync(_settings, _overlayVisible);
    }

    private void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var window = new SettingsWindow(_settings);
        window.SetPresentProviders(_presentProviders);
        window.SettingsChanged += UpdateSettings;
        window.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _timer?.Stop();
        _tray?.Dispose();
        _engine?.Dispose();
        _syncthing?.Dispose();
        _collector?.Dispose();
        base.OnExit(e);
    }
}

