using Avalonia.Controls;
using Avalonia.Interactivity;
using MilX.Pipeline;

namespace MilX.Desktop.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"MIL-X {AppInfo.Version}  ·  tracks MS-DIAL {AppInfo.UpstreamVersion}  ·  .NET {Environment.Version}  ·  {Environment.OSVersion}";
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
