using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenDIAL.Pipeline;

namespace OpenDIAL.Desktop.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Tracks MS-DIAL {PipelineRunner.UpstreamVersion}  ·  .NET {Environment.Version}  ·  {Environment.OSVersion}";
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
