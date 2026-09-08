namespace OpenDIAL.Desktop.Help;

/// <summary>One page of the manual: its front matter, its Markdown body and what it links to.</summary>
public sealed class ManualPage
{
    public ManualPage(string slug, string title, string section, int order, string summary, string body)
    {
        Slug = slug;
        Title = title;
        Section = section;
        Order = order;
        Summary = summary;
        Body = body;
    }

    /// <summary>The file name without its extension; what a wikilink names.</summary>
    public string Slug { get; }
    public string Title { get; }
    public string Section { get; }
    public int Order { get; }
    public string Summary { get; }
    /// <summary>The Markdown after the front matter.</summary>
    public string Body { get; }

    /// <summary>Slugs this page links to with [[double brackets]], in order of first appearance.</summary>
    public IReadOnlyList<string> Links { get; internal set; } = Array.Empty<string>();

    /// <summary>Slugs of the pages that link here: the "referenced by" list at the bottom.</summary>
    public IReadOnlyList<string> Backlinks { get; internal set; } = Array.Empty<string>();

    /// <summary>The headings, in order, for the page outline and for [[slug#Heading]] targets.</summary>
    public IReadOnlyList<string> Headings { get; internal set; } = Array.Empty<string>();

    /// <summary>The body with Markdown stripped, lower-cased once, for the search.</summary>
    internal string SearchText { get; set; } = string.Empty;

    public override string ToString() => Title;
}

/// <summary>One search hit: the page, how well it matched, and a line of context.</summary>
public sealed record ManualHit(ManualPage Page, int Score, string Snippet);
