using System.Text.Json;
using System.Text.Json.Serialization;
using OpenDIAL.Pipeline.Vendor;

namespace OpenDIAL.Desktop.Services;

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

/// <summary>Persists <see cref="AppSettings"/> as JSON under %APPDATA%/OpenDIAL/settings.json (~/.config/OpenDIAL on macOS/Linux), or wherever OPENDIAL_SETTINGS_DIR points.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public SettingsService()
    {
        // OPENDIAL_SETTINGS_DIR points the settings somewhere else, so a scripted run of the installed
        // application neither reads nor rewrites the person's own recent projects and layout
        var overridden = Environment.GetEnvironmentVariable("OPENDIAL_SETTINGS_DIR");
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Folder = string.IsNullOrWhiteSpace(overridden) ? Path.Combine(root, "OpenDIAL") : overridden;
        FilePath = Path.Combine(Folder, "settings.json");
        Current = Load();
    }

    public string Folder { get; }
    public string FilePath { get; }
    public AppSettings Current { get; private set; }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
            }
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
