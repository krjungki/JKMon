using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

// File-level alias beats the WPF Shapes import that brings in a different Path type.
using Path = System.IO.Path;

namespace JKMon.App;

/// <summary>
/// Loads provider icons from the installed applications, so no third-party artwork ships with this repository and
/// the overlay always shows whatever icon the installed version uses.
/// </summary>
internal static class ProviderIconResolver
{
    private const string SyncRootManagerKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\SyncRootManager";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHDefExtractIconW(
        string iconFile, int index, uint flags, out IntPtr largeIcon, out IntPtr smallIcon, uint iconSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    internal static ImageSource? Resolve(string providerId, int pixelSize)
    {
        var key = $"{providerId}:{pixelSize}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var source = Load(providerId, pixelSize);
        Cache[key] = source;
        return source;
    }

    private static ImageSource? Load(string providerId, int pixelSize)
    {
        var location = providerId switch
        {
            "onedrive" => LocateOneDrive(),
            "syncthing" => LocateRunningProcess("syncthing"),
            "gsa" => LocateRunningProcess("GlobalSecureAccessClient"),
            _ => null
        };

        return location is null ? null : Extract(location.Value.Path, location.Value.Index, pixelSize);
    }

    private static ImageSource? Extract(string path, int index, int pixelSize)
    {
        var large = IntPtr.Zero;
        var small = IntPtr.Zero;
        try
        {
            // The low word requests the large icon size and the high word the small one.
            var requested = (uint)(pixelSize | (16 << 16));
            if (SHDefExtractIconW(path, index, 0, out large, out small, requested) != 0 || large == IntPtr.Zero)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                large, System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or DllNotFoundException)
        {
            return null;
        }
        finally
        {
            if (large != IntPtr.Zero)
            {
                DestroyIcon(large);
            }

            if (small != IntPtr.Zero)
            {
                DestroyIcon(small);
            }
        }
    }

    private static (string Path, int Index)? LocateOneDrive()
    {
        var registered = OneDriveIconFromRegistry();
        if (registered is not null)
        {
            return registered;
        }

        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft OneDrive", "OneDrive.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft OneDrive", "OneDrive.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "OneDrive", "OneDrive.exe")
        ];

        var found = candidates.FirstOrDefault(File.Exists);
        return found is null ? null : (found, 0);
    }

    private static (string Path, int Index)? OneDriveIconFromRegistry()
    {
        try
        {
            using var manager = Registry.LocalMachine.OpenSubKey(SyncRootManagerKey);
            if (manager is null)
            {
                return null;
            }

            foreach (var name in manager.GetSubKeyNames())
            {
                if (!name.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var root = manager.OpenSubKey(name);
                if (root?.GetValue("IconResource") is string resource)
                {
                    return ParseIconResource(resource);
                }
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }

        return null;
    }

    /// <summary>Shell icon references look like "C:\path\app.exe,-501".</summary>
    private static (string Path, int Index)? ParseIconResource(string resource)
    {
        var separator = resource.LastIndexOf(',');
        if (separator < 0)
        {
            return File.Exists(resource) ? (resource, 0) : null;
        }

        var path = resource[..separator].Trim().Trim('"');
        if (!File.Exists(path) || !int.TryParse(resource[(separator + 1)..].Trim(), out var index))
        {
            return null;
        }

        return (path, index);
    }

    private static (string Path, int Index)? LocateRunningProcess(string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                var file = process.MainModule?.FileName;
                if (file is not null && File.Exists(file))
                {
                    return (file, 0);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // A process we cannot open simply contributes no icon.
            }
            finally
            {
                process.Dispose();
            }
        }

        return null;
    }
}
