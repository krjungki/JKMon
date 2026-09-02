using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using JKMon.App.Interop;
using JKMon.App.Update;
using JKMon.Core;
using JKMon.Core.Metrics;
using JKMon.Core.Presentation;
using JKMon.Core.Settings;
using JKMon.Core.Sync;
using JKMon.Core.Update;

namespace JKMon.App;

/// <summary>
/// Owns the refresh loop and every window. There is no <c>Application</c> subclass to derive from any more: this
/// runs as a WinForms message loop with no main form, because the overlay is layered and the tray icon has no window.
/// </summary>
internal sealed class JkMonContext : ApplicationContext
{
    private readonly SystemMetricsCollector _collector = new();
    private readonly SyncthingSyncProvider _syncthing = new();
    private readonly System.Windows.Forms.Timer _timer = new();

    private readonly SettingsStore _store;
    private readonly MonitorEngine _engine;
    private readonly OverlayForm _overlay;
    private readonly TrayController _tray;

    private SettingsForm? _settingsForm;
    private IReadOnlyList<string> _presentProviders = [];
    private JkMonSettings _settings;

    /// <summary>When this run began, which is what keeps a launch from bypassing the start-up preference.</summary>
    private readonly DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;

    /// <summary>Not persisted: a restart is a deliberate act and may retry immediately.</summary>
    private DateTimeOffset _retryNotBefore = DateTimeOffset.MinValue;

    /// <summary>The start-up check ignores the interval, so it must only happen once per run.</summary>
    private bool _checkedThisRun;

    private bool _refreshing;
    private bool _updating;

    /// <summary>Whether the user wants the overlay shown. Full-screen suppression hides the window without
    /// changing this, so the tray toggle and the resume both know what to go back to.</summary>
    private bool _overlayVisible = true;

    private bool _fullscreenSuppressed;

    internal JkMonContext(UpdateArguments arguments)
    {
        // Leftovers from an earlier update are swept even when this start is not the one that followed a swap.
        UpdateDownloader.ScheduleCleanup(arguments.CleanupDirectory);

        _store = new SettingsStore();
        _settings = _store.Load();

        // Re-registering here repoints the entry when the portable folder has been moved since the last run.
        StartupRegistration.Apply(_settings.StartWithWindows);

        // Architecture matters here: this build is x64, so on Windows on ARM it runs emulated and some system
        // libraries behave differently. A diagnostic report is useless without it.
        DiagnosticLog.Write(
            $"start {typeof(JkMonContext).Assembly.GetName().Version} process={RuntimeInformation.ProcessArchitecture} " +
            $"os={RuntimeInformation.OSArchitecture} {RuntimeInformation.OSDescription}");

        _engine = new MonitorEngine(
            () => _collector.Read(),
            [new OneDriveSyncProvider(log: DiagnosticLog.Write), _syncthing, new GlobalSecureAccessSyncProvider()])
        {
            Settings = _settings
        };

        _overlay = new OverlayForm();
        _overlay.Show();
        _overlay.ApplySettings(_settings);

        _tray = new TrayController();
        _tray.LayerChanged += layer => UpdateSettings(_settings with { Layer = layer });
        _tray.RefreshIntervalChanged += seconds => UpdateSettings(_settings with { RefreshSeconds = seconds });
        _tray.VisibilityToggled += ToggleOverlay;
        _tray.SettingsRequested += ShowSettings;
        _tray.UpdateCheckRequested += () => _ = CheckForUpdatesAsync(manual: true);
        _tray.ExitRequested += Quit;
        _tray.Sync(_settings, overlayVisible: true);

        _timer.Interval = _settings.RefreshSeconds * 1000;
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
                Quit();
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
        if (_refreshing)
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
            _tray.ShowStatus(model);
            _presentProviders = [.. model.Circles.Select(circle => circle.ProviderId)];
            _settingsForm?.SetPresentProviders(_presentProviders);

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
        var overlayMonitor = ForegroundWindowInterop.MonitorOf(_overlay.Handle);

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
            _overlay.Hide();
        }
        else
        {
            // The counters accumulated through the pause, so they are re-primed before anything is shown again.
            _collector.ResetBaseline();
            if (_overlayVisible)
            {
                _overlay.Show();
            }
        }

        DiagnosticLog.Write($"fullscreen suppression {(suppress ? "on" : "off")}");
        return suppress;
    }

    private void UpdateSettings(JkMonSettings settings)
    {
        var wasAutoStart = _settings.StartWithWindows;
        _settings = settings.Normalized();
        _store.Save(_settings);

        if (_settings.StartWithWindows != wasAutoStart)
        {
            StartupRegistration.Apply(_settings.StartWithWindows);
        }

        _engine.Settings = _settings;
        _timer.Interval = _settings.RefreshSeconds * 1000;

        _overlay.ApplySettings(_settings);
        _tray.Sync(_settings, _overlay.Visible);
    }

    private void ToggleOverlay()
    {
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

        _tray.Sync(_settings, _overlayVisible);
    }

    private void ShowSettings()
    {
        if (_settingsForm is not null)
        {
            _settingsForm.Activate();
            return;
        }

        var form = new SettingsForm(_settings);
        form.SetPresentProviders(_presentProviders);
        form.SettingsChanged += UpdateSettings;
        form.ThemeChangeRequested += SwitchTheme;
        form.ThemeLoadRequested += settings =>
        {
            UpdateSettings(settings);
            Relaunch();
        };

        form.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm = form;
        form.Show();
    }

    /// <summary>
    /// Both windows measure themselves from the theme's font when they are built, so a switch restarts rather
    /// than re-skinning. The settings are written first, which is what the new process reads on the way up.
    /// </summary>
    private void SwitchTheme(AppTheme theme)
    {
        UpdateSettings(ThemeCatalog.Apply(_settings, theme));
        Relaunch();
    }

    private void Relaunch()
    {
        var executable = Environment.ProcessPath;
        if (executable is null)
        {
            DiagnosticLog.Write("theme switch: no process path, staying on the current theme");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            DiagnosticLog.Write($"theme switch: relaunch failed: {ex.Message}");
            return;
        }

        Quit();
    }

    private void Quit()
    {
        _timer.Stop();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _tray.Dispose();
            _settingsForm?.Dispose();
            _overlay.Dispose();
            _syncthing.Dispose();
            _collector.Dispose();
        }

        base.Dispose(disposing);
    }
}
