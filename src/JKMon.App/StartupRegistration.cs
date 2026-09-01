using System.IO;
using Microsoft.Win32;

namespace JKMon.App;

/// <summary>
/// Per-user autostart entry. HKCU needs no elevation, which keeps a portable copy self-sufficient, and the entry
/// always points at the executable that wrote it so moving the folder re-registers the new location.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "JKMon";

    /// <summary>Rewrites the entry on every start, which repairs it after the portable folder is moved.</summary>
    internal static void Apply(bool enabled)
    {
        var command = CommandLine();
        if (command is null)
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                key.SetValue(ValueName, command, RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            DiagnosticLog.Write($"startup registration failed: {ex.Message}");
        }
    }

    /// <summary>Quoted so a path containing spaces still launches.</summary>
    private static string? CommandLine() =>
        Environment.ProcessPath is { Length: > 0 } path ? $"\"{path}\"" : null;
}
