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
| `help-window` | the manual open at the tags page, with its table of contents |

A layout defect this class of test would have caught, and did not because the frame was never
captured at a laptop's width: the review filter row drawing straight over the counts beside it.
That one is now held by a direct assertion in `ReviewWorkspaceTests` instead, comparing the two
bounds at 1280 points wide, because an assertion about two rectangles says what it means more
plainly than a picture does. Prefer that shape where the property can be named.

## The smoke test

Everything above tests the source. None of it tests the thing that gets installed, and a defect that
only appears there passes the whole suite: an asset that did not make it into the bundle, a font
that stopped resolving, a plugin that fails to load, a shortcut that never fires. That last one is
not hypothetical. Every workspace shortcut had been dead since the day it was written and nothing
noticed, because nothing had ever pressed one in the packaged application.

```bash
opendial/scripts/smoke-ui.py --project ~/…/Project-2609071200.mdproject
opendial/scripts/smoke-ui.py --process ~/…/raw-folder --method method.txt --library library.msp
```

Both launch `/Applications/OpenDIAL.app` through LaunchServices, the way a person does, with an
isolated settings folder so the run neither reads nor rewrites the person's own recent projects.

The first form opens a real project and then checks: the title names the project, the ion table
filled, opening a processed project lands on the review workspace, each of the five shortcuts
selects the workspace it names on both the command and the control key, a click on the Statistics
tab selects it, the process is still alive, and nothing unhandled reached stdout or stderr.

The second form covers the work, not the shell. It writes an `.odproj` from the raw files in the
folder (typing the injections by name: `BK` is a blank, `Eq` a control), presses Cmd+R, waits for
the run, and checks that the SCIEX plugin inside the bundle read every `.wiff` natively, that the
alignment table and the MS-DIAL project were written, and that the OpenDIAL project now points at
them. Then it does what a reviewer does, through the keyboard and the mouse: types the selected
feature's id into the filter box and sees the table narrow to it, types a narrower integration
window into the two boxes and clicks **Apply to all**, checks that every sample now sits inside the
window and that heights changed, clicks **Save review** and checks the tag file and the
`.before-curation` backups on disk, exports the reviewed table and the OpenQuant component list and
reads them back (the edited feature is marked *Manually quantified*), presses F1 and sees the
manual open at the review page, and searches it. `--limit N` processes only the first N files;
`--bundle` drives a different build, `--shots` keeps the screenshots, `--keep` leaves it running.

It has teeth. Rebuild with `Gesture="Cmd+5"` put back and it passes the first four shortcuts and
stops on the fifth: *timed out after 10 s waiting for Cmd+5 to select Statistics*, with the state
the window was still reporting printed underneath.

### How it can assert anything

Reading state off a screenshot is guesswork, so the application says it plainly instead. When
`OPENDIAL_UI_PROBE` names a file, the window writes a small JSON snapshot to it whenever what it is
showing changes: the title, the status line, the selected workspace, whether results are loaded, how
many rows the ion table holds, the run's stage and log tail, the selected feature with its
per-sample windows and heights, whether the review has unsaved edits, and what the statistics
workspace has computed. The shipped
application writes nothing, because nothing sets that variable. `UiProbeTests` holds the shape of
that file, since a rename here would otherwise only break a script nobody runs on every commit.

One thing flows the other way. Beside the probe the application watches `<probe>.commands` and
runs what it finds — `exportReviewed`, `exportOpenQuant`, `reexport`, `openHelp`, `closeHelp` —
through the same view-model methods the menu runs once its dialog has closed. The script needs
that for exactly one kind of step: an export goes through the operating system's own save panel,
which is not this application's code to drive, so the path is handed over and everything after the
panel runs for real. The result of each command comes back in the next snapshot.

The snapshot also carries where the workspace tabs, the evidence tabs and every named control
are — inside the window, in layout units, not on
the screen. Turning that into a place to click means knowing where the window frame is and how tall
its title bar is, and those belong to the window manager rather than to the application: the script
reads the frame from System Events, takes the difference against the client size the probe reports,
and adds the offset. A script that instead worked its coordinates out from a screenshot breaks the
moment the window moves.

### Driving it by hand

`scripts/ui-drive.sh` does the same primitives one at a time, for a screenshot or a look:

```bash
opendial/scripts/ui-drive.sh front
opendial/scripts/ui-drive.sh click 372 108     # the Statistics workspace tab
opendial/scripts/ui-drive.sh key 23 cmd        # Cmd+5, the same thing by keyboard
opendial/scripts/ui-drive.sh shot /tmp/now.png
```

Three things make this less obvious than it looks, all found the hard way:

**Clicks need real mouse events.** System Events' `click at` asks the accessibility layer to press a
control, and Avalonia publishes almost nothing to that layer, so a click at a coordinate does
nothing whatsoever — no error, no effect. `cliclick` posts the events a mouse would (`brew install
cliclick`), and those work everywhere: workspace tabs, panel tabs, buttons, a row of a table.

**Keystrokes need the physical key.** They do arrive, but `keystroke "5" using command down` sends a
character event that never reaches a shortcut, while `key code 23 using command down` sends the key
and does.

**The window has to be frontmost before the click, not after.** Otherwise the click only activates
it and is swallowed. Both scripts front the process on every command, and `ui-drive.sh` puts the
pointer back where it found it, because this runs on somebody's actual desk.

Typing works the same way: `cliclick t:text` types into whatever has the focus, so a text box is
clicked first, `Cmd+A` selects what is in it, and the new value is typed over it.

One more: `open --env` cannot apply an environment to an instance that already exists, and fails
with a bare `-600` rather than saying so. The smoke test makes sure nothing is running first, which
takes some insisting, because a polite quit is refused while the window is asking whether to discard
an unsaved project.

Coordinates are screen points with the origin top left, which on a Retina display is half the pixel
coordinate read off a screenshot.

And one that no script can get past: the first time the application — or the process that launched
it, which is what macOS holds responsible for an application started with `open` — reads a
protected folder, macOS shows a privacy prompt and the read blocks in the kernel until a person
answers it. The run then sits at *Processing 10 %* with the process idle. Answer the prompt;
the read resumes on its own. The script neither can nor should click it.

What this confirmed on the real eight-injection run, entirely through clicks: the orthogonal model
refuses that batch with "separates two classes, and this batch has 4"; the plain discriminant fits
it and reports R²Y 0.79, Q² 0.38 and p 0.005, with the verdict saying plainly that a Q² that low
does not survive cross-validation; and clicking a row of the VIP table jumps to the review workspace
with that exact feature open.
