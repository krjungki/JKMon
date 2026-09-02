using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using JKMon.Core.Settings;
using JKMon.Core.Update;

using MessageBox = System.Windows.MessageBox;

namespace JKMon.App.Update;

internal enum UpdateOutcome
{
    UpToDate,
    CheckFailed,
    Declined,
    StagingFailed,
    Applying
}

/// <summary>Ties the version check, the user's decision and the staged swap together.</summary>
internal sealed class UpdateService : IDisposable
{
    private readonly HttpClient _http;
    private readonly UpdateChecker _checker;

    internal UpdateService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10),
            // The download follows GitHub's redirect to its asset host and nowhere else.
            DefaultRequestVersion = new Version(2, 0)
        };

        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"JKMon/{Current}");
        _checker = new UpdateChecker(_http);
    }

    internal static ReleaseVersion Current =>
        ReleaseVersion.TryParse(
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            out var version)
            ? version
            : ReleaseVersion.Zero;

    internal async Task<UpdateOutcome> RunAsync(bool announceWhenCurrent, CancellationToken cancellationToken)
    {
        var latest = await _checker.TryGetLatestAsync(cancellationToken).ConfigureAwait(true);
        if (latest is null)
        {
            if (announceWhenCurrent)
            {
                Say("업데이트를 확인하지 못했습니다. 네트워크를 확인한 뒤 다시 시도해 주세요.", MessageBoxImage.Warning);
            }

            return UpdateOutcome.CheckFailed;
        }

        var info = latest.Value;
        if (info.Version <= Current)
        {
            if (announceWhenCurrent)
            {
                Say($"이미 최신 버전입니다. (현재 {Current})", MessageBoxImage.Information);
            }

            return UpdateOutcome.UpToDate;
        }

        var answer = MessageBox.Show(
            $"새 버전 {info.Version}이 있습니다. 현재 버전은 {Current}입니다.\n\n" +
            "지금 업데이트할까요? 앱이 잠시 종료된 뒤 다시 실행됩니다.",
            "JKMon 업데이트",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            return UpdateOutcome.Declined;
        }

        var staged = await new UpdateDownloader(_http).TryStageAsync(info, cancellationToken).ConfigureAwait(true);
        if (staged is null)
        {
            Say("업데이트 파일을 내려받거나 검증하지 못했습니다. 설치된 버전은 그대로입니다.", MessageBoxImage.Warning);
            return UpdateOutcome.StagingFailed;
        }

        if (!Launch(staged))
        {
            UpdateDownloader.TryDelete(staged.WorkDirectory);
            Say("업데이트 프로그램을 실행하지 못했습니다. 설치된 버전은 그대로입니다.", MessageBoxImage.Warning);
            return UpdateOutcome.StagingFailed;
        }

        return UpdateOutcome.Applying;
    }

    /// <summary>Runs the staged build from the temporary folder so the installed files are free to be replaced.</summary>
    private static bool Launch(StagedUpdate staged)
    {
        try
        {
            var info = new ProcessStartInfo(Path.Combine(staged.StagedDirectory, "JKMon.exe"))
            {
                UseShellExecute = false,
                WorkingDirectory = staged.StagedDirectory
            };

            info.ArgumentList.Add(UpdateArguments.ApplySwitch);
            info.ArgumentList.Add("--source");
            info.ArgumentList.Add(staged.StagedDirectory);
            info.ArgumentList.Add("--target");
            info.ArgumentList.Add(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            info.ArgumentList.Add("--work");
            info.ArgumentList.Add(staged.WorkDirectory);
            info.ArgumentList.Add("--pid");
            info.ArgumentList.Add(Environment.ProcessId.ToString());

            using var started = Process.Start(info);
            return started is not null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            DiagnosticLog.Write($"update: could not start the applier: {ex.Message}");
            return false;
        }
    }

    private static void Say(string message, MessageBoxImage icon) =>
        MessageBox.Show(message, "JKMon 업데이트", MessageBoxButton.OK, icon);

    internal static bool IsDue(JkMonSettings settings, DateTimeOffset appStartedUtc) => UpdateSchedule.IsDue(
        settings.UpdateCheck,
        settings.LastUpdateCheckUtc,
        appStartedUtc,
        DateTimeOffset.UtcNow,
        settings.CheckUpdatesOnStartup);

    public void Dispose() => _http.Dispose();
}
