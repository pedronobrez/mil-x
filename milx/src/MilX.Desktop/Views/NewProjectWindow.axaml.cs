using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using MilX.Desktop.ViewModels;

namespace MilX.Desktop.Views;

public partial class NewProjectWindow : Window
{
    public static readonly IValueConverter StepTitle = new FuncValueConverter<int, string>(step => step switch
    {
        0 => "Where the project lives",
        1 => "Which files are in the batch",
        2 => "How the data is processed",
        _ => "Ready to create",
    });

    private readonly NewProjectViewModel? _vm;

    public NewProjectWindow() : this(null) { }

    public NewProjectWindow(NewProjectViewModel? vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    public bool ProcessNow => _vm?.ProcessNow ?? false;

    private async void OnNext(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (await _vm.NextAsync()) Close(_vm.Result);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
