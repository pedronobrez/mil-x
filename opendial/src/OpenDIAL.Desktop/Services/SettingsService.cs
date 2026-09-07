using System.Text.Json;
using System.Text.Json.Serialization;
using OpenDIAL.Pipeline.Vendor;

namespace OpenDIAL.Desktop.Services;

public sealed class AppSettings
{
    public VendorConversionOptions VendorConversion { get; set; } = new();
    public string LastInputFolder { get; set; } = string.Empty;
    public string LastOutputFolder { get; set; } = string.Empty;
    public string LastMspFile { get; set; } = string.Empty;
    public string LastMethodFile { get; set; } = string.Empty;
}

/// <summary>Persists <see cref="AppSettings"/> as JSON under %APPDATA%/OpenDIAL/settings.json (~/.config/OpenDIAL on macOS/Linux).</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public SettingsService()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Folder = Path.Combine(root, "OpenDIAL");
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

    public async Task SaveAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(Folder);
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        await File.WriteAllTextAsync(FilePath, json, ct).ConfigureAwait(false);
    }
}
