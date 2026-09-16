using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MilX.Desktop.Help;

/// <summary>
/// The manual as a set of pages that know each other.
///
/// The pages are Markdown files under docs/manual, embedded into the application at build time so
/// the help is the help of the build that is running, not whatever happens to be on disk. They link
/// to one another with [[double brackets]], the way an Obsidian vault does, and the same files are
/// what the PDF is built from, so there is one manual rather than two that drift apart.
/// </summary>
public sealed class Manual
{
    private static readonly Regex FrontMatter = new(@"\A---\s*\n(.*?)\n---\s*\n", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex WikiLink = new(@"\[\[([^\]\|#]+)(#[^\]\|]*)?(\|[^\]]*)?\]\]", RegexOptions.Compiled);
    private static readonly Regex Heading = new(@"^#{1,6}\s+(.+?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex Markup = new(@"[*_`>#|]|\!\[[^\]]*\]\([^)]*\)|\]\([^)]*\)|\[|\]", RegexOptions.Compiled);

    /// <summary>The sections in the order the table of contents shows them; the keys every language's pages use.</summary>
    public static readonly IReadOnlyList<string> SectionOrder = new[]
    {
        "Start", "Workspaces", "Reviewing", "Data and files", "Reference", "Under the hood", "Help",
    };

    /// <summary>The languages the manual exists in: the folder each lives in, and the name it goes by.</summary>
    public static readonly IReadOnlyList<(string Code, string Name)> Languages = new[] { ("en", "English"), ("pt", "Português") };

    private static readonly Dictionary<string, string> PortugueseSections = new(StringComparer.Ordinal)
    {
        ["Start"] = "Início", ["Workspaces"] = "Áreas de trabalho", ["Reviewing"] = "Revisão", ["Data and files"] = "Dados e arquivos",
        ["Reference"] = "Referência", ["Under the hood"] = "Por dentro", ["Help"] = "Ajuda",
    };

    /// <summary>What a section is called in the manual's language.</summary>
    public static string SectionLabel(string section, string language) =>
        language == "pt" && PortugueseSections.TryGetValue(section, out var pt) ? pt : section;

    /// <summary>The language this manual was loaded in.</summary>
    public string Language { get; private set; } = "en";

    private readonly Dictionary<string, ManualPage> _bySlug;
    private readonly Dictionary<string, ManualPage> _byTitle;

    private Manual(IReadOnlyList<ManualPage> pages)
    {
        Pages = pages;
        _bySlug = pages.ToDictionary(p => p.Slug, StringComparer.OrdinalIgnoreCase);
        _byTitle = new Dictionary<string, ManualPage>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in pages) _byTitle.TryAdd(page.Title, page);
        Resolve();
    }

    /// <summary>Every page, in table-of-contents order.</summary>
    public IReadOnlyList<ManualPage> Pages { get; }

    /// <summary>The manual embedded in this build, in English or in the language asked for.</summary>
    public static Manual Load(string language = "en") => Load(typeof(Manual).Assembly, language);

    public static Manual Load(Assembly assembly, string language = "en")
    {
        // English pages sit at Manual/<slug>.md, a translation at Manual/<code>/<slug>.md
        var prefix = language == "en" ? "Manual/" : $"Manual/{language}/";
        var pages = new List<ManualPage>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal)))
        {
            var rest = name[prefix.Length..];
            if (rest.Contains('/')) continue;   // another language's folder
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            pages.Add(Parse(Path.GetFileNameWithoutExtension(rest), reader.ReadToEnd()));
        }
        if (pages.Count == 0 && language != "en") return Load(assembly, "en");
        return FromPages(pages, language);
    }

    /// <summary>A manual read straight from a folder of Markdown files; what the PDF build and the tests use.</summary>
    public static Manual LoadFrom(string folder, string language = "en")
    {
        var pages = Directory.EnumerateFiles(folder, "*.md")
            .Select(f => Parse(Path.GetFileNameWithoutExtension(f), File.ReadAllText(f)))
            .ToList();
        return FromPages(pages, language);
    }

    private static Manual FromPages(List<ManualPage> pages, string language)
    {
        var ordered = pages
            .OrderBy(p => { var i = SectionOrder.ToList().IndexOf(p.Section); return i < 0 ? SectionOrder.Count : i; })
            .ThenBy(p => p.Order)
            .ThenBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new Manual(ordered) { Language = language };
    }

    public static ManualPage Parse(string slug, string text)
    {
        text = text.Replace("\r\n", "\n");
        var title = slug;
        var section = "Reference";
        var order = 999;
        var summary = string.Empty;
        var body = text;
        var match = FrontMatter.Match(text);
        if (match.Success)
        {
            body = text[match.Length..];
            foreach (var line in match.Groups[1].Value.Split('\n'))
            {
                var colon = line.IndexOf(':');
                if (colon <= 0) continue;
                var key = line[..colon].Trim().ToLowerInvariant();
                var value = line[(colon + 1)..].Trim().Trim('"');
                switch (key)
                {
                    case "title": title = value; break;
                    case "section": section = value; break;
                    case "order": if (int.TryParse(value, out var n)) order = n; break;
                    case "summary": summary = value; break;
                }
            }
        }
        var page = new ManualPage(slug, title, section, order, summary, body)
        {
            Links = WikiLink.Matches(body).Select(m => m.Groups[1].Value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Headings = Heading.Matches(body).Select(m => m.Groups[1].Value).ToList(),
            SearchText = Markup.Replace(WikiLink.Replace(body, m => m.Groups[3].Success ? m.Groups[3].Value[1..] : m.Groups[1].Value), " ").ToLowerInvariant(),
        };
        return page;
    }

    private void Resolve()
    {
        var backlinks = Pages.ToDictionary(p => p.Slug, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var page in Pages)
        {
            page.Links = page.Links.Select(l => Find(l)?.Slug ?? l).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var link in page.Links)
            {
                if (backlinks.TryGetValue(link, out var list) && !string.Equals(link, page.Slug, StringComparison.OrdinalIgnoreCase)) list.Add(page.Slug);
            }
        }
        foreach (var page in Pages) page.Backlinks = backlinks[page.Slug];
    }

    /// <summary>A page by slug or by title, case-insensitively; null when there is no such page.</summary>
    public ManualPage? Find(string slugOrTitle)
    {
        if (string.IsNullOrWhiteSpace(slugOrTitle)) return null;
        var key = slugOrTitle.Trim();
        if (_bySlug.TryGetValue(key, out var page)) return page;
        if (_byTitle.TryGetValue(key, out page)) return page;
        // "Samples workspace" for "The Samples workspace", and a slug typed with spaces
        key = key.Replace(' ', '-');
        return _bySlug.TryGetValue(key, out page) ? page : null;
    }

    /// <summary>Every wikilink in the manual that names no page: what the tests hold at zero.</summary>
    public IReadOnlyList<(ManualPage Page, string Link)> BrokenLinks() =>
        Pages.SelectMany(p => p.Links.Where(l => Find(l) is null).Select(l => (p, l))).ToList();

    /// <summary>
    /// Pages matching every word of the query, best first. A word in the title counts for more than
    /// one in a heading, and that for more than one in the body; the snippet is the first place the
    /// first word appears.
    /// </summary>
    public IReadOnlyList<ManualHit> Search(string query, int limit = 30)
    {
        var words = (query ?? string.Empty).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return Array.Empty<ManualHit>();
        var hits = new List<ManualHit>();
        foreach (var page in Pages)
        {
            var title = page.Title.ToLowerInvariant();
            var headings = string.Join(" ", page.Headings).ToLowerInvariant();
            var score = 0;
            var all = true;
            foreach (var word in words)
            {
                var inTitle = title.Contains(word, StringComparison.Ordinal);
                var inHeading = headings.Contains(word, StringComparison.Ordinal);
                var inBody = Count(page.SearchText, word);
                if (!inTitle && !inHeading && inBody == 0) { all = false; break; }
                score += (inTitle ? 40 : 0) + (inHeading ? 12 : 0) + Math.Min(inBody, 20);
            }
            if (!all) continue;
            hits.Add(new ManualHit(page, score, Snippet(page, words[0])));
        }
        return hits.OrderByDescending(h => h.Score).ThenBy(h => h.Page.Title).Take(limit).ToList();
    }

    private static int Count(string text, string word)
    {
        var count = 0;
        var at = 0;
        while ((at = text.IndexOf(word, at, StringComparison.Ordinal)) >= 0) { count++; at += word.Length; }
        return count;
    }

    private static string Snippet(ManualPage page, string word)
    {
        var text = page.SearchText;
        var at = text.IndexOf(word, StringComparison.Ordinal);
        if (at < 0) return page.Summary;
        var start = Math.Max(0, at - 60);
        var end = Math.Min(text.Length, at + word.Length + 80);
        var piece = text[start..end].Replace('\n', ' ');
        piece = Regex.Replace(piece, @"\s+", " ").Trim();
        return (start > 0 ? "…" : string.Empty) + piece + (end < text.Length ? "…" : string.Empty);
    }
}
