---
title: The Explorer workspace
section: Workspaces
order: 14
summary: The raw files as chromatograms and spectra — the channel tree, TIC/BPC/XIC overlays, scan-by-scan spectra, averaging, the peaks panel and manual XICs.
---

# The Explorer workspace

A qualitative look at the raw files, before or after processing: a tree of samples and their
channels on the left, a chromatogram over a spectrum in the centre, and panels on the right. `⌘1`
shows it. It reads any format the application reads — see [[raw-data-formats]] — and it is where
a raw file dropped on the application opens.

![the Explorer](images/explorer.png)

## Samples and channels

Each file in the batch is a node; expanding it loads the file (the first is loaded and its TIC
ticked when the workspace fills) and lists its **channels**: the sets of spectra that make one
chromatogram each.

- **TIC** — the whole-sample total ion current. For an acquisition with several experiments per cycle, every experiment of a cycle is summed into one point, the way OpenQuant draws it.
- **MS1** or **TOF MS 100–1500** — the survey scans.
- **MS2 events (n)** — for a data-dependent file, all the product-ion scans as one channel, since their precursor changes scan to scan.
- **MS2 400.0–425.0, CE 30** — for a data-independent file, one channel per isolation window, recognised by an isolation target and collision energy that repeat across the run.
- **IDA MS2 slot 3 → 50–1000, CE 35** — for a SCIEX IDA file, one channel per dependent experiment slot; their scan counts fall off as later slots trigger less often.

Ticking a channel draws it; the **filter** box narrows the tree by channel label (`MS2 313.2`)
and keeps ticked channels visible. **Uncheck all**, **TIC of all** and **Collapse** at the bottom
do what they say; the same three are under the View menu.

![IDA channels](images/explorer-ida.png)

## The chromatogram

Every ticked channel is drawn as one trace, plus the manual XICs of the active sample. The band
above it chooses **TIC** or **BPC** (base-peak chromatogram) for the channel traces, and the
**Active channel** — the one the spectrum pane and the XICs read from; selecting a node in the tree
sets it.

The toolbar:

| Control | What it does |
| --- | --- |
| **Select range** | a plain drag selects a retention-time range instead of zooming; `⇧`-drag always selects |
| **Overview** | fits the whole chromatogram; double-click does the same |
| **Clear range** | drops the selection |
| **Normalise** | scales every trace to 100 %; the Y axis reads relative intensity |
| **Stack** | draws each trace in its own horizontal band |
| **m/z labels** | labels the eight most intense peaks of the spectrum with their m/z |
| **RT labels** | labels the apex of each trace in view with its retention time |
| **Legend** | |
| **Smooth (σ, scans)** | a Gaussian smoothing of every trace, in scans; 0 is off |
| **Baseline (min)** | subtracts a rolling-minimum baseline over that many minutes; 0 is off |

Chart gestures, the same in every chart of the application, are in [[keyboard-shortcuts#Charts]]:
drag to zoom the X axis, wheel to zoom, right-drag to pan, double-click to fit, `⇧`-drag to
select, hover for a tooltip.

Clicking the chromatogram shows the nearest scan of the active channel in the spectrum pane and
puts a marker at its retention time.

## The spectrum

The scan the marker sits on, as a stick spectrum normalised to 100 %, with its precursor marked
when it has one. **◀** and **▶** and the scan box step through the scans of the active channel;
the count beside it is the number of scans in the channel. **Average selected range** replaces the
scan with the mean spectrum over the selected retention-time range, which is how a weak product
spectrum is read.

When a peak from the Peaks panel is selected, the pane shows its deconvoluted MS/MS instead,
mirrored against the library reference when the peak is annotated: measured up, library down.

## The panels

**Peaks** — the peaks the run found in the active sample, filterable by name or m/z: Name, RT,
m/z, Height, Score. Selecting one draws its XIC as a manual entry, shades its integration window
on the chromatogram, marks its apex, and shows its deconvoluted MS/MS. The title says how many
peaks and how many are annotated; `No results for this sample` before a run.

**Manual XIC** — extracted ion chromatograms from the MS1 channel of the active sample. Type one
or more m/z values (space-separated), a tolerance in Da or ppm, and **Add**; each entry is drawn
as a dashed trace and can be removed with its `✕` or all at once with **Clear all**.

**Spectrum peaks** — the spectrum in the pane as a table: m/z, intensity, relative intensity,
most intense first.

## What it does not do

The Explorer draws and reads; it does not edit. Re-integration, tagging and everything else that
changes a result lives in the [[analytics-workspace]].
