using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LePaSentinel;

/// <summary>Product identity and install-dependent paths.</summary>
public static class AppInfo
{
    public const string Name = "LePa Sentinel";
    public const string ExeName = "LePaSentinel.exe";
    public const string ProjectUrl = "https://github.com/emanueleparini/LePa-Sentinel";

    /// <summary>Must match the TaskId of the StartupTask in packaging/msix/AppxManifest.xml.</summary>
    public const string StartupTaskId = "LePaSentinelStartup";

    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    /// <summary>True when running from the MSIX package (Microsoft Store).</summary>
    public static bool IsPackaged { get; } = DetectPackage();

    /// <summary>
    /// Path written into the Claude Code hooks. The MSIX install folder changes with every update,
    /// so the packaged app is invoked through its stable app execution alias instead.
    /// </summary>
    public static string HookExePath => IsPackaged
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                       "Microsoft", "WindowsApps", ExeName)
        : Environment.ProcessPath!;

    /// <summary>Claude Code's config folder (honours CLAUDE_CONFIG_DIR like Claude Code does).</summary>
    public static string ClaudeDir =>
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    static bool DetectPackage()
    {
        try
        {
            int length = 0;
            // ERROR_INSUFFICIENT_BUFFER (122) means there is a package name to return.
            return GetCurrentPackageFullName(ref length, IntPtr.Zero) == 122;
        }
        catch { return false; }
    }

    [DllImport("kernel32.dll")]
    static extern int GetCurrentPackageFullName(ref int packageFullNameLength, IntPtr packageFullName);
}
