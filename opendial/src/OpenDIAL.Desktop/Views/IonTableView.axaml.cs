using Avalonia.Controls;

namespace OpenDIAL.Desktop.Views;

public partial class IonTableView : UserControl
{
    public IonTableView()
    {
        InitializeComponent();
    }

    /// <summary>The grid itself, so the shell can save and restore the column order.</summary>
    public DataGrid Table => this.FindControl<DataGrid>("IonTable")!;
}
