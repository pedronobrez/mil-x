using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenDIAL.Desktop.ViewModels;
using OpenDIAL.Pipeline.Project;

namespace OpenDIAL.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        DataContextChanged += (_, _) => Attach();
        Closing += async (_, e) =>
        {
            if (_vm is null || _closeConfirmed) return;
            e.Cancel = true;
            if (await _vm.ConfirmDiscardAsync("Quit anyway?"))
            {
                _closeConfirmed = true;
                Close();
            }
        };
    }

    private bool _closeConfirmed;

    private void Attach()
    {
        _vm = DataContext as MainWindowViewModel;
        if (_vm is null) return;
        _vm.ShowNewProjectWizard = async () =>
        {
            var wizard = new NewProjectWindow(new NewProjectViewModel(_vm.Settings, new Services.FileDialogService(this)));
            return await wizard.ShowDialog<OpenDialProject?>(this);
        };
        _vm.ShowSettings = async () => await new SettingsWindow(_vm.Settings).ShowDialog(this);
        _vm.ShowAbout = async () => await new AboutWindow().ShowDialog(this);
        _vm.LogLines.CollectionChanged += OnLogChanged;
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && _vm is { LogLines.Count: > 0 } && LogList.IsVisible)
        {
            LogList.ScrollIntoView(_vm.LogLines[^1]);
        }
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();
}
