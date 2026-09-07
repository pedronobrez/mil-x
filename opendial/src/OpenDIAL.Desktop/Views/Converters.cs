using System.Globalization;
using Avalonia.Data.Converters;

namespace OpenDIAL.Desktop.Views;

public static class Converters
{
    /// <summary>True when the bound int equals the (string) parameter.</summary>
    public static readonly IValueConverter EqualsInt = new FuncValueConverter<int, string?, bool>((v, p) => int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n == v);

    /// <summary>Formats a double with the given format string, blank for NaN.</summary>
    public static readonly IValueConverter Number = new FuncValueConverter<double, string?, string>((v, p) => double.IsNaN(v) ? string.Empty : v.ToString(p ?? "0.###", CultureInfo.InvariantCulture));

    /// <summary>Compact intensity (1.23E5 above 1e5).</summary>
    public static readonly IValueConverter Intensity = new FuncValueConverter<double, string?, string>((v, _) => double.IsNaN(v) ? string.Empty : Controls.ChartBase.FormatIntensity(v));

    /// <summary>Label of the tear-off button, which reads as the action it will take.</summary>
    public static readonly IValueConverter DetachLabel = new FuncValueConverter<bool, string>(v => v ? "Dock table" : "Open in a window");

    public static readonly IValueConverter IsNotNull = new FuncValueConverter<object?, bool>(v => v is not null);
    public static readonly IValueConverter IsNull = new FuncValueConverter<object?, bool>(v => v is null);
    public static readonly IValueConverter NotEmpty = new FuncValueConverter<string?, bool>(v => !string.IsNullOrEmpty(v));
}
