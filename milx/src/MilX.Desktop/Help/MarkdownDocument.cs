using System.Text.RegularExpressions;

namespace MilX.Desktop.Help;

/// <summary>A block of a manual page, after parsing.</summary>
public abstract record Block;
public sealed record HeadingBlock(int Level, string Text) : Block;
public sealed record ParagraphBlock(string Text) : Block;
public sealed record CodeBlock(string Text, string Language) : Block;
public sealed record QuoteBlock(string Text) : Block;
public sealed record ImageBlock(string Alt, string Source) : Block;
public sealed record RuleBlock : Block;
public sealed record ListItem(string Text, ListBlock? Nested);
public sealed record ListBlock(bool Ordered, IReadOnlyList<ListItem> Items) : Block;
public sealed record TableBlock(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows) : Block;

/// <summary>
/// The subset of Markdown the manual is written in, parsed into blocks. It is a small grammar on
/// purpose: headings, paragraphs, fenced code, quotes, lists two levels deep, pipe tables, images
/// and rules, with the inline marks handled by <see cref="InlineText"/>. Nothing else is needed to
/// write the manual, and a small parser is one that can be read.
/// </summary>
public static class MarkdownDocument
{
    private static readonly Regex ListLine = new(@"^(\s*)([-*]|\d+[.)])\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex ImageLine = new(@"^!\[([^\]]*)\]\(([^)]+)\)\s*$", RegexOptions.Compiled);
    private static readonly Regex TableSeparator = new(@"^\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$", RegexOptions.Compiled);

    public static IReadOnlyList<Block> Parse(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var blocks = new List<Block>();
        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.Length == 0) { i++; continue; }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var language = trimmed[3..].Trim();
                var code = new List<string>();
                i++;
                while (i < lines.Length && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal)) code.Add(lines[i++]);
                i++;
                blocks.Add(new CodeBlock(string.Join("\n", code), language));
                continue;
            }
            if (trimmed.StartsWith('#'))
            {
                var level = trimmed.TakeWhile(c => c == '#').Count();
                if (level <= 6 && trimmed.Length > level && trimmed[level] == ' ')
                {
                    blocks.Add(new HeadingBlock(level, trimmed[level..].Trim()));
                    i++;
                    continue;
                }
            }
            if (trimmed is "---" or "***" or "___")
            {
                blocks.Add(new RuleBlock());
                i++;
                continue;
            }
            var image = ImageLine.Match(trimmed);
            if (image.Success)
            {
                blocks.Add(new ImageBlock(image.Groups[1].Value, image.Groups[2].Value));
                i++;
                continue;
            }
            if (trimmed.StartsWith('>'))
            {
                var quote = new List<string>();
                while (i < lines.Length && lines[i].Trim().StartsWith('>')) quote.Add(lines[i++].Trim().TrimStart('>').Trim());
                blocks.Add(new QuoteBlock(string.Join(" ", quote.Where(q => q.Length > 0))));
                continue;
            }
            if (trimmed.StartsWith('|') && i + 1 < lines.Length && TableSeparator.IsMatch(lines[i + 1].Trim()))
            {
                var header = Cells(trimmed);
                i += 2;
                var rows = new List<IReadOnlyList<string>>();
                while (i < lines.Length && lines[i].Trim().StartsWith('|')) rows.Add(Cells(lines[i++].Trim()));
                blocks.Add(new TableBlock(header, rows));
                continue;
            }
            if (ListLine.IsMatch(line))
            {
                blocks.Add(ParseList(lines, ref i, Indent(line)));
                continue;
            }

            // a paragraph: consecutive lines that are none of the above, joined, since the source is hard-wrapped
            var paragraph = new List<string>();
            while (i < lines.Length)
            {
                var t = lines[i].Trim();
                if (t.Length == 0 || t.StartsWith("```", StringComparison.Ordinal) || (t.StartsWith('#') && t.Contains("# ")) || t.StartsWith('>') || ListLine.IsMatch(lines[i]) || ImageLine.IsMatch(t)
                    || (t.StartsWith('|') && i + 1 < lines.Length && TableSeparator.IsMatch(lines[i + 1].Trim())))
                {
                    break;
                }
                paragraph.Add(t);
                i++;
            }
            if (paragraph.Count > 0) blocks.Add(new ParagraphBlock(string.Join(" ", paragraph)));
            else i++;
        }
        return blocks;
    }

    private static int Indent(string line) => line.TakeWhile(c => c == ' ' || c == '\t').Sum(c => c == '\t' ? 4 : 1);

    private static ListBlock ParseList(string[] lines, ref int i, int indent)
    {
        var items = new List<ListItem>();
        bool? ordered = null;
        while (i < lines.Length)
        {
            var line = lines[i];
            if (line.Trim().Length == 0)
            {
                // a blank line ends the list unless another item at this depth follows it
                if (i + 1 < lines.Length && ListLine.IsMatch(lines[i + 1]) && Indent(lines[i + 1]) >= indent) { i++; continue; }
                break;
            }
            var match = ListLine.Match(line);
            if (!match.Success) break;
            var depth = Indent(line);
            if (depth < indent) break;
            if (depth > indent) break;   // handled by the item that owns it
            var isOrdered = char.IsDigit(match.Groups[2].Value[0]);
            if (ordered is not null && ordered != isOrdered) break;   // a numbered list after a bulleted one is a new list
            ordered ??= isOrdered;
            var text = match.Groups[3].Value.Trim();
            i++;
            // continuation lines, indented and not items themselves
            while (i < lines.Length && lines[i].Trim().Length > 0 && !ListLine.IsMatch(lines[i]) && Indent(lines[i]) > indent)
            {
                text += " " + lines[i].Trim();
                i++;
            }
            ListBlock? nested = null;
            if (i < lines.Length && ListLine.IsMatch(lines[i]) && Indent(lines[i]) > indent)
            {
                nested = ParseList(lines, ref i, Indent(lines[i]));
            }
            items.Add(new ListItem(text, nested));
        }
        return new ListBlock(ordered ?? false, items);
    }

    private static IReadOnlyList<string> Cells(string row)
    {
        var inner = row.Trim();
        if (inner.StartsWith('|')) inner = inner[1..];
        if (inner.EndsWith('|')) inner = inner[..^1];
        // a pipe inside backticks is content, not a column
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        var inCode = false;
        foreach (var c in inner)
        {
            if (c == '`') inCode = !inCode;
            if (c == '|' && !inCode) { cells.Add(current.ToString().Trim()); current.Clear(); }
            else current.Append(c);
        }
        cells.Add(current.ToString().Trim());
        return cells;
    }
}

