using System.Runtime.InteropServices;
using Avalonia;

namespace OpenDIAL.Desktop.Services;

/// <summary>
/// The geometry of docking the ion table back by dragging its window over the main one: where the
/// drop zone is, and whether a dragged window is over it. Pure, so it is tested without a screen.
/// </summary>
public static class DockSnap
{
    /// <summary>The share of the main window's width, from its left edge, that counts as the drop zone.</summary>
    public const double ZoneShare = 0.4;

    /// <summary>
    /// How far a window has to have moved from where it appeared before a move counts as a drag,
    /// so a window that opens over the zone is not docked by the click that opened it.
    /// </summary>
    public const int MinimumDragPixels = 24;

    /// <summary>
    /// The point of the dragged window taken as the hand that holds it: the middle of its title bar.
    /// The pointer's own position is not known while the window manager moves the window, and a
    /// title bar is held near its middle often enough for this to feel right.
    /// </summary>
    public static PixelPoint Grip(PixelPoint position, PixelSize frame, double scaling) =>
        new(position.X + frame.Width / 2, position.Y + (int)Math.Round(12 * scaling));

    /// <summary>The drop zone: the left part of the main window's frame, its full height.</summary>
    public static PixelRect Zone(PixelRect mainFrame) =>
        new(mainFrame.X, mainFrame.Y, (int)Math.Round(mainFrame.Width * ZoneShare), mainFrame.Height);

    /// <summary>Whether the hand holding the dragged window is over the drop zone.</summary>
    public static bool IsOverZone(PixelRect mainFrame, PixelPoint draggedPosition, PixelSize draggedFrame, double scaling) =>
        Zone(mainFrame).Contains(Grip(draggedPosition, draggedFrame, scaling));

    /// <summary>Whether a move from where the window appeared is far enough to be a drag.</summary>
    public static bool IsADrag(PixelPoint shownAt, PixelPoint now) =>
        Math.Abs(now.X - shownAt.X) >= MinimumDragPixels || Math.Abs(now.Y - shownAt.Y) >= MinimumDragPixels;
}

/// <summary>
/// Whether the left mouse button is down right now, asked of the platform. A window being dragged
/// by its title bar sends no pointer events to the application — the window manager has the mouse
/// — so the end of the drag can only be seen by asking whether the button is still held.
/// </summary>
public static class MouseButtons
{
    /// <summary>True or false when the platform can say; null where it cannot, and snapping is then off.</summary>
    public static bool? LeftIsDown()
    {
        try
        {
            if (OperatingSystem.IsMacOS()) return (PressedMouseButtons() & 1) != 0;
            if (OperatingSystem.IsWindows()) return (GetAsyncKeyState(0x01) & 0x8000) != 0;
        }
        catch (Exception)
        {
            // a missing symbol or a sandbox that refuses: better no snapping than a crash
        }
        return null;
    }

    private static IntPtr _nsEvent;
    private static IntPtr _pressedMouseButtons;

    private static nuint PressedMouseButtons()
    {
        if (_nsEvent == IntPtr.Zero)
        {
            _nsEvent = objc_getClass("NSEvent");
            _pressedMouseButtons = sel_registerName("pressedMouseButtons");
        }
        return objc_msgSend_nuint(_nsEvent, _pressedMouseButtons);
    }

    [DllImport("/usr/lib/libobjc.A.dylib")] private static extern IntPtr objc_getClass(string name);
    [DllImport("/usr/lib/libobjc.A.dylib")] private static extern IntPtr sel_registerName(string name);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")] private static extern nuint objc_msgSend_nuint(IntPtr receiver, IntPtr selector);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
}
