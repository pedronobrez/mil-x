using Avalonia;

namespace OpenDIAL.Desktop.Controls;

/// <summary>
/// The zoom and the pan of a graph that is drawn to fit, and the arithmetic that keeps a point of
/// the drawing under the pointer while the wheel turns.
///
/// The networks and the pathway map used to be pictures: laid out to fit the panel, and that was
/// all. A network of two hundred features does not fit anything legibly, so the drawing has to be
/// something you can go into. Both graphs push the same transform around what they draw and send
/// pointer positions back through it before they hit-test, so what the eye picks is what the
/// pointer picks.
/// </summary>
internal sealed class GraphView
{
    private const double Smallest = 0.4;
    private const double Largest = 12;

    public double Zoom { get; private set; } = 1;
    public Vector Pan { get; private set; }

    public bool IsMoved => Math.Abs(Zoom - 1) > 1e-6 || Pan != default;

    /// <summary>The transform the drawing is made under.</summary>
    public Matrix Matrix => Matrix.CreateScale(Zoom, Zoom) * Matrix.CreateTranslation(Pan.X, Pan.Y);

    /// <summary>A point of the panel in the coordinates the graph draws in.</summary>
    public Point ToDrawing(Point onScreen) => new((onScreen.X - Pan.X) / Zoom, (onScreen.Y - Pan.Y) / Zoom);

    /// <summary>A vector dragged across the panel, in the coordinates the graph draws in.</summary>
    public Vector ToDrawing(Vector onScreen) => new(onScreen.X / Zoom, onScreen.Y / Zoom);

    public void Reset()
    {
        Zoom = 1;
        Pan = default;
    }

    public void MoveBy(Vector delta) => Pan += delta;

    /// <summary>Zooms about a point of the panel, which stays where it is under the pointer.</summary>
    public bool ZoomAbout(Point anchor, double factor)
    {
        var wanted = Math.Clamp(Zoom * factor, Smallest, Largest);
        if (Math.Abs(wanted - Zoom) < 1e-9) return false;
        var ratio = wanted / Zoom;
        Pan = new Vector(anchor.X - (anchor.X - Pan.X) * ratio, anchor.Y - (anchor.Y - Pan.Y) * ratio);
        Zoom = wanted;
        return true;
    }
}
