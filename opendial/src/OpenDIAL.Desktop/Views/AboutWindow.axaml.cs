using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenDIAL.Pipeline;

namespace OpenDIAL.Desktop.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"OpenDIAL {AppInfo.Version}  ·  tracks MS-DIAL {AppInfo.UpstreamVersion}  ·  .NET {Environment.Version}  ·  {Environment.OSVersion}";
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
