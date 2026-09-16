using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MilX.Desktop.Help;

/// <summary>A section of the table of contents with its pages.</summary>
public sealed record ManualSection(string Name, IReadOnlyList<ManualPage> Pages);

/// <summary>
/// The manual as the help window shows it: a table of contents, a search box that filters it, the
/// page being read, and the trail of pages read before it so Back means something.
/// </summary>
public sealed partial class HelpViewModel : ObservableObject
{
    private readonly List<ManualPage> _history = new();
    private int _position = -1;

    public HelpViewModel(Manual manual)
    {
        Manual = manual;
        Sections = SectionsOf(manual);
        Current = manual.Find("index") ?? manual.Pages.FirstOrDefault();
        if (Current is not null) { _history.Add(Current); _position = 0; }
    }

    /// <summary>The manual in the language showing; the pages are the same set under every language.</summary>
    public Manual Manual { get; private set; }
    [ObservableProperty] private IReadOnlyList<ManualSection> _sections;
    public ObservableCollection<ManualHit> Results { get; } = new();

    /// <summary>Told which language was chosen, so the choice can be kept.</summary>
    public Action<string>? LanguageChanged { get; set; }

    public string Language => Manual.Language;
    public string LanguageName => Manual.Languages.First(l => l.Code == Language).Name;
    /// <summary>The name of the other language — what the switch offers.</summary>
    public string OtherLanguageName => Manual.Languages.First(l => l.Code != Language).Name;

    private static IReadOnlyList<ManualSection> SectionsOf(Manual manual) => Manual.SectionOrder
        .Select(s => new ManualSection(Manual.SectionLabel(s, manual.Language), manual.Pages.Where(p => p.Section == s).ToList()))
        .Where(s => s.Pages.Count > 0)
        .Concat(manual.Pages.Where(p => !Manual.SectionOrder.Contains(p.Section)).GroupBy(p => p.Section).Select(g => new ManualSection(g.Key, g.ToList())))
        .ToList();

    /// <summary>Shows the same page in the other language; the trail and the search carry over as slugs.</summary>
    [RelayCommand]
    private void ToggleLanguage() => SwitchLanguage(Manual.Languages.First(l => l.Code != Language).Code);

    public void SwitchLanguage(string code)
    {
        if (code == Language) return;
        var manual = Manual.Load(code);
        if (manual.Language != code) return;   // not embedded in this build
        var slug = Current?.Slug ?? "index";
        Manual = manual;
        Sections = SectionsOf(manual);
        // the trail is kept as slugs, so Back still goes where it went
        var trail = _history.Select(p => manual.Find(p.Slug)).Where(p => p is not null).Cast<ManualPage>().ToList();
        _history.Clear();
        _history.AddRange(trail);
        _position = Math.Min(_position, _history.Count - 1);
        var query = Query;
        Current = manual.Find(slug) ?? manual.Pages.FirstOrDefault();
        OnPropertyChanged(nameof(Manual));
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(LanguageName));
        OnPropertyChanged(nameof(OtherLanguageName));
        if (!string.IsNullOrWhiteSpace(query)) OnQueryChanged(query);
        LanguageChanged?.Invoke(code);
    }

    [ObservableProperty] private ManualPage? _current;
    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private bool _isOpen;
    /// <summary>The heading a link asked for, to scroll to once the page is rendered.</summary>
    [ObservableProperty] private string? _anchor;
    /// <summary>The words to mark on the page: the search that led here.</summary>
    [ObservableProperty] private string _highlight = string.Empty;

    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);
    public bool CanGoBack => _position > 0;
    public bool CanGoForward => _position < _history.Count - 1;
    public string Breadcrumb => Current is null ? string.Empty : $"{Manual.SectionLabel(Current.Section, Language)}  ›  {Current.Title}";
    public IReadOnlyList<ManualPage> Backlinks => Current is null ? Array.Empty<ManualPage>() : Current.Backlinks.Select(s => Manual.Find(s)).Where(p => p is not null).Cast<ManualPage>().ToList();

    partial void OnQueryChanged(string value)
    {
        Results.Clear();
        foreach (var hit in Manual.Search(value)) Results.Add(hit);
        OnPropertyChanged(nameof(HasQuery));
    }

    partial void OnCurrentChanged(ManualPage? value)
    {
        OnPropertyChanged(nameof(Breadcrumb));
        OnPropertyChanged(nameof(Backlinks));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    /// <summary>Opens a page by slug or title, remembering where the reader came from.</summary>
    public bool Open(string slugOrTitle, string? anchor = null, string? highlight = null)
    {
        var page = Manual.Find(slugOrTitle);
        if (page is null) return false;
        if (_position < _history.Count - 1) _history.RemoveRange(_position + 1, _history.Count - _position - 1);
        if (Current != page || _history.Count == 0)
        {
            _history.Add(page);
            _position = _history.Count - 1;
        }
        Anchor = anchor;
        Highlight = highlight ?? string.Empty;
        Current = page;
        return true;
    }

    /// <summary>Opens the page a search hit points at, keeping the words to mark on it.</summary>
    public void Open(ManualHit hit) => Open(hit.Page.Slug, null, Query);

    [RelayCommand]
    private void Back()
    {
        if (!CanGoBack) return;
        _position--;
        Anchor = null;
        Current = _history[_position];
    }

    [RelayCommand]
    private void Forward()
    {
        if (!CanGoForward) return;
        _position++;
        Anchor = null;
        Current = _history[_position];
    }

    [RelayCommand]
    private void ClearQuery() => Query = string.Empty;

    /// <summary>The page that explains a workspace, for the F1 that is pressed while it is showing.</summary>
    public static string PageForWorkspace(int workspace) => workspace switch
    {
        0 => "explorer-workspace",
        1 => "analytics-workspace",
        2 => "method-workspace",
        3 => "samples-workspace",
        4 => "statistics-workspace",
        _ => "index",
    };
}
