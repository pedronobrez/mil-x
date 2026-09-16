using Avalonia;
using Avalonia.Controls;

namespace MilX.Desktop.Views;

/// <summary>
/// The evidence tabs. The workspace shows one of these, or two side by side when the reviewer
/// splits the area; the selected tab of each is kept in the view model, so a split that shows the
/// mirror beside the statistics survives moving to another feature.
/// </summary>
public partial class EvidenceView : UserControl
{
    /// <summary>Which tab this copy is showing; each copy keeps its own, in the view model.</summary>
    public static readonly StyledProperty<int> SelectedTabProperty =
        AvaloniaProperty.Register<EvidenceView, int>(nameof(SelectedTab), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public int SelectedTab
    {
        get => GetValue(SelectedTabProperty);
        set => SetValue(SelectedTabProperty, value);
    }

    public EvidenceView()
    {
        InitializeComponent();
        // tied in code rather than in the markup: an ancestor binding to this type does not resolve
        // from inside its own template, and the tie has to work in both directions anyway
        Tabs.SelectionChanged += (_, _) => SelectedTab = Tabs.SelectedIndex;
        SizeChanged += (_, _) => NarrowMs2();
        this.GetObservable(SelectedTabProperty).Subscribe(new AnonymousObserver<int>(index =>
        {
            if (index >= 0 && index < Tabs.ItemCount && Tabs.SelectedIndex != index) Tabs.SelectedIndex = index;
        }));
    }

    private sealed class AnonymousObserver<T> : IObserver<T>
    {
        private readonly Action<T> _next;
        public AnonymousObserver(Action<T> next) => _next = next;
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) => _next(value);
    }

    /// <summary>The tab strip, which the shell reads to report what is showing.</summary>
    public TabControl Tabs => this.FindControl<TabControl>("ResultTabs")!;

    /// <summary>
    /// In a split pane the mirror is worth more than the table of scores beside it: at half the
    /// width the chart was left a few centimetres and the numbers took the rest. Below a width
    /// where both fit, the scores step aside — they are in the Candidates tab as well.
    /// </summary>
    private void NarrowMs2()
    {
        var scores = this.FindControl<Border>("Ms2Scores");
        var columns = this.FindControl<Grid>("Ms2Columns")?.ColumnDefinitions;
        if (scores is null || columns is null || columns.Count < 2) return;
        var room = Bounds.Width >= 620;
        scores.IsVisible = room;
        columns[1].Width = new GridLength(room ? 260 : 0);
    }
}
