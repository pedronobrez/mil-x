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
| **Contour** | replaces the chromatogram with the active channel as a surface — see [[explorer-workspace#The contour]] |
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

## The contour

**Contour** on the toolbar, or **View ▸ Explorer: contour view**, draws the active channel as a
surface: retention time across, m/z up, intensity as colour. A chromatogram sums the mass axis
away and a spectrum sums the time axis away; the contour is the same data with neither summed, so
an interference sitting beside a target, or a ridge running down the whole run at one mass, is
visible rather than inferred.

Each scan of the channel is a row and each m/z bin a column. The mass axis is divided into about
1,400 bins — roughly one per screen pixel across a wide range. A long run has more scans than a
screen has rows, and the scans over the limit of about 900 rows are **averaged into their row, not
skipped**: sampling every k-th scan loses a one-scan peak entirely, which is the opposite of what a
picture of the data is for. The average rather than the sum, because the last group is usually
short and a row that is dimmer only for that would be a lie about the data. The band above the
surface says how many rows and bins were built, from how many scans, and how long it took.

| Control | Effect |
| --- | --- |
| **Intensity** | how intensities become colour: *sqrt* (default), *log* or *linear*. A linear ramp over four decades shows the base peak and nothing else; square root lifts the minor ions into view without the flattening that makes a log plot look uniformly grey |
| **Colours** | the palette: inferno, viridis, magma, turbo, greys |
| **m/z … to** | the mass range of the next build; 0 and 0 is the channel's own range |
| **Rebuild** | rebuild the grid over the time range on screen and the m/z range typed, at full resolution for that range |
| **Extract this view** | the chromatogram and the spectrum of the rectangle on screen — both read from the reader, not from the grid: the XIC of the m/z band joins the Manual XIC list and the average spectrum of the time range fills the spectrum pane, and the view returns to the chromatogram to show them |
| click | the spectrum of the scan under the cursor, in the spectrum pane |
| hover | the retention time, m/z and intensity under the cursor |

The top of the colour scale is the 99.7th percentile of the non-zero cells, not the maximum: a
single saturated or spiking scan would otherwise set the scale for everything else and leave the
rest of the surface black. The colour bar names the law and the top. Drag to zoom the time axis,
wheel to zoom, right-drag to pan, double-click to fit, as on every chart; the picture leaves as SVG
or PNG through the chart's own right-click menu, see [[chart-export]].

Nothing is quantified from the contour: its m/z bins are wider than the instrument's steps and its
rows may be several scans averaged, so it is a picture, deliberately. *Extract this view* goes back
to the reader for both the XIC and the spectrum.

## The spectrum

The scan the marker sits on, as a stick spectrum normalised to 100 %, with its precursor marked
when it has one. For a product-ion scan the band above the pane names the precursor the scan
itself isolated, its isolation window, the collision energy and, in a SCIEX IDA file, the slot it
came from — per scan, never the slot's placeholder mass. **◀** and **▶** and the scan box step through the scans of the active channel;
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

**Manual XIC** — extracted ion chromatograms of the active sample. Type one or more m/z values,
separated by commas or spaces (`183.1391, 313.2384`), a tolerance in Da or ppm, and **Add**; each
entry is drawn as a dashed trace and can be removed with its `✕` or all at once with **Clear all**.
The **Source** chooses which scans the window is summed over:

| Source | Scans |
| --- | --- |
| **MS1** | the survey channel of the active sample (the default) |
| **Active channel** | the channel the spectrum pane reads from, whatever its kind |
| **Every checked channel** | one trace per ticked channel, so the same mass is compared across slots or windows |
| **All MS2** | every product-ion scan of the file, from every channel, merged by retention time |
| **Gated by precursor** | only the MS2 scans whose own precursor is within **± Da** of the **Precursor m/z** typed beside it, from any IDA slot or DDA event, merged by retention time |

The gated source is what a product ion's chromatogram needs on a data-dependent file: the
instrument puts one precursor's spectra into whichever slot was free that cycle, and the slot's
own label carries only the placeholder mass the method was written with. The gate reads the
precursor each scan actually isolated — for SCIEX IDA that is the per-scan `ParentMZ` — so a
precursor spread over twelve slots becomes one trace. A line under the form says how many scans
the gate found and how many slots they came from. The same source is `gated:MZ[:TOL]` on the
[[command-line]].

**Mass calc** — a formula into the numbers a mass spectrometrist needs, and a measured mass into
an error; the tables and the arithmetic are MIL-Q's, so the two applications give the same
numbers. Type a molecular formula: `C18H34O4`. Groups with multipliers, `C6H4(NO2)2`, and single
isotopes in square brackets, `[13C]6H12O6`, are read, with `D` accepted for deuterium, so a
labelled internal standard such as `C18H30D4O4` is typed as it is written. The panel reports the
formula in Hill order, the monoisotopic mass, the average mass, the ring-and-double-bond equivalent
(RDBE) and the m/z of the ion for the adduct chosen beside the formula:

| Negative | Positive |
| --- | --- |
| [M−H]⁻ | [M+H]⁺ |
| [M+Cl]⁻ | [M+NH₄]⁺ |
| [M+HCOO]⁻ | [M+Na]⁺ |
| [M+CH₃COO]⁻ | [M+K]⁺ |
| [M−2H]²⁻ | [M+2H]²⁺ |

The ion mass accounts for the electron: a cation is lighter than the sum of its atoms by one
electron mass and an anion heavier, which matters at the fourth decimal. Type a measured m/z
under the computed one — or press **Measured from spectrum** to take the peak of the spectrum
pane nearest the theoretical ion — and the error is shown in mDa and in ppm. The theoretical
isotope pattern of the ion — m/z and relative abundance of M, M+1, M+2 and so on, down to 0.1 % —
is tabulated, and **Overlay on spectrum** draws it on the spectrum pane as dashed sticks scaled to
the measured peak nearest the monoisotopic mass, so the measured satellites can be compared with
the expected ones; **Clear overlay** removes it, and so does showing another spectrum. The overlay
is only meaningful on a spectrum that carries the satellites: a product-ion scan isolates the
monoisotopic precursor, so overlay on the survey channel.

**Spectrum peaks** — the spectrum in the pane as a table: m/z, intensity, relative intensity,
most intense first.

## What it does not do

The Explorer draws and reads; it does not edit. Re-integration, tagging and everything else that
changes a result lives in the [[analytics-workspace]].
