using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MilX.Desktop.Help;

/// <summary>A paragraph whose links answer a click. Where each link sits in the text is remembered as it is built.</summary>
public sealed class LinkTextBlock : TextBlock
{
    private readonly List<(int Start, int End, string Target, string? Anchor, bool Wiki)> _links = new();
    private int _length;

    /// <summary>Raised with the link target (a slug for a wikilink, a URL otherwise) and its anchor.</summary>
    public event Action<string, string?, bool>? LinkClicked;

    public void AddRun(string text, bool bold, bool italic, bool code, bool highlighted)
    {
        var run = new Run(text);
        if (bold) run.FontWeight = FontWeight.SemiBold;
        if (italic) run.FontStyle = FontStyle.Italic;
        if (code)
        {
            run.FontFamily = (FontFamily)(Application.Current?.FindResource("OdMonoFont") ?? FontFamily.Default);
            run.FontSize = 12;
        }
        Inlines!.Add(run);   // in the tree first, so the brushes resolve against the window's theme
        if (highlighted) Themed(run, TextElement.BackgroundProperty, "OdWarningSoft", Colors.LightYellow);
        else if (code) Themed(run, TextElement.BackgroundProperty, "OdSurfaceAlt", Colors.WhiteSmoke);
        _length += text.Length;
    }

    public void AddLink(string text, string target, string? anchor, bool wiki, bool bold, bool highlighted)
    {
        var run = new Run(text) { TextDecorations = wiki ? null : Avalonia.Media.TextDecorations.Underline };
        if (bold) run.FontWeight = FontWeight.SemiBold;
        Inlines!.Add(run);
        Themed(run, TextElement.ForegroundProperty, "OdAccent", Colors.RoyalBlue);
        if (highlighted) Themed(run, TextElement.BackgroundProperty, "OdWarningSoft", Colors.LightYellow);
        _links.Add((_length, _length + text.Length, target, anchor, wiki));
        _length += text.Length;
    }

    private (string Target, string? Anchor, bool Wiki)? LinkAt(Point point)
    {
        if (_links.Count == 0 || TextLayout is null) return null;
        var hit = TextLayout.HitTestPoint(point);
        if (!hit.IsInside) return null;
        var position = hit.TextPosition;
        foreach (var link in _links)
        {
            if (position >= link.Start && position < link.End) return (link.Target, link.Anchor, link.Wiki);
        }
        return null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Cursor = LinkAt(e.GetPosition(this)) is null ? Cursor.Default : new Cursor(StandardCursorType.Hand);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        if (LinkAt(e.GetPosition(this)) is { } link)
        {
            LinkClicked?.Invoke(link.Target, link.Anchor, link.Wiki);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Gives a property the theme's brush, and keeps it there. The brush is resolved through the
    /// element once it is in a window, not through the application while the page is being built:
    /// looked up that way, a dark window got the light surface behind its code, and white on white.
    /// </summary>
    internal static void Themed(StyledElement target, AvaloniaProperty property, string key, Color fallback) =>
        target.Bind(property, target.GetResourceObservable(key, v => v as IBrush ?? new SolidColorBrush(fallback)));
}

/// <summary>
/// Turns a page's blocks into controls. Headings carry their text as Tag so a [[page#Heading]] link
/// can scroll to them, and every link in every paragraph reports to one handler.
/// </summary>
public sealed class MarkdownRenderer
{
    private readonly Action<string, string?, bool> _onLink;
    private readonly string _highlight;

    public MarkdownRenderer(Action<string, string?, bool> onLink, string? highlight = null)
    {
        _onLink = onLink;
        _highlight = highlight?.Trim() ?? string.Empty;
    }

    public IReadOnlyList<Control> Render(IReadOnlyList<Block> blocks)
    {
        var controls = new List<Control>();
        foreach (var block in blocks)
        {
            var control = Render(block);
            if (control is not null) controls.Add(control);
        }
        return controls;
    }

    private Control? Render(Block block) => block switch
    {
        HeadingBlock h => Heading(h),
        ParagraphBlock p => Paragraph(p.Text, 13, new Thickness(0, 0, 0, 10)),
        CodeBlock c => Code(c),
        QuoteBlock q => Quote(q),
        ImageBlock i => Image(i),
        RuleBlock => Rule(),
        ListBlock l => List(l, 0),
        TableBlock t => Table(t),
        _ => null,
    };

    private static Control Rule()
    {
        var rule = new Border { Height = 1, Margin = new Thickness(0, 8, 0, 14) };
        LinkTextBlock.Themed(rule, Border.BackgroundProperty, "OdLine", Colors.LightGray);
        return rule;
    }

    private Control Heading(HeadingBlock h)
    {
        var block = Paragraph(h.Text, h.Level switch { 1 => 22, 2 => 17, 3 => 14.5, _ => 13 }, new Thickness(0, h.Level == 1 ? 0 : 16, 0, h.Level == 1 ? 12 : 6));
        block.FontWeight = FontWeight.SemiBold;
        block.Tag = InlineText.Plain(h.Text);
        block.Classes.Add("heading");
        block.Classes.Add("h" + Math.Min(h.Level, 4));
        return block;
    }

    private LinkTextBlock Paragraph(string text, double size, Thickness margin)
    {
        var block = new LinkTextBlock { TextWrapping = TextWrapping.Wrap, FontSize = size, Margin = margin, LineHeight = size * 1.5 };
        block.Inlines = new InlineCollection();
        block.LinkClicked += (target, anchor, wiki) => _onLink(target, anchor, wiki);
        foreach (var span in InlineText.Parse(text))
        {
            if (span.Link is not null)
            {
                block.AddLink(span.Text, span.Link, span.LinkAnchor, span.IsWikiLink, span.Bold, false);
                continue;
            }
            foreach (var (piece, marked) in Split(span.Text))
            {
                block.AddRun(piece, span.Bold, span.Italic, span.Code, marked);
            }
        }
        return block;
    }

    /// <summary>Cuts a run around the highlighted words, so only they are marked.</summary>
    private IEnumerable<(string Text, bool Marked)> Split(string text)
    {
        var words = _highlight.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || text.Length == 0) { yield return (text, false); yield break; }
        var at = 0;
        while (at < text.Length)
        {
            var best = -1;
            var bestLength = 0;
            foreach (var word in words)
            {
                var index = text.IndexOf(word, at, StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && (best < 0 || index < best)) { best = index; bestLength = word.Length; }
            }
            if (best < 0) { yield return (text[at..], false); yield break; }
            if (best > at) yield return (text[at..best], false);
            yield return (text[best..(best + bestLength)], true);
            at = best + bestLength;
        }
    }

    private static Control Code(CodeBlock c)
    {
        var text = new SelectableTextBlock
        {
            Text = c.Text,
            FontFamily = (FontFamily)(Application.Current?.FindResource("OdMonoFont") ?? FontFamily.Default),
            FontSize = 11.5,
            TextWrapping = TextWrapping.NoWrap,
        };
        var box = new Border
        {
            Classes = { "panel" },
            Padding = new Thickness(12, 8),
            Margin = new Thickness(0, 2, 0, 12),
            Child = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = text },
        };
        LinkTextBlock.Themed(box, Border.BackgroundProperty, "OdSurfaceAlt", Colors.WhiteSmoke);
        return box;
    }

