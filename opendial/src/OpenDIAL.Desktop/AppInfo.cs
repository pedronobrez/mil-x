using System.Reflection;
using OpenDIAL.Pipeline;

namespace OpenDIAL.Desktop;

/// <summary>What this build is, read off the assembly so it is stamped once, in Directory.Build.props.</summary>
public static class AppInfo
{
    public static string Version { get; } = Read();

    /// <summary>The MS-DIAL release the engine is taken from.</summary>
    public static string UpstreamVersion => PipelineRunner.UpstreamVersion;

    private static string Read()
    {
        var informational = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // the SDK may append "+<commit>"; the number is what a person needs
            var plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }
        return typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
