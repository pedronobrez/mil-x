# ADR 0004: New desktop GUI on Avalonia instead of porting the WPF application

## Status
Accepted (2026-09-06)

## Context
`MsdialGuiApp` is 66 k lines of C# plus 171 XAML views, and depends on `ChartDrawing` (26 k lines,
custom WPF visual layer), `CommonMVVM`, `NCDK.Display` and `ReactiveProperty.WPF`. 67 of the
330 model classes import `System.Windows.Media` (brushes, pens, bitmaps), `MessageBox` and the
WPF `Dispatcher`. WPF has no macOS or Linux implementation and no automated converter to
Avalonia handles custom `DrawingVisual` rendering.

## Decision
Build a new, smaller desktop application that reuses OpenQuant's visual identity and shell
(four workspaces, token palette, toolbars as bands of type), so the two applications read as one suite.
Build it (`MilX.Desktop`) with Avalonia 11 and
CommunityToolkit.Mvvm on top of `MilX.Pipeline`, a headless orchestration library adapted
from the MS-DIAL console (`MsdialCoreTestApp`). Charts are custom Avalonia controls (EIC/TIC line
chart, spectrum stick chart with mirror plot, per-sample bar chart). The upstream model layer is
not reused; the upstream engine, parameter classes, project files and exporters are.

## Consequences
* A working, native macOS/Linux GUI for the core workflow (project → run → browse peaks/spectra/
  alignment → export) in a fraction of the WPF code base.
* Advanced Windows views (statistics with R, pathway maps, molecular networking browser,
  imaging, MS-FINDER integration, manual peak curation) are out of scope for the MVP and can be
  added incrementally; `.mdproject` files remain interchangeable with the Windows GUI.
