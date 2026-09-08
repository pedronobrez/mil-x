using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using OpenDIAL.Desktop.Help;

namespace OpenDIAL.Desktop.Views;

public partial class HelpWindow : Window
{
    private HelpViewModel? _vm;

    public HelpWindow()
    {
        InitializeComponent();
        FocusSearchCommand = new RelayCommand(() => { SearchBox.Focus(); SearchBox.SelectAll(); });
        DataContextChanged += (_, _) => Attach();
        // the page column is as wide as the scroller leaves it, up to a comfortable measure; a
        // wrapped paragraph asked for its width would otherwise run past the panel
        PageScroller.SizeChanged += (_, e) => PageColumn.Width = Math.Max(300, Math.Min(900, e.NewSize.Width - PageScroller.Padding.Left - PageScroller.Padding.Right - 18));
        SearchBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _vm is not null) { _vm.Query = string.Empty; e.Handled = true; }
        };
    }

    public IRelayCommand FocusSearchCommand { get; }

    /// <summary>The page currently rendered, for the tests and the probe.</summary>
    public ManualPage? ShownPage => _vm?.Current;

    private void Attach()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnViewModelChanged;
        _vm = DataContext as HelpViewModel;
        if (_vm is null) return;
        _vm.PropertyChanged += OnViewModelChanged;
        Render();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HelpViewModel.Current) or nameof(HelpViewModel.Highlight)) Render();
    }

    private void OnPageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string slug } && _vm is not null) _vm.Open(slug);
    }

    private void OnHitSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (Hits.SelectedItem is ManualHit hit && _vm is not null)
        {
            _vm.Open(hit);
            Hits.SelectedItem = null;
        }
    }

    /// <summary>A link on the page: a slug opens another page, anything else goes to the system.</summary>
    private void OnLink(string target, string? anchor, bool wiki)
    {
        if (_vm is null) return;
        if (wiki || !target.Contains("://", StringComparison.Ordinal))
        {
            if (!_vm.Open(target, anchor)) return;
            return;
        }
        Services.ShellService.Open(target);
    }

    private void Render()
    {
        if (_vm?.Current is null) return;
        PageHost.Children.Clear();
        var renderer = new MarkdownRenderer(OnLink, _vm.Highlight);
        foreach (var control in renderer.Render(MarkdownDocument.Parse(_vm.Current.Body))) PageHost.Children.Add(control);
        MarkCurrentInContents();
        Dispatcher.UIThread.Post(() =>
        {
            var anchor = _vm.Anchor;
            var heading = string.IsNullOrEmpty(anchor) ? null
                : PageHost.Children.OfType<Control>().FirstOrDefault(c => c.Classes.Contains("heading") && string.Equals(c.Tag as string, anchor, StringComparison.OrdinalIgnoreCase));
            if (heading is not null) heading.BringIntoView();
            else PageScroller.ScrollToHome();
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Marks the page being read in the table of contents.</summary>
    private void MarkCurrentInContents()
    {
        var slug = _vm?.Current?.Slug;
        foreach (var button in Contents.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("toc")))
        {
            var isCurrent = button.Tag is string s && string.Equals(s, slug, StringComparison.OrdinalIgnoreCase);
            if (isCurrent) button.Classes.Add("current"); else button.Classes.Remove("current");
        }
    }
}
