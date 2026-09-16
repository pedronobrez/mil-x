using Avalonia.Controls;
using MilX.Desktop.ViewModels;

namespace MilX.Desktop.Views;

public partial class IonTableView : UserControl
{
    public IonTableView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => TellTheViewModelHowTheRowsAreSorted();
        AttachedToVisualTree += (_, _) => TellTheViewModelHowTheRowsAreSorted();
    }

    /// <summary>The grid itself, so the shell can save and restore the column order.</summary>
    public DataGrid Table => this.FindControl<DataGrid>("IonTable")!;

    /// <summary>
    /// Confirm ▸, the arrows and Next unreviewed walk the table the way it is on screen. Sorting by
    /// S/N and then confirming down the list is a normal way to work, and following the collection's
    /// own order instead would jump around the screen. The grid's view is the sorted one; the view
    /// model asks for it when it needs to move.
    /// </summary>
    private void TellTheViewModelHowTheRowsAreSorted()
    {
        if (DataContext is not AnalyticsViewModel vm) return;
        vm.RowsAsShown = () =>
        {
            var view = Table?.CollectionView;
            if (view is null) return Array.Empty<SpotRowViewModel>();
            var rows = new List<SpotRowViewModel>();
            foreach (var item in view)
            {
                if (item is SpotRowViewModel row) rows.Add(row);
            }
            return rows;
        };
    }
}
