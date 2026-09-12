---
title: Troubleshooting
section: Help
order: 60
summary: Symptoms, causes and fixes — the application will not open, a file will not read, a run finds nothing, a shortcut does nothing, a result cannot be edited, a model looks too good.
---

# Troubleshooting

Each entry is a symptom, what is behind it, and what to do. The log (`⌘L`) is the first place to
look for anything a run did; the status bar for anything the window did.

## Opening the application

**macOS says the application is damaged, or cannot be opened.** The bundle is ad-hoc signed and
not notarised, and a copy that arrived by download or AirDrop is quarantined. Right-click ▸
**Open** ▸ **Open** once. A bundle built on the same machine has no quarantine flag and opens
straight away. If a copy was installed by dragging in Finder rather than `ditto`, the signature
may have been lost; reinstall with `ditto`.

**Finder opens projects in the wrong copy of the application.** Two bundles claim the same
document types. Keep one in `/Applications`; the packaging script unregisters the `dist/` copy
while an installed one exists.

**A privacy prompt appears and the application seems frozen.** macOS asks once, per application,
before it reads a protected folder (Documents, Desktop, other applications' data). A file read
blocks until the prompt is answered. Answer it. When the application was launched from a script,
the prompt names the process that launched it.

## Reading files

**`.wiff` shows `wiff · msconvert` instead of `wiff · native`.** The SCIEX plugin is not in the
bundle (`Contents/MacOS/plugins/sciex`): the build was made without `vendor/sciex`. Run
`fetch-sciex-assemblies.sh` and rebuild, or let msconvert convert the file.

**`No spectra could be read from …`, or `Could not read file`.** For a `.wiff`, the `.wiff.scan`
has to sit beside it. For a vendor file with no native reader, msconvert is needed: see the next
entry. For an mzML, the file may be truncated; the log shows how far the reader got.

**`Cannot read … on this platform: vendor formats need ProteoWizard msconvert`.** No converter was
found. Install Docker Desktop and pull the ProteoWizard image, or point [[settings]] at a native
msconvert, or convert the file elsewhere and put the `.mzML` beside the source. See
[[raw-data-formats#The msconvert bridge]].

**Conversion takes minutes per file.** Under Docker on Apple Silicon the image runs under x86-64
emulation; that is the cost. It happens once per file.

**The log says `rosetta error: invalid gdt selector index` and msconvert exits with code 1.** The
container is running under Rosetta, which cannot run the image's Wine. Give Docker a QEMU
virtual machine instead: with colima, `colima delete` and `colima start --arch x86_64 --vm-type
qemu`; in Docker Desktop, turn off "Use Rosetta for x86_64/amd64 emulation". See
[[raw-data-formats#The msconvert bridge]].

**colima refuses to start with `guest agent binary could not be found for Linux-x86_64`.** The
x86-64 VM needs lima's extra guest agents: `brew install lima-additional-guestagents`.

**`Raw data file not found (was the project folder moved?)`.** The project records the raw files'
paths; relative when they are nearby, absolute otherwise. Move the raw files with the project, or
add them again in the Samples workspace.

**`.abf` will not open.** It cannot on macOS or Linux: the Reifycs reader is a Windows-only native
library. Convert to mzML.

## Processing

**The run finds zero peaks, or absurd retention times, on a machine set to a decimal comma.**
This is the defect the port fixes: MS-DIAL parses numbers with the machine's locale. The desktop
application and the `opendial-cli` launcher force the invariant culture, so it cannot happen
there; running `MSDIALCUI` directly can. Use the launcher, or set
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.

**`Library not found`.** The method names an MSP file that is not at that path. Fix it in the
Method workspace; the last library used is offered in the file dialog.

**Every feature is Unknown.** No library was given, or the library's polarity does not match the
run, or the MS1 tolerance is too tight for the instrument. The log says how many records loaded;
zero means the file was empty or unreadable.

**Far fewer peaks than MS-DIAL on Windows found, on `.wiff` data.** Check that the plugin is
centroiding with SCIEX's peak finder (`OPENDIAL_WIFF_CENTROID` unset or `sciex`). The
local-maximum method reports heights about a quarter lower, and the minimum-height cut-off then
drops peaks the Windows run kept. See [[raw-data-formats#SCIEX .wiff]].

**Fewer MS/MS assignments than the Windows project.** Check the acquisition type in the Samples
workspace. MS-DIAL 5.5 marked the IDA files of the validation set as SWATH, which assigns spectra
by isolation window and is looser than DDA. See [[processing#Acquisition types]].

**Different names from the Windows project for the same peaks.** Almost always the library, not the
processing: a different build of the library has records at different masses. Point both at the
same file. `ResultCompare --check-library` says whether a library could have produced a run's
annotations. See [[annotation#Why two runs can disagree]].

**`Run failed: …` with `AccessViolationException` while opening a project.** Fixed in this
version's upstream patch to `LargeListMessagePack`; a build made before it crashed on projects
with a large library. Rebuild.

**Out of memory on a very large study.** Alignment runs in memory, as upstream. Reduce the batch,
or set `Alignment light mode: True` in the method text.

## The window

**`⌘5` (or any workspace shortcut) does nothing.** Fixed in this version; in earlier builds the
shortcuts were written in a form Avalonia parsed to the wrong key. If it recurs, the
`GestureTests` say which binding is wrong.

**A review key (`⌘⇧1`, `⌘⇧C`, …) does nothing.** They act only while the review is on screen:
the Analytics workspace, or the ion table's own window. Earlier builds bound the tags to `Ctrl`
with a bare digit, which Avalonia read as another key, and the verdicts to `Alt`, which fired
only while the focus was inside the review; both are gone.

**The filter band covers the counts on a narrow window.** Fixed: the band scrolls sideways. Widen
the window if it is narrower than 1100 points.

**The ion table is missing.** It was torn off into its own window, which may be behind the main
one or on another screen. **Dock table** brings it back; `IonTableDetached` in `settings.json`
remembers the state.

**A chart is zoomed into nothing.** Double-click it, or **Overview** in the Explorer.

**The manual's search finds nothing.** Every word has to match; try fewer words. The search is
over the pages' text and titles, not the interface's labels.

**The manual opens in the other language.** The **Português** / **English** button at the top of
the Help window switches it, and the choice is kept in `settings.json` for next time.

## Reviewing

**Re-integration or Split isomer says the result cannot be edited.** The result was opened from an
`.mdalign` export or a folder without its `.arf2` files; there is no container to write. Open
the `.mdproject` or the folder that has the binary files.

**`Set a retention window first`.** The from and to boxes are empty or reversed. `⇧`-drag a panel
or type two increasing times.

**Save review says the review could not be saved.** The results folder is read-only, or a file is
locked by another process (MS-DIAL on a shared drive). The tags stay in the session until it can
write.

**An edit needs undoing.** Rename the `.before-curation` files back over the originals; they hold
the alignment as the run produced it. See [[projects-and-files#Backups]].

**Tags made in MS-DIAL do not show.** They are read from `<alignment>_tags.xml` beside the
alignment file; MS-DIAL writes it when its own review is saved. The file must be beside the
`.arf2` OpenDIAL opened.

## Statistics

**The discriminant model separates the classes perfectly and the verdict still says it does not
survive cross-validation.** That is the verdict working. With few injections and thousands of
features a fit separates anything; only Q² and the permutation p say whether it would hold on new
data. Read the plot as a picture of this batch.

**The orthogonal model refuses: `separates two classes, and this batch has 4`.** OPLS-DA is
two-class by construction. Use the discriminant model, or set the classes so only two remain (the
others' injections still take part in the principal components).

**The drift correction corrects nothing.** Fewer than three injections are typed `QC` in the
Samples workspace, or their heights are zero. The message says how many it found. Order and batch
matter too: an injection order of 0 everywhere is read as the file order.

**The network is empty.** No feature in the filter carries a product spectrum, or the cut-off is
too high. Lower it, or untick **Annotated only**.

## Getting more detail

`OPENDIAL_TRACE=1` prints Avalonia's binding warnings to the console. The log holds the engine's
own messages. `RawDump` and `WiffProbe` (see [[command-line]]) show what the raw-data layer reads
from a file, independently of the application.
