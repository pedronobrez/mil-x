using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenDIAL.Desktop.Services;

namespace OpenDIAL.Desktop.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService? _settings;

    public SettingsWindow() : this(null)
    {
    }

    public SettingsWindow(SettingsService? settings)
    {
        InitializeComponent();
        _settings = settings;
        if (settings is not null)
        {
            var v = settings.Current.VendorConversion;
            MsconvertPath.Text = v.MsconvertPath;
            UseDocker.IsChecked = v.UseDocker;
            DockerImage.Text = v.DockerImage;
            CacheFolder.Text = v.ConversionCacheFolder;
            SettingsPath.Text = "Stored in " + settings.FilePath;
        }
    }

    private async void OnBrowseMsconvert(object? sender, RoutedEventArgs e)
    {
        var dialogs = new FileDialogService(this);
        var files = await dialogs.PickFilesAsync("Locate msconvert", new[] { "*" }, allowMultiple: false);
        if (files.Count > 0) MsconvertPath.Text = files[0];
    }

    private async void OnBrowseCache(object? sender, RoutedEventArgs e)
    {
        var dialogs = new FileDialogService(this);
        var folder = await dialogs.PickFolderAsync("Choose the conversion cache folder");
        if (folder is not null) CacheFolder.Text = folder;
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_settings is not null)
        {
            var v = _settings.Current.VendorConversion;
            v.MsconvertPath = MsconvertPath.Text?.Trim() ?? string.Empty;
            v.UseDocker = UseDocker.IsChecked == true;
            v.DockerImage = string.IsNullOrWhiteSpace(DockerImage.Text) ? "chambm/pwiz-skyline-i-agree-to-the-vendor-licenses" : DockerImage.Text.Trim();
            v.ConversionCacheFolder = CacheFolder.Text?.Trim() ?? string.Empty;
            try
            {
                await _settings.SaveAsync();
            }
            catch (Exception ex)
            {
                SettingsPath.Text = "Could not save settings: " + ex.Message;
                return;
            }
        }
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
