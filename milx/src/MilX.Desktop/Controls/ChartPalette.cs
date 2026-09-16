using Avalonia.Media;

namespace MilX.Desktop.Controls;

/// <summary>
/// The colours every chart in the application draws with, in one place so a control that does not
/// derive from <see cref="ChartBase"/> still looks like the rest.
/// </summary>
public static class ChartPalette
{
    public static readonly Color[] Categorical =
    {
        Color.Parse("#E15759"), Color.Parse("#59A14F"), Color.Parse("#F28E2B"), Color.Parse("#B07AA1"), Color.Parse("#76B7B2"),
        Color.Parse("#EDC948"), Color.Parse("#9C755F"), Color.Parse("#FF9DA7"), Color.Parse("#4E79A7"), Color.Parse("#BAB0AC"),
    };

    public static Color Ink(bool dark) => dark ? Color.Parse("#e6e8eb") : Color.Parse("#1a1d21");
    public static Color Muted(bool dark) => dark ? Color.Parse("#9ba3ae") : Color.Parse("#6b7280");
    public static Color Faint(bool dark) => dark ? Color.Parse("#6b7280") : Color.Parse("#9aa1ac");
    public static Color Line(bool dark) => dark ? Color.Parse("#2c3035") : Color.Parse("#e3e5ea");
    public static Color LineStrong(bool dark) => dark ? Color.Parse("#3c4148") : Color.Parse("#cfd3da");
    public static Color Surface(bool dark) => dark ? Color.Parse("#1e2124") : Color.Parse("#ffffff");
    public static Color Accent(bool dark) => dark ? Color.Parse("#6f9be0") : Color.Parse("#234b8c");

    /// <summary>Series colour by index: accent first, then the categorical palette.</summary>
    public static Color ForIndex(bool dark, int index) =>
        index == 0 ? Accent(dark) : Categorical[(index - 1) % Categorical.Length];
}