    private Control Quote(QuoteBlock q)
    {
        var paragraph = Paragraph(q.Text, 13, new Thickness(0));
        LinkTextBlock.Themed(paragraph, TextBlock.ForegroundProperty, "OdInkMuted", Colors.Gray);
        var quote = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 2, 0, 2),
            Margin = new Thickness(0, 2, 0, 12),
            Child = paragraph,
        };
        LinkTextBlock.Themed(quote, Border.BorderBrushProperty, "OdAccentSoft", Colors.LightBlue);
        return quote;
    }

    private static Control Image(ImageBlock i)
    {
        Bitmap? bitmap = null;
        try
        {
            var name = "Manual/" + i.Source.Replace('\\', '/');
            using var stream = typeof(MarkdownRenderer).Assembly.GetManifestResourceStream(name);
            if (stream is not null) bitmap = new Bitmap(stream);
        }
        catch
        {
            bitmap = null;
        }
        if (bitmap is null)
        {
            return new TextBlock { Text = $"[image: {i.Alt}]", Classes = { "hint" }, Margin = new Thickness(0, 0, 0, 10) };
        }
        var image = new Avalonia.Controls.Image { Source = bitmap, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, MaxWidth = 900 };
        var caption = string.IsNullOrWhiteSpace(i.Alt) ? null : new TextBlock { Text = i.Alt, Classes = { "hint" }, Margin = new Thickness(0, 4, 0, 0) };
        var stack = new StackPanel { Margin = new Thickness(0, 4, 0, 14) };
        var frame = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = image, HorizontalAlignment = HorizontalAlignment.Left };
        LinkTextBlock.Themed(frame, Border.BorderBrushProperty, "OdLine", Colors.LightGray);
        stack.Children.Add(frame);
        if (caption is not null) stack.Children.Add(caption);
        return stack;
    }

    private Control List(ListBlock list, int depth)
    {
        var stack = new StackPanel { Margin = new Thickness(depth == 0 ? 4 : 18, 0, 0, depth == 0 ? 10 : 0), Spacing = 3 };
        var number = 1;
        foreach (var item in list.Items)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("22,*") };
            var marker = new TextBlock { Text = list.Ordered ? $"{number++}." : "•", VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
            LinkTextBlock.Themed(marker, TextBlock.ForegroundProperty, "OdInkMuted", Colors.Gray);
            var text = Paragraph(item.Text, 13, new Thickness(0));
            Grid.SetColumn(text, 1);
            row.Children.Add(marker);
            row.Children.Add(text);
            stack.Children.Add(row);
            if (item.Nested is not null) stack.Children.Add(List(item.Nested, depth + 1));
        }
        return stack;
    }

    private Control Table(TableBlock t)
    {
        var columns = Math.Max(t.Header.Count, t.Rows.Count == 0 ? 0 : t.Rows.Max(r => r.Count));
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 14) };
        for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(c == columns - 1 ? GridLength.Star : GridLength.Auto) { MaxWidth = 420 });
        void Add(IReadOnlyList<string> cells, int row, bool header)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < columns; c++)
            {
                var text = c < cells.Count ? cells[c] : string.Empty;
                var block = Paragraph(text, 12.5, new Thickness(0));
                if (header) { block.FontWeight = FontWeight.SemiBold; LinkTextBlock.Themed(block, TextBlock.ForegroundProperty, "OdInkMuted", Colors.Gray); }
                var cell = new Border
                {
                    Child = block,
                    Padding = new Thickness(8, 5),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                };
                LinkTextBlock.Themed(cell, Border.BorderBrushProperty, "OdLine", Colors.LightGray);
                if (header) LinkTextBlock.Themed(cell, Border.BackgroundProperty, "OdSurfaceAlt", Colors.WhiteSmoke);
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }
        Add(t.Header, 0, true);
        for (var r = 0; r < t.Rows.Count; r++) Add(t.Rows[r], r + 1, false);
        return new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = grid };
    }
}
