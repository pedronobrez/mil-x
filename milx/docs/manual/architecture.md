---
title: Architecture
section: Under the hood
order: 51
summary: How the port is put together — the upstream engine, the open raw-data layer, the pipeline library, the desktop application — and the decisions behind it.
---

# Architecture

## The pieces

| Project | What it is |
| --- | --- |
| **MS-DIAL upstream** (`MsdialWorkbench-MSDIAL-v5.5.260817/`) | the engine, kept almost untouched so a newer release can be dropped in: six files are patched, and `upstream-patches/` holds the diff |
| **MilX.RawData** | the open raw-data layer, a drop-in for the closed `RawDataHandler` package: the same public types in the same namespaces, so every upstream project compiles unchanged with one MSBuild switch (`UseOpenRawData=true`). Holds the mzML reader, the msconvert bridge, the plugin loader and the legacy-dll bridge |
| **MilX.Plugins.SciexWiff** | the native `.wiff` reader on SCIEX's Clearcore2 SDK, loaded from `plugins/sciex` |
| **MilX.Pipeline** | the headless orchestration library, adapted from MS-DIAL's console: runs the workflow with progress and cancellation, opens results, reads them back for the views, edits and saves the alignment, holds the curation store, the statistics and the survey-scan cache |
| **MilX.Interop.OpenQuant** | the OpenQuant component CSV and `.oqproj` reader |
| **MilX.Desktop** | the Avalonia application: view models over the pipeline, the custom charts, the manual |
| **tests/** | five suites; see [[building-and-testing]] |
| **tools/** | the diagnostic tools; see [[command-line]] |

## Why an open raw-data layer

MS-DIAL reads raw data through a closed NuGet package whose vendor-enabled build wraps
Windows-only SDKs; the repository ships only the "vendor unsupported" build, which reads mzML
through an opaque reader that drops the last peak of every spectrum. `MilX.RawData` exposes the
same API and reproduces everything downstream code relies on — spectrum ordering, index
conventions, unit conversions, accumulated spectra — while fixing what was wrong and adding what
was missing (Numpress, 64-bit integers, parameter groups, ion mobility). Vendor formats are
treated as mzML that has not been produced yet and go through msconvert once; SCIEX `.wiff` is
read natively because the SDK is managed code. See [[raw-data-formats]].

## Why a new desktop application

MS-DIAL's GUI is 66 000 lines of WPF over a custom drawing layer, with no macOS or Linux
implementation and no automatic path to Avalonia. MIL-X's application is new and smaller, on
Avalonia 11 with CommunityToolkit.Mvvm, and reuses OpenQuant's shell and palette so the two read
as one suite. It reuses the engine, the parameter classes, the project files and the exporters;
it does not reuse the upstream model layer.

## The invariant culture

MS-DIAL parses and formats numbers with the thread's culture in about 170 places. On a
decimal-comma locale the closed reader turned a scan time of `0.6` s into `6000` s and the
pipeline silently found zero peaks. Every MIL-X entry point — the application, the console,
the tests — forces the invariant culture first, and the raw-data layer always parses invariantly.

## The six upstream patches

| File | Change |
| --- | --- |
| `CommonStandard/MessagePack/LargeListMessagePack.cs` | reads until the requested count is in the buffer; a short `Stream.Read` from a zip entry used to reach the unsafe LZ4 decoder and crash the process when a project with a large library was opened |
| `MsdialCore/MsdialCore.csproj` | the `RawDataHandler` package references are conditional on `UseOpenRawData`, which adds the open project instead |
| `MsdialCore/DataObj/EadLipidSqliteDatabase.cs` | compiles against `Microsoft.Data.Sqlite` under the switch, because `System.Data.SQLite` has no Apple Silicon native library |
| `MsdialCoreTestApp/Program.cs` | forces the invariant culture |
| `MsdialCoreTestApp/Process/CommonProcess.cs` | files imported from a folder get the project's acquisition type; left `None`, a quarter of the MS/MS assignments of an IDA dataset were lost |
| `MsdialCoreTestApp/Parser/AnalysisFilesParser.cs` | `Path.Combine` instead of a backslash, which is a file-name character on macOS |

## Reading and writing results

The views never touch the raw engine objects directly. `ResultLoader` reads the `.pai2`, `.dcl`
and `.arf2` files into flat rows (`AlignmentSpotRow`, `AlignedSamplePeak`, `SampleInfo`) that the
ion table, the panels and the statistics share, keeping a handle on the engine's own objects so a
hand edit can be written back through MS-DIAL's serialiser. Curation lives in `CurationStore`,
which reads and writes the tag file and the sidecar.

## The probe and the command channel

For the smoke test the window can say what it is showing: with `MILX_UI_PROBE` set, a JSON
snapshot — title, workspace, run state, the selected feature, where the named controls are — is
written whenever something changes, and a `.commands` file beside it is watched for the few
actions that go through the operating system's own dialogs. Nothing of this runs in ordinary use.
See [[building-and-testing#The smoke test]].

## More

The reverse-engineering report of the upstream tree, the dependency graph, the closed component's
surface and the deliberate deviations are in `docs/ARCHITECTURE.md`; the decisions in
`docs/adr/`; the SCIEX IDA findings in `docs/SCIEX-IDA.md`; the porting status in
`docs/PORTING.md`.
