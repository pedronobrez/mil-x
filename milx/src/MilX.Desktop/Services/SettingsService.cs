using System.Text.Json;
using System.Text.Json.Serialization;
using MilX.Pipeline.Vendor;

namespace MilX.Desktop.Services;

public sealed class RecentProject
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public sealed class AppSettings
{
    public VendorConversionOptions VendorConversion { get; set; } = new();
    public string LastInputFolder { get; set; } = string.Empty;
    public string LastOutputFolder { get; set; } = string.Empty;
    public string LastProjectFolder { get; set; } = string.Empty;
    public string LastMspFile { get; set; } = string.Empty;
    public string LastMethodFile { get; set; } = string.Empty;
    /// <summary>"System", "Light" or "Dark".</summary>
    public string Theme { get; set; } = "System";
    /// <summary>The language the manual opens in: "en" or "pt".</summary>
    public string HelpLanguage { get; set; } = "en";
    public List<RecentProject> RecentProjects { get; set; } = new();
    public bool ShowLog { get; set; } = false;
    /// <summary>Ion table column headers in the order the reviewer arranged them.</summary>
    public List<string> IonTableColumnOrder { get; set; } = new();
    /// <summary>The ion table was last used in its own window.</summary>
    public bool IonTableDetached { get; set; } = false;
    /// <summary>How the last figure was exported, which is what the next export dialog opens with.</summary>
    public Charts.ChartExportOptions ChartExport { get; set; } = new();
}

/// <summary>
/// Persists <see cref="AppSettings"/> as JSON under %APPDATA%/MIL-X/settings.json (~/.config/MIL-X on
/// macOS/Linux), or wherever MILX_SETTINGS_DIR points. Until 1.0 the folder was named OpenDIAL; the
/// first start of 1.0 reads that file when there is no new one, so recent projects, the theme and
/// the column layout survive the rename. The old folder is left as it is.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public SettingsService()
    {
        // MILX_SETTINGS_DIR points the settings somewhere else, so a scripted run of the installed
        // application neither reads nor rewrites the person's own recent projects and layout
        var overridden = Environment.GetEnvironmentVariable("MILX_SETTINGS_DIR");
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Folder = string.IsNullOrWhiteSpace(overridden) ? Path.Combine(root, "MIL-X") : overridden;
        FilePath = Path.Combine(Folder, "settings.json");
        LegacyFilePath = string.IsNullOrWhiteSpace(overridden) ? Path.Combine(root, "OpenDIAL", "settings.json") : null;
        Current = Load();
    }

    public string Folder { get; }
    public string FilePath { get; }

    /// <summary>Where OpenDIAL kept the same file; read once, when the new one does not exist yet.</summary>
    public string? LegacyFilePath { get; }

    /// <summary>True when this start took its settings from the OpenDIAL folder.</summary>
    public bool MigratedFromOpenDial { get; private set; }

    public AppSettings Current { get; private set; }

    private AppSettings Load()
    {
        var source = File.Exists(FilePath) ? FilePath : LegacyFilePath is not null && File.Exists(LegacyFilePath) ? LegacyFilePath : null;
        if (source is null) return new AppSettings();
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(source), JsonOptions) ?? new AppSettings();
            MigratedFromOpenDial = source == LegacyFilePath;
            return settings;
        }
        catch
        {
            // corrupt settings are ignored; defaults are used
        }
        return new AppSettings();
    }

    public void AddRecent(string path, string name)
    {
        Current.RecentProjects.RemoveAll(r => string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase));
        Current.RecentProjects.Insert(0, new RecentProject { Name = name, Path = path });
        if (Current.RecentProjects.Count > 10)
        {
            Current.RecentProjects.RemoveRange(10, Current.RecentProjects.Count - 10);
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(Folder);
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        await File.WriteAllTextAsync(FilePath, json, ct).ConfigureAwait(false);
    }
}
