using System.Diagnostics;
using System.IO;

namespace JKMon.App.Update;

/// <summary>
/// Runs from the staged copy in the temporary folder and replaces the installed files. The previous build is kept
/// beside the app until the new one starts, so a failed swap can be undone.
/// </summary>
internal static class UpdateApplier
{
    private const string BackupFolderName = ".jkmon-previous";

    private static readonly TimeSpan GracefulExitTimeout = TimeSpan.FromSeconds(10);

    internal static int Run(UpdateArguments arguments)
    {
        var source = arguments.SourceDirectory!;
        var target = arguments.TargetDirectory!;
        var backup = Path.Combine(target, BackupFolderName);

        if (!WaitForExit(arguments.WaitForProcessId))
        {
            return 2;
        }

        try
        {
            UpdateDownloader.TryDelete(backup);
            Directory.CreateDirectory(backup);

            // Only files the release ships are touched, so settings.json and the log stay where the user left them.
            foreach (var incoming in Directory.GetFiles(source))
            {
                var name = Path.GetFileName(incoming);
                var existing = Path.Combine(target, name);

                if (File.Exists(existing))
                {
                    File.Move(existing, Path.Combine(backup, name), overwrite: true);
                }

                File.Copy(incoming, existing, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Rollback(backup, target);
            return 3;
        }

        var relaunched = Relaunch(Path.Combine(target, "JKMon.exe"), arguments.WorkDirectory);
        if (!relaunched)
        {
            Rollback(backup, target);
            Relaunch(Path.Combine(target, "JKMon.exe"), null);
            return 4;
        }

        UpdateDownloader.TryDelete(backup);
        return 0;
    }

    /// <summary>A process id that no longer exists means the app has already gone, which is the state we need.</summary>
    private static bool WaitForExit(int processId)
    {
        if (processId <= 0)
        {
            return true;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.WaitForExit((int)GracefulExitTimeout.TotalMilliseconds))
            {
                return true;
            }

            process.Kill(entireProcessTree: true);
            return process.WaitForExit((int)GracefulExitTimeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>The relaunched app deletes the staging folder, which this process cannot delete while running in it.</summary>
    private static bool Relaunch(string executable, string? workDirectory)
    {
        try
        {
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! };
            if (UpdateDownloader.IsStagingRoot(workDirectory))
            {
                info.ArgumentList.Add(UpdateArguments.CleanupSwitch);
                info.ArgumentList.Add(workDirectory!);
            }

            using var started = Process.Start(info);
            return started is not null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }

    private static void Rollback(string backup, string target)
    {
        try
        {
            if (!Directory.Exists(backup))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(backup))
            {
                File.Move(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
            }

            Directory.Delete(backup, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing further can be done from here; the backup folder stays for manual recovery.
        }
    }
}
