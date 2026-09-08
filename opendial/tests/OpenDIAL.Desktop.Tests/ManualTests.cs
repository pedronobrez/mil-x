using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using OpenDIAL.Desktop.Help;
using OpenDIAL.Desktop.Views;
using Xunit;

namespace OpenDIAL.Desktop.Tests;

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
    private static readonly Manual Embedded = Manual.Load();

    /// <summary>The repository, found from the test binary, for the files the tests read from source.</summary>
    private static string RepositoryRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpenDIAL.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("the repository root was not found above " + AppContext.BaseDirectory);
        }
    }

    [Fact]
    public void The_manual_is_embedded_and_has_its_sections()
    {
        Assert.True(Embedded.Pages.Count >= 25, $"only {Embedded.Pages.Count} pages are embedded");
        foreach (var section in Manual.SectionOrder)
        {
            Assert.True(Embedded.Pages.Any(p => p.Section == section), $"no page in the section '{section}'");
        }
        Assert.NotNull(Embedded.Find("index"));
    }

    [Fact]
    public void Every_page_has_front_matter()
    {
        foreach (var page in Embedded.Pages)
        {
            Assert.False(string.IsNullOrWhiteSpace(page.Title) || page.Title == page.Slug, $"{page.Slug} has no title");
            Assert.False(string.IsNullOrWhiteSpace(page.Summary), $"{page.Slug} has no summary");
            Assert.Contains(page.Section, Manual.SectionOrder);
            Assert.True(page.Order < 999, $"{page.Slug} has no order");
            Assert.StartsWith("# ", page.Body.TrimStart(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Every_wikilink_points_at_a_page_that_exists()
    {
        var broken = Embedded.BrokenLinks();
        Assert.True(broken.Count == 0, "broken links: " + string.Join(", ", broken.Select(b => $"{b.Page.Slug} -> [[{b.Link}]]")));
    }

    [Fact]
    public void Every_anchor_names_a_heading_on_its_page()
    {
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

    [Fact]
    public void Every_page_is_reachable_from_the_index()
    {
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

    [Fact]
    public void Every_image_the_manual_shows_is_embedded()
    {
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

    [Fact]
    public void The_versions_page_names_this_build()
    {
        var versions = Embedded.Find("versions");
        Assert.NotNull(versions);
        Assert.Contains("## " + AppInfo.Version, versions!.Body);
        Assert.Contains(AppInfo.UpstreamVersion, versions.Body);
    }

    [Fact]
    public void The_search_puts_the_page_named_after_the_word_first()
    {
        var hits = Embedded.Search("drift correction");
        Assert.NotEmpty(hits);
        Assert.Equal("statistics-workspace", hits[0].Page.Slug);
        Assert.Contains("drift", hits[0].Snippet, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("keyboard-shortcuts", Embedded.Search("shortcuts")[0].Page.Slug);
        Assert.Empty(Embedded.Search("zebra crossing"));
        Assert.Empty(Embedded.Search("   "));
    }

    [Fact]
    public void Backlinks_are_the_reverse_of_links()
    {
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
    [Fact]
    public void Every_control_the_interface_shows_is_described()
    {
        var views = Path.Combine(RepositoryRoot, "src", "OpenDIAL.Desktop", "Views");
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

    [Fact]
    public void Every_shortcut_is_in_the_shortcut_table()
    {
        var page = Embedded.Find("keyboard-shortcuts")!.Body;
        var window = Path.Combine(RepositoryRoot, "src", "OpenDIAL.Desktop", "Views", "MainWindow.axaml");
        var analytics = Path.Combine(RepositoryRoot, "src", "OpenDIAL.Desktop", "Views", "AnalyticsView.axaml");
        var gestures = new Regex(@"Gesture=""([^""]+)""");
        var missing = new List<string>();
        foreach (var file in new[] { window, analytics })
        {
            foreach (Match m in gestures.Matches(File.ReadAllText(file)))
            {
                var gesture = Regex.Replace(m.Groups[1].Value
                    .Replace("Down", "↓").Replace("Up", "↑")
                    .Replace("Cmd+", "⌘").Replace("OemQuestion", "?").Replace("Shift+", "⇧").Replace("⌘⇧+", "⌘⇧"),
                    @"D(\d)", "$1").Replace("⌘+", "⌘");
                // "⌘S" is written "⌘S" on the page; "Ctrl+1" as "Ctrl+1"; "Alt+C" as "Alt+C"
                if (!page.Contains(gesture, StringComparison.Ordinal)) missing.Add(m.Groups[1].Value + " (" + gesture + ")");
            }
        }
        Assert.True(missing.Count == 0, "shortcuts not on the page: " + string.Join(", ", missing));
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
        var vm = new HelpViewModel(Embedded);
        var window = new HelpWindow { DataContext = vm, Width = 1100, Height = 760 };
        window.Show();
        Assert.Equal("index", window.ShownPage?.Slug);
        var headings = window.GetVisualDescendants().OfType<LinkTextBlock>().Where(t => t.Classes.Contains("heading")).ToList();
        Assert.NotEmpty(headings);
        Assert.Equal("OpenDIAL manual", headings[0].Tag);

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
