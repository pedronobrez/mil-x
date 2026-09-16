using Avalonia.Controls;

namespace MilX.Desktop.Views;

public partial class IonTableWindow : Window
{
    public IonTableWindow()
    {
        InitializeComponent();
    }

    public IonTableView TableView => this.FindControl<IonTableView>("Table")!;
}