/// <summary>One piece of a paragraph after the inline marks are read.</summary>
public sealed record InlineSpan(string Text, bool Bold, bool Italic, bool Code, string? Link, string? LinkAnchor, bool IsWikiLink);

/// <summary>
/// Reads the inline marks: `code`, **bold**, *italic*, [text](url), and the [[wikilinks]] the pages
/// use for one another, with an optional [[page#Heading]] anchor and [[page|shown as this]] label.
/// </summary>
public static class InlineText
{
    private static readonly Regex Wiki = new(@"^\[\[([^\]\|#]+)(?:#([^\]\|]*))?(?:\|([^\]]*))?\]\]", RegexOptions.Compiled);
    private static readonly Regex Link = new(@"^\[([^\]]+)\]\(([^)\s]+)\)", RegexOptions.Compiled);

    public static IReadOnlyList<InlineSpan> Parse(string text)
    {
        var spans = new List<InlineSpan>();
        var buffer = new System.Text.StringBuilder();
        bool bold = false, italic = false;
        void Flush()
        {
            if (buffer.Length == 0) return;
            spans.Add(new InlineSpan(buffer.ToString(), bold, italic, false, null, null, false));
            buffer.Clear();
        }
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\\' && i + 1 < text.Length) { buffer.Append(text[i + 1]); i += 2; continue; }
            if (c == '`')
            {
                var end = text.IndexOf('`', i + 1);
                if (end > i)
                {
                    Flush();
                    spans.Add(new InlineSpan(text[(i + 1)..end], bold, italic, true, null, null, false));
                    i = end + 1;
                    continue;
                }
            }
            if (c == '[' && i + 1 < text.Length && text[i + 1] == '[')
            {
                var m = Wiki.Match(text[i..]);
                if (m.Success)
                {
                    Flush();
                    var target = m.Groups[1].Value.Trim();
                    var anchor = m.Groups[2].Success ? m.Groups[2].Value.Trim() : null;
                    var label = m.Groups[3].Success ? m.Groups[3].Value : (anchor is { Length: > 0 } ? $"{target} › {anchor}" : target);
                    spans.Add(new InlineSpan(label, bold, italic, false, target, anchor, true));
                    i += m.Length;
                    continue;
                }
            }
            if (c == '[')
            {
                var m = Link.Match(text[i..]);
                if (m.Success)
                {
                    Flush();
                    spans.Add(new InlineSpan(m.Groups[1].Value, bold, italic, false, m.Groups[2].Value, null, false));
                    i += m.Length;
                    continue;
                }
            }
            if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                Flush();
                bold = !bold;
                i += 2;
                continue;
            }
            if (c == '*' && (italic || (i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1]))))
            {
                Flush();
                italic = !italic;
                i++;
                continue;
            }
            buffer.Append(c);
            i++;
        }
        Flush();
        return spans;
    }

    /// <summary>The paragraph with every mark removed: what a search reads and what a test compares.</summary>
    public static string Plain(string text) => string.Concat(Parse(text).Select(s => s.Text));
}
