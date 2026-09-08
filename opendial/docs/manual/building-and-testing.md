---
title: Building and testing
section: Under the hood
order: 52
summary: Building the application and the console from source, the test suites, the visual regression baselines, and the smoke test that drives the installed bundle.
---

# Building and testing

## Building

```bash
bash opendial/scripts/setup-macos.sh          # the .NET 8 SDK into ~/.dotnet, no sudo
bash opendial/scripts/fetch-sciex-assemblies.sh   # optional: the SCIEX SDK, for native .wiff
bash opendial/scripts/build-gui.sh            # -> opendial/dist/opendial-desktop-<rid>/
bash opendial/scripts/make-app-bundle.sh      # -> opendial/dist/OpenDIAL.app
ditto opendial/dist/OpenDIAL.app /Applications/OpenDIAL.app
bash opendial/scripts/build-cli.sh            # -> opendial/dist/opendial-cli-<rid>/
```

Every build passes `-p:UseOpenRawData=true`, the switch that replaces the closed reader with the
open one across the upstream tree. The bundle is self-contained and ad-hoc signed, which is what
Apple Silicon needs to run it; it is not notarised. The version comes from
`opendial/Directory.Build.props` and is stamped into the application, the bundle and this
manual's [[versions]] page.

For development without publishing:

```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH
dotnet run --project opendial/src/OpenDIAL.Desktop -c Release -p:UseOpenRawData=true
```

Linux builds the same way with `linux-x64`; the console is covered by CI on Ubuntu. Windows builds
with `win-x64`.

## The SCIEX SDK

`fetch-sciex-assemblies.sh` copies the Clearcore2 assemblies from the `alpharaw` Python package,
which redistributes them under SCIEX's WIFF Reader Distributable SDK license, into
`opendial/vendor/sciex` together with the license. They are not committed. When the folder is
present the build compiles the plugin and mirrors it into `plugins/sciex`; when it is not, `.wiff`
goes through msconvert.

## The tests

```bash
bash opendial/scripts/test.sh
```

| Suite | What it holds |
| --- | --- |
| `OpenDIAL.RawData.Tests` | the mzML reader: encodings, Numpress round trips, the semantics downstream code relies on, the vendor bridge |
| `OpenDIAL.Pipeline.Tests` | the curation store, the peak editor, the library search, the caches, the statistics (with equivalence tests between the plain and the fast cross-validation), the synthetic end-to-end run, and real-data tests that run when `OPENDIAL_TEST_ALIGNMENT` points at a real result |
| `OpenDIAL.Desktop.Tests` | the real views, headless: the review workspace, the statistics workspace, the gestures, the fonts, the probe, the manual, and the visual regression frames |
| `OpenDIAL.Interop.OpenQuant.Tests` | the component CSV and the batch import |
| `OpenDIAL.Plugins.SciexWiff.Tests` | the native reader, on a real `.wiff` when `OPENDIAL_TEST_WIFF` names one |

`test.sh` then generates a synthetic LC-MS/MS DDA study and a GC-MS one and runs the console over
each end to end. The upstream suites also pass on Apple Silicon with the open reader, apart from
five tests that hard-code Windows path separators or CRLF line endings.

## The manual's tests

`ManualTests` hold this manual to the application. Every wikilink has to name a page that exists;
every image has to be embedded; every page needs its front matter; the [[versions]] page has to
name the build's version; and every button, tab, menu item and check box in the interface has to
appear somewhere in the manual's text, so a control added without a sentence about it fails the
build. The search has to find the drift correction from the word `drift`.

## The visual regression tests

`VisualRegressionTests` render a workspace with Skia and compare the frame with a stored one
under `tests/OpenDIAL.Desktop.Tests/Baselines/`. The frames travel between machines because the
application carries both its fonts — Inter and JetBrains Mono NL — and the comparison averages
three-by-three blocks before comparing, which forgets hinting and keeps everything that moved.
At most one per cent of the blocks may differ; blanking a tenth of the width fails it.

When a frame fails, look at the rendered file the message names next to the baseline. If the
interface changed on purpose:

```bash
OPENDIAL_UPDATE_BASELINES=1 dotnet test opendial/tests/OpenDIAL.Desktop.Tests -c Release -p:UseOpenRawData=true
```

and look at what it wrote before committing it.

## The smoke test

Everything above tests the source. The smoke test drives the thing that gets installed:

```bash
opendial/scripts/smoke-ui.py --project ~/…/Project-2609071200.mdproject
opendial/scripts/smoke-ui.py --process ~/…/raw-folder --method method.txt --library lib.msp
```

It launches `/Applications/OpenDIAL.app` through LaunchServices with an isolated settings
folder, and reads what the window says it is showing from the probe file (see
[[environment-variables#Scripting the application]]) rather than guessing from pixels. The
first form opens a processed project and checks the title, the ion table, every workspace
shortcut on both modifier keys, and a click on a tab. The second writes a project from the raw
files in the folder, presses `⌘R`, waits for the run, checks that the `.wiff` files were read by
the plugin inside the bundle and that the exports and the project were written, then does what a
reviewer does through the keyboard and the mouse: types a feature id into the filter, types an
integration window and clicks **Apply to all**, clicks **Save review**, checks the tag file and
the backups on disk, exports the reviewed table and the OpenQuant list (the one step that goes
through the system's save panel is handed over by the command channel), opens the manual with
`F1` and searches it. `--keep` leaves the application running to look at; `--shots` keeps the
screenshots.

Three things about macOS make this less obvious than it looks: System Events' `click at` does
nothing on an Avalonia window, because it goes through the accessibility layer, so real mouse
events are posted with `cliclick`; keystrokes have to be sent as key codes, since a character
event never reaches a shortcut; and the window has to be frontmost before the click. `open --env`
cannot apply an environment to an instance that already runs, so the script makes sure none does.
A macOS privacy prompt — the first time the application, or the process that launched it, reads a
protected folder — stops the run until a person answers it; the script cannot and should not.
`scripts/ui-drive.sh` does the same primitives one at a time for a screenshot or a look.

Both forms end in the Statistics workspace: they read the analysis dataset from the probe
(`statistics.source`, `statistics.features`, `statistics.standards`), turn the **Volcano plot**,
**Principal components**, **Heatmap** and **Lipid enrichment** pages through the `selectStatisticsPage`
command, screenshot each, click **Build** on the heatmap and **Compute** on the enrichment and check
that both answer, and write the volcano plot as SVG and PNG through `exportChart`,
which names the page, the chart's index on it, the format, the scale and the path.

## Building this manual

```bash
python3 opendial/scripts/build-manual.py     # -> opendial/dist/OpenDIAL-manual-<version>.pdf and .html
```

The pages under `docs/manual` are embedded in the application at build time and compiled into one
document by the script, with the wikilinks turned into internal links; pandoc renders the HTML
and Google Chrome prints the PDF. The manual is updated with every version; the [[versions]] page
carries the notes.
