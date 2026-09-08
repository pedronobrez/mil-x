# The visual regression tests

The headless interface tests catch a broken binding. They do not catch a broken layout: a panel that
collapses to nothing, a strip that overflows into the one beside it, a column that clips its own
text — all of those bind perfectly and look wrong. The tests in
`tests/OpenDIAL.Desktop.Tests/VisualRegressionTests.cs` render a workspace with Skia, capture the
frame, and compare it with a stored one under `Baselines/`.

```bash
dotnet test opendial/tests/OpenDIAL.Desktop.Tests/OpenDIAL.Desktop.Tests.csproj -c Release -p:UseOpenRawData=true
```

## Why a stored frame usually cannot travel, and why these do

The obvious objection to storing rendered frames is that they describe the machine that made them.
Three things make that true, and all three are dealt with:

**The fonts.** This was the real problem. Text is most of what is on screen, and a different face
means different advance widths, which means different line breaks, different column widths and a
different layout — not a slightly different picture but a different one. The application ships both
faces it uses rather than borrowing them: Inter for the text, through `WithInterFont()`, and
JetBrains Mono NL for the columns of numbers, as an `AvaloniaResource` under `Assets/Fonts` with its
Open Font License beside it. Before this, `OdMonoFont` resolved to Menlo here, Consolas on Windows
and DejaVu on most Linuxes.

The test application did not build with the same font stack as the real one, so the frames described
neither. It does now, which is a small fix with a large effect: it is the difference between a
baseline that describes OpenDIAL and one that describes this laptop.

`FontTests` guards both. It measures a narrow string and a wide one in the monospace family and
requires them to be equal, which no proportional fallback would satisfy, and requires the family to
differ from a name that does not exist. It also asserts that Inter is loaded and is what unnamed
text is drawn in. Without those, a font that stopped resolving would fail silently: everything would
still render, in whatever the machine offered instead.

**The rasteriser.** Hinting and anti-aliasing still differ a little between machines and between
Skia versions, at the level of individual pixels along the edge of a glyph. The comparison averages
each three-by-three block into one value before comparing, which throws that away and keeps
everything that moved, resized or disappeared. A control that shifted by a pixel survives
coarsening; a glyph hinted a shade darker does not.

**The window size.** Every frame is captured at a size written into the test, never at whatever the
screen happens to be.

What is left is a threshold: at most one per cent of the coarsened blocks may differ by more than a
little. It is a real bound, not a rubber stamp. Blanking a strip of about a tenth of the width and a
seventh of the height is measured at 1.56 % and fails the test.

## When one fails

Look at the frame before doing anything else. The failure message names a file in the temporary
directory holding what was actually rendered; open it next to the baseline. Either the interface
changed on purpose, or it broke.

If it changed on purpose:

```bash
OPENDIAL_UPDATE_BASELINES=1 dotnet test opendial/tests/OpenDIAL.Desktop.Tests/OpenDIAL.Desktop.Tests.csproj -c Release -p:UseOpenRawData=true
```

That rewrites every baseline from the current render, into the source tree rather than into `bin`.
Look at what it wrote before committing it: an update is the one operation here that can quietly
bless a regression.

The first run on a machine with no baseline at all records one instead of failing, so a fresh clone
does not open with five red tests. That is a convenience and a small hole: a baseline recorded on a
machine where something is already wrong will happily agree with itself. The committed baselines are
the reference; delete a local one only to re-record it deliberately.

## What is covered

| Baseline | What it holds |
| --- | --- |
| `review-workspace` | the ion table beside the evidence, the filter band, the review panels |
| `ion-table-window` | the same table torn off into a window of its own |
| `statistics-drift` | the correction table and the before-and-after traces |
| `statistics-discriminant` | the score plot and the VIP table |
| `statistics-orthogonal` | the predictive-against-orthogonal scores and the S-plot |

A layout defect this class of test would have caught, and did not because the frame was never
captured at a laptop's width: the review filter row drawing straight over the counts beside it.
That one is now held by a direct assertion in `ReviewWorkspaceTests` instead, comparing the two
bounds at 1280 points wide, because an assertion about two rectangles says what it means more
plainly than a picture does. Prefer that shape where the property can be named.

## Driving the running application

Headless frames catch layout. They do not catch the application actually being wrong on a real
result, so it is worth driving the installed build now and then. `scripts/ui-drive.sh` does it:

```bash
opendial/scripts/ui-drive.sh front
opendial/scripts/ui-drive.sh click 371 107     # the Statistics workspace tab
opendial/scripts/ui-drive.sh key 23 cmd        # Cmd+5, the same thing by keyboard
opendial/scripts/ui-drive.sh shot /tmp/now.png
opendial/scripts/ui-drive.sh where             # the cursor, for working coordinates out
```

Three things make this less obvious than it looks, all of them found the hard way:

**Clicks need real mouse events.** System Events' `click at` asks the accessibility layer to press a
control, and Avalonia publishes almost nothing to that layer, so a click at a coordinate does
nothing whatsoever — no error, no effect. `cliclick` posts the events a mouse would (`brew install
cliclick`), and those work everywhere in the interface: workspace tabs, panel tabs, buttons, and a
row of a table.

**Keystrokes need the physical key.** They do arrive, but `keystroke "5" using command down` sends a
character event that never reaches a shortcut, while `key code 23 using command down` sends the key
and does. Driving it this way is what turned up the fact that none of the workspace shortcuts had
ever worked: `Gesture="Cmd+5"` parses without complaint and binds `Key.Clear`, because the parser
reads the digit as the numeric value of the key enumeration. They are `Cmd+D5` now.

**The window has to be frontmost before the click, not after.** Otherwise the click only activates
it and is swallowed. The script fronts the process on every command, and puts the pointer back where
it found it, because this runs on somebody's actual desk.

Coordinates are screen points with the origin top left, which on a Retina display is half the pixel
coordinate read off a screenshot.

What this confirmed on the real eight-injection run, entirely through clicks: the orthogonal model
refuses that batch with "separates two classes, and this batch has 4"; the plain discriminant fits
it and reports R²Y 0.79, Q² 0.38 and p 0.005, with the verdict saying plainly that a Q² that low
does not survive cross-validation; and clicking a row of the VIP table jumps to the review workspace
with that exact feature open.
