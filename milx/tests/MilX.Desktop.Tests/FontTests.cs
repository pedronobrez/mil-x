using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Xunit;

namespace MilX.Desktop.Tests;

/// <summary>
/// The two faces the interface uses are carried in the application, not borrowed from the machine.
/// If either stopped resolving, everything would still render — silently, in whatever the machine
/// offered instead — and every stored frame would start describing this machine again.
/// </summary>
public class FontTests
{
    private static double WidthOf(string family, string text)
    {
        var block = new TextBlock { Text = text, FontFamily = new FontFamily(family), FontSize = 14 };
        block.Measure(Avalonia.Size.Infinity);
        return block.DesiredSize.Width;
    }

    [AvaloniaFact]
    public void The_monospace_face_is_the_one_carried_with_the_application()
    {
        // every glyph the same width is what makes a column of masses line up
        var narrow = WidthOf("avares://MIL-X/Assets/Fonts#JetBrains Mono NL", "iiiiiiiiii");
        var wide = WidthOf("avares://MIL-X/Assets/Fonts#JetBrains Mono NL", "MMMMMMMMMM");
        Assert.Equal(narrow, wide, 1);

        // and it is not the fallback: a family that does not exist gives the machine's own face
        var missing = WidthOf("No Such Family At All", "MMMMMMMMMM");
        Assert.NotEqual(missing, wide, 1);
    }

    [AvaloniaFact]
    public void The_text_face_is_Inter_and_it_is_what_unnamed_text_gets()
    {
        // it is really loaded, not a name that quietly resolves to something else
        Assert.Contains(FontManager.Current.SystemFonts, f => f.Name.Contains("Inter", StringComparison.OrdinalIgnoreCase));

        // and it is the default, so text that names no family is drawn in it
        var inter = WidthOf("Inter", "Annotated only");
        var unnamed = WidthOf("No Such Family At All", "Annotated only");
        Assert.Equal(inter, unnamed, 1);
    }
}
