using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using MilX.Desktop.Help;
using MilX.Desktop.Views;
using Xunit;

namespace MilX.Desktop.Tests;

/// <summary>
/// Holds the manual to the application.
///
/// A manual that lags the build gives wrong answers about the tool it describes, so the parts of
/// it that can be checked are checked here: every link names a page, every image is carried, every
/// control the interface shows has a sentence somewhere, and the versions page names this build.
/// The prose itself is a habit; these are the fence around it.
/// </summary>
public class ManualTests
{
    private static readonly Dictionary<string, Manual> Editions = Manual.Languages.ToDictionary(l => l.Code, l => Manual.Load(l.Code));

    /// <summary>The embedded manual in one of its languages; every structural check runs on each.</summary>
    private static Manual Edition(string language) => Editions[language];

    public static IEnumerable<object[]> Languages => Manual.Languages.Select(l => new object[] { l.Code });

    /// <summary>The repository, found from the test binary, for the files the tests read from source.</summary>
    private static string RepositoryRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MilX.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("the repository root was not found above " + AppContext.BaseDirectory);
        }
    }

    [Theory, MemberData(nameof(Languages))]
    public void The_manual_is_embedded_and_has_its_sections(string language)
    {
        var Embedded = Edition(language);
        Assert.True(Embedded.Pages.Count >= 25, $"only {Embedded.Pages.Count} pages are embedded");
        foreach (var section in Manual.SectionOrder)
        {
            Assert.True(Embedded.Pages.Any(p => p.Section == section), $"no page in the section '{section}'");
        }
        Assert.NotNull(Embedded.Find("index"));
    }

    [Theory, MemberData(nameof(Languages))]
    public void Every_page_has_front_matter(string language)
    {
        var Embedded = Edition(language);
        foreach (var page in Embedded.Pages)
        {
            Assert.False(string.IsNullOrWhiteSpace(page.Title) || page.Title == page.Slug, $"{page.Slug} has no title");
            Assert.False(string.IsNullOrWhiteSpace(page.Summary), $"{page.Slug} has no summary");
            Assert.Contains(page.Section, Manual.SectionOrder);
            Assert.True(page.Order < 999, $"{page.Slug} has no order");
            Assert.StartsWith("# ", page.Body.TrimStart(), StringComparison.Ordinal);
        }
    }

    [Theory, MemberData(nameof(Languages))]
    public void Every_wikilink_points_at_a_page_that_exists(string language)
    {
        var Embedded = Edition(language);
        var broken = Embedded.BrokenLinks();
        Assert.True(broken.Count == 0, "broken links: " + string.Join(", ", broken.Select(b => $"{b.Page.Slug} -> [[{b.Link}]]")));
    }

    [Theory, MemberData(nameof(Languages))]
    public void Every_anchor_names_a_heading_on_its_page(string language)
    {
        var Embedded = Edition(language);
        var anchors = new Regex(@"\[\[([^\]\|#]+)#([^\]\|]+)(\|[^\]]*)?\]\]");
        var missing = new List<string>();
        foreach (var page in Embedded.Pages)
        {
            foreach (Match m in anchors.Matches(page.Body))
            {
                var target = Embedded.Find(m.Groups[1].Value.Trim());
                if (target is null) continue;
                var heading = m.Groups[2].Value.Trim();
                if (!target.Headings.Any(h => string.Equals(InlineText.Plain(h), heading, StringComparison.OrdinalIgnoreCase)))
                {
                    missing.Add($"{page.Slug} -> [[{target.Slug}#{heading}]]");
                }
            }
        }
        Assert.True(missing.Count == 0, "anchors with no heading: " + string.Join(", ", missing));
    }

    [Theory, MemberData(nameof(Languages))]
    public void Every_page_is_reachable_from_the_index(string language)
    {
        var Embedded = Edition(language);
        var index = Embedded.Find("index")!;
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { index.Slug };
        var queue = new Queue<ManualPage>();
        queue.Enqueue(index);
        while (queue.Count > 0)
        {
            foreach (var link in queue.Dequeue().Links)
            {
                if (Embedded.Find(link) is { } page && reachable.Add(page.Slug)) queue.Enqueue(page);
            }
        }
        var orphans = Embedded.Pages.Where(p => !reachable.Contains(p.Slug)).Select(p => p.Slug).ToList();
        Assert.True(orphans.Count == 0, "pages no link reaches: " + string.Join(", ", orphans));
    }

    [Theory, MemberData(nameof(Languages))]
    public void Every_image_the_manual_shows_is_embedded(string language)
    {
        var Embedded = Edition(language);
        var images = new Regex(@"!\[[^\]]*\]\(([^)]+)\)");
        var names = typeof(Manual).Assembly.GetManifestResourceNames().ToHashSet(StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (var page in Embedded.Pages)
        {
            foreach (Match m in images.Matches(page.Body))
            {
                if (!names.Contains("Manual/" + m.Groups[1].Value)) missing.Add($"{page.Slug}: {m.Groups[1].Value}");
            }
        }
        Assert.True(missing.Count == 0, "images not embedded: " + string.Join(", ", missing));
    }

    [Theory, MemberData(nameof(Languages))]
    public void The_versions_page_names_this_build(string language)
    {
        var Embedded = Edition(language);
        var versions = Embedded.Find("versions");
        Assert.NotNull(versions);
        Assert.Contains("## " + AppInfo.Version, versions!.Body);
        Assert.Contains(AppInfo.UpstreamVersion, versions.Body);
    }

    [Theory, MemberData(nameof(Languages))]
    public void The_search_puts_the_page_named_after_the_word_first(string language)
    {
        var Embedded = Edition(language);
        var hits = Embedded.Search("drift correction");
        Assert.NotEmpty(hits);
        Assert.Equal("statistics-workspace", hits[0].Page.Slug);
        Assert.Contains("drift", hits[0].Snippet, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("keyboard-shortcuts", Embedded.Search(language == "pt" ? "atalhos" : "shortcuts")[0].Page.Slug);
        Assert.Empty(Embedded.Search("zebra crossing"));
        Assert.Empty(Embedded.Search("   "));
    }

    [Theory, MemberData(nameof(Languages))]
    public void Backlinks_are_the_reverse_of_links(string language)
    {
        var Embedded = Edition(language);
        var reviewTags = Embedded.Find("review-tags")!;
        Assert.Contains("analytics-workspace", reviewTags.Backlinks);
        foreach (var page in Embedded.Pages)
        {
            foreach (var back in page.Backlinks)
            {
                Assert.Contains(page.Slug, Embedded.Find(back)!.Links);
            }
        }
    }

    /// <summary>
    /// Every label the interface shows on a button, a tab, a menu item, a check box or a radio
    /// button has to appear in the manual. This is what keeps a control from being added without a
    /// word about it: the build goes red until the sentence is written.
    /// </summary>
    [Theory, MemberData(nameof(Languages))]
    public void Every_control_the_interface_shows_is_described(string language)
    {
        var Embedded = Edition(language);
        var views = Path.Combine(RepositoryRoot, "src", "MilX.Desktop", "Views");
        Assert.True(Directory.Exists(views), views);
        var labels = new Regex(@"<(?:Button|ToggleButton|MenuItem|TabItem|CheckBox|RadioButton)\b[^>]*?\b(?:Content|Header)=""([^""{]+)""", RegexOptions.Compiled);
        // the source is hard-wrapped, so a label may sit across a line break
        var text = Regex.Replace(string.Join("\n", Embedded.Pages.Select(p => p.Body)), @"\s+", " ").ToLowerInvariant();
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // one-word dialog buttons and glyphs that no manual needs to spell out
            "OK", "Cancel", "Save", "Close", "Back", "Next", "Apply", "Remove", "Add", "Browse", "Export", "Search", "Clear", "Quit",
            "Da", "ppm", "RT", "Log", "Process", "Reset",
        };
        var missing = new List<string>();
        foreach (var file in Directory.EnumerateFiles(views, "*.axaml"))
        {
            foreach (Match m in labels.Matches(File.ReadAllText(file)))
            {
                var label = m.Groups[1].Value.Replace("_", string.Empty).TrimEnd('…', '.', ' ', '▸').Trim();
                if (label.Length < 3 || ignored.Contains(label) || !label.Any(char.IsLetter)) continue;
                if (!text.Contains(label.ToLowerInvariant(), StringComparison.Ordinal))
                {
                    missing.Add($"{Path.GetFileName(file)}: \"{label}\"");
                }
            }
        }
        Assert.True(missing.Count == 0, "controls the manual does not mention: " + string.Join("; ", missing.Distinct()));
    }

    [Theory, MemberData(nameof(Languages))]
    public void Every_shortcut_is_in_the_shortcut_table(string language)
    {
        var Embedded = Edition(language);
        var page = Embedded.Find("keyboard-shortcuts")!.Body;
        var window = Path.Combine(RepositoryRoot, "src", "MilX.Desktop", "Views", "MainWindow.axaml");
        var ionTable = Path.Combine(RepositoryRoot, "src", "MilX.Desktop", "Views", "IonTableWindow.axaml");
        var gestures = new Regex(@"Gesture=""([^""]+)""");
        var missing = new List<string>();
        foreach (var file in new[] { window, ionTable })
        {
            foreach (Match m in gestures.Matches(File.ReadAllText(file)))
            {
                var raw = m.Groups[1].Value;
                var gesture = raw.StartsWith("Cmd+", StringComparison.Ordinal)
                    ? Regex.Replace(raw.Replace("Cmd+", "⌘").Replace("Shift+", "⇧").Replace("OemQuestion", "?").Replace("Down", "↓").Replace("Up", "↑"), @"D(\d)", "$1")
                    : Regex.Replace(raw.Replace("Down", "↓").Replace("Up", "↑"), @"D(\d)", "$1");
                // "⌘S" is written "⌘S" on the page, "⌘⇧1" as "⌘⇧1"; "Ctrl+Shift+1" as "Ctrl+Shift+1"
                if (!page.Contains(gesture, StringComparison.Ordinal)) missing.Add(m.Groups[1].Value + " (" + gesture + ")");
            }
        }
        Assert.True(missing.Count == 0, "shortcuts not on the page: " + string.Join(", ", missing));
    }

    /// <summary>
    /// The Portuguese edition is the English one page for page: the same slugs, in the same sections and
    /// order, showing the same figures. A page added to one without the other fails here.
    /// </summary>
    [Fact]
    public void The_editions_have_the_same_pages()
    {
        var english = Edition("en");
        var images = new Regex(@"!\[[^\]]*\]\(([^)]+)\)");
        foreach (var (code, _) in Manual.Languages.Where(l => l.Code != "en"))
        {
            var other = Edition(code);
            Assert.Equal(code, other.Language);
            Assert.Equal(english.Pages.Select(p => p.Slug), other.Pages.Select(p => p.Slug));
            foreach (var page in english.Pages)
            {
                var twin = other.Find(page.Slug)!;
                Assert.Equal(page.Section, twin.Section);
                Assert.Equal(page.Order, twin.Order);
                Assert.Equal(images.Matches(page.Body).Select(m => m.Groups[1].Value), images.Matches(twin.Body).Select(m => m.Groups[1].Value));
            }
        }
    }

    /// <summary>The switch is a control like any other: each edition names both languages.</summary>
    [Theory, MemberData(nameof(Languages))]
    public void Each_edition_names_the_language_switch(string language)
    {
        var text = string.Join("\n", Edition(language).Pages.Select(p => p.Body));
        foreach (var (_, name) in Manual.Languages) Assert.Contains(name, text);
    }

    /// <summary>
    /// A page's brushes follow the window's theme. Looked up through the application while the page
    /// was built, a dark window drew its code spans on the light theme's surface: white on white.
    /// </summary>
    [AvaloniaFact]
    public void The_page_takes_its_brushes_from_the_window_theme()
    {
        static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
        static Color Resource(Window window, string key)
        {
            Assert.True(window.TryFindResource(key, window.ActualThemeVariant, out var value), key);
            return ColorOf(Assert.IsAssignableFrom<IBrush>(value));
        }

        foreach (var variant in new[] { Avalonia.Styling.ThemeVariant.Dark, Avalonia.Styling.ThemeVariant.Light })
        {
            var vm = new HelpViewModel(Edition("en"));
            var window = new HelpWindow { DataContext = vm, Width = 1100, Height = 760, RequestedThemeVariant = variant };
            window.Show();
            vm.Query = "drift";
            vm.Open(vm.Results[0]);   // the statistics page: code spans, a table, and the word marked
            var runs = window.GetVisualDescendants().OfType<LinkTextBlock>().SelectMany(b => b.Inlines!.OfType<Run>()).ToList();
            var code = runs.Where(r => r.FontSize == 12 && r.Background is not null).ToList();
            Assert.NotEmpty(code);
            Assert.All(code, r => Assert.Equal(Resource(window, "OdSurfaceAlt"), ColorOf(r.Background)));
            var marked = runs.Where(r => r.Text?.Equals("drift", StringComparison.OrdinalIgnoreCase) == true).ToList();
            Assert.NotEmpty(marked);
            Assert.All(marked, r => Assert.Equal(Resource(window, "OdWarningSoft"), ColorOf(r.Background)));
            var links = window.GetVisualDescendants().OfType<LinkTextBlock>().SelectMany(b => b.Inlines!.OfType<Run>()).Where(r => r.Foreground is not null).ToList();
            Assert.Contains(links, r => ColorOf(r.Foreground) == Resource(window, "OdAccent"));
            // the two variants really differ, so the check above is not vacuous
            if (variant == Avalonia.Styling.ThemeVariant.Dark) Assert.NotEqual(Colors.WhiteSmoke, ColorOf(code[0].Background));
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_help_window_switches_language_on_the_same_page()
    {
        var vm = new HelpViewModel(Edition("en"));
        var window = new HelpWindow { DataContext = vm, Width = 1100, Height = 760 };
        window.Show();
        Assert.True(vm.Open("review-tags"));
        Assert.Equal("Português", vm.OtherLanguageName);
        string? chosen = null;
        vm.LanguageChanged = code => chosen = code;

        vm.ToggleLanguageCommand.Execute(null);
        Assert.Equal("pt", vm.Language);
        Assert.Equal("pt", chosen);
        Assert.Equal("English", vm.OtherLanguageName);
        Assert.Equal("review-tags", vm.Current?.Slug);
        Assert.Equal("review-tags", window.ShownPage?.Slug);
        Assert.StartsWith("Revisão", vm.Breadcrumb);
        Assert.Contains(vm.Sections, s => s.Name == "Áreas de trabalho");
        var headings = window.GetVisualDescendants().OfType<LinkTextBlock>().Where(t => t.Classes.Contains("heading")).ToList();
        Assert.Equal("Marcações e vereditos", headings[0].Tag);

        // the trail carried over as slugs, so Back still goes where it went, in the new language
        Assert.True(vm.CanGoBack);
        vm.BackCommand.Execute(null);
        Assert.Equal("index", vm.Current?.Slug);
        Assert.Equal("pt", vm.Current?.Slug is null ? null : vm.Language);

        vm.SwitchLanguage("en");
        Assert.Equal("en", vm.Language);
        Assert.Equal("index", vm.Current?.Slug);
        window.Close();
    }

    [Fact]
    public void The_markdown_parser_reads_every_construct_the_manual_uses()
    {
        var blocks = MarkdownDocument.Parse("""
            # Title

            A paragraph with **bold**, *italic*, `code`, a [[page-slug|label]], a [[Other#Heading]] and a [link](https://x.y).
            It wraps.

            - one
            - two
              - nested
            1. first
            2. second

            | a | b |
            | --- | --- |
            | 1 | `x|y` |

            ```bash
            echo hi
            ```

            > a quote

            ![alt](images/x.png)

            ---
            """);
        Assert.Collection(blocks,
            b => Assert.Equal(1, Assert.IsType<HeadingBlock>(b).Level),
            b => Assert.Contains("It wraps.", Assert.IsType<ParagraphBlock>(b).Text),
            b => { var l = Assert.IsType<ListBlock>(b); Assert.False(l.Ordered); Assert.Equal(2, l.Items.Count); Assert.NotNull(l.Items[1].Nested); },
            b => { var l = Assert.IsType<ListBlock>(b); Assert.True(l.Ordered); Assert.Equal(2, l.Items.Count); },
            b => { var t = Assert.IsType<TableBlock>(b); Assert.Equal(2, t.Header.Count); Assert.Equal("`x|y`", t.Rows[0][1]); },
            b => Assert.Equal("echo hi", Assert.IsType<CodeBlock>(b).Text),
            b => Assert.Equal("a quote", Assert.IsType<QuoteBlock>(b).Text),
            b => Assert.Equal("images/x.png", Assert.IsType<ImageBlock>(b).Source),
            b => Assert.IsType<RuleBlock>(b));

        var spans = InlineText.Parse("A **bold** [[page-slug|label]] and [[Other#Heading]] and [link](https://x.y) `c`");
        Assert.Contains(spans, s => s.Bold && s.Text == "bold");
        Assert.Contains(spans, s => s.IsWikiLink && s.Link == "page-slug" && s.Text == "label");
        Assert.Contains(spans, s => s.IsWikiLink && s.Link == "Other" && s.LinkAnchor == "Heading");
        Assert.Contains(spans, s => !s.IsWikiLink && s.Link == "https://x.y" && s.Text == "link");
        Assert.Contains(spans, s => s.Code && s.Text == "c");
    }

    [AvaloniaFact]
    public void The_help_window_renders_a_page_and_follows_a_link()
    {
        var vm = new HelpViewModel(Edition("en"));
        var window = new HelpWindow { DataContext = vm, Width = 1100, Height = 760 };
        window.Show();
        Assert.Equal("index", window.ShownPage?.Slug);
        var headings = window.GetVisualDescendants().OfType<LinkTextBlock>().Where(t => t.Classes.Contains("heading")).ToList();
        Assert.NotEmpty(headings);
        Assert.Equal("MIL-X manual", headings[0].Tag);

        Assert.True(vm.Open("review-tags"));
        Assert.Equal("review-tags", window.ShownPage?.Slug);
        Assert.True(vm.CanGoBack);
        vm.BackCommand.Execute(null);
        Assert.Equal("index", vm.Current?.Slug);
        Assert.True(vm.CanGoForward);

        vm.Query = "drift";
        Assert.NotEmpty(vm.Results);
        vm.Open(vm.Results[0]);
        Assert.Equal("statistics-workspace", vm.Current?.Slug);
        Assert.Equal("drift", vm.Highlight);
        window.Close();
    }

    [AvaloniaFact]
    public void F1_opens_the_manual_at_the_page_for_the_workspace()
    {
        var vm = new ViewModels.MainWindowViewModel(new Services.SettingsService(), new NoDialogs(), new NoMessages());
        var window = new MainWindow { DataContext = vm, Width = 1200, Height = 800 };
        var shown = 0;
        vm.ShowHelpWindow = () => shown++;
        window.Show();
        vm.SelectedWorkspace = 4;
        window.KeyPress(Key.F1, RawInputModifiers.None, PhysicalKey.F1, null);
        Assert.Equal(1, shown);
        Assert.Equal("statistics-workspace", vm.Help?.Current?.Slug);
        vm.OpenHelpCommand.Execute("troubleshooting");
        Assert.Equal("troubleshooting", vm.Help?.Current?.Slug);
        window.Close();
    }

    private sealed class NoDialogs : Services.IFileDialogService
    {
        public Task<IReadOnlyList<string>> PickFilesAsync(string title, IReadOnlyList<string> patterns, bool allowMultiple, string? startFolder = null)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<string?> PickFolderAsync(string title, string? startFolder = null) => Task.FromResult<string?>(null);
        public Task<string?> SaveFileAsync(string title, string suggestedName, string extension, string? startFolder = null) => Task.FromResult<string?>(null);
    }

    private sealed class NoMessages : Services.IMessageService
    {
        public Task<Services.DiscardChoice> ConfirmDiscardAsync(string question) => Task.FromResult(Services.DiscardChoice.Discard);
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ConfirmAsync(string title, string message, string okLabel = "OK") => Task.FromResult(true);
    }
}
