# OpenDIAL × OpenQuant: shared conventions and borrowed features

OpenQuant (targeted quantitation and review, PyQt6) and OpenDIAL (untargeted processing on the
MS-DIAL engine, Avalonia) are meant to feel like two workspaces of the same suite. This note
records what OpenDIAL takes from OpenQuant, what it deliberately does not, and what is worth
adding later.

## Adopted

| OpenQuant | OpenDIAL |
| --- | --- |
| Native `.wiff` reading through SCIEX's Clearcore2 assemblies on .NET 8 (`openquant/wiff.py`, `bootstrap.py`) | `OpenDIAL.Plugins.SciexWiff` — same SDK, same recipe (invariant culture, managed structured storage, `ReadOnlyShared`), linked directly since OpenDIAL is .NET. Multi-sample batches expand to one entry per sample. |
| Visual identity: one token palette (light/dark), accent `#234b8c` used sparingly, 13 px system type, eyebrow section labels, serif wordmark; flat "band of type" toolbars, underlined workspace tabs, rounded fields, muted table headers, thin scrollbars | Reproduced as an Avalonia resource dictionary and control styles in `OpenDIAL.Desktop` (`Assets/`). |
| Shell: one window, one session, four workspaces switched with Ctrl/Cmd+1–4, menu bar, status bar, `•` for unsaved | Same shell, five workspaces on Ctrl/Cmd+1–5: **Explorer**, **Analytics**, **Method**, **Samples**, **Statistics**. |
| **Explorer** — samples/channels tree, TIC/BPC/XIC overlay, scan-by-scan spectrum, averaged spectrum over a selected range, manual XIC list, spectrum peak table | Explorer workspace on top of `RawDataAccess` (any format OpenDIAL reads). |
| **Analytics review grid** — one chromatogram panel per sample for the selected component, Same Y / Link X / paging / magnify | Applied to untargeted results: one EIC panel per sample for the selected *aligned spot*, with the integration range of each sample's peak and gap-filled samples marked. This is the fastest way to judge an alignment. |
| Results table + **Statistics** (mean, SD, %CV by group) + **Metric plot** (any column vs row order, coloured by group, click selects) | Same three tabs under the grid, grouped by sample class. |
| **Samples** workspace: type (Sample/Blank/QC/Standard), group, dilution, comment, acquisition metadata | Samples workspace; the columns map onto MS-DIAL's `AnalysisFileBean` (type, class, order, batch, dilution). |
| **Method** workspace as the single place the processing method lives; New-project wizard that writes the project file first | Method workspace (form + method-file text) and the same wizard flow. |
| Project file that survives closing (`.oqproj`) | OpenDIAL project JSON beside MS-DIAL's `.mdproject`, so both the Windows GUI and OpenDIAL can reopen it. |

## Deliberately not adopted (different problem)

* Calibration curves, internal-standard ratios, ion-ratio qualifiers, acceptance criteria: these
  define *targeted* quantitation. In the suite the workflow is "discover in OpenDIAL, quantify in
  OpenQuant"; OpenDIAL's exports (alignment table with m/z, RT, MS/MS) are the natural input for
  an OpenQuant component table.
* LIPID MAPS local index and the formula finder: MS-DIAL brings its own lipid annotation
  (LBM/EIEIO) and formula/structure elucidation through MS-FINDER; duplicating them would create
  two answers.

## Worth adding next (cheap, high value)

1. **Export to OpenQuant**: write an OpenQuant component CSV (`name, precursor, fragment, rt,
   window, tolerance, unit`) from selected alignment spots, so a discovery list becomes a
   targeted method in one click.
2. **Import an OpenQuant batch** (`.oqproj` sample table) as the Samples workspace, keeping
   sample types and groups.
3. **Mass calc / adduct panel** from OpenQuant's `chemistry.py` as an Explorer side panel (small,
   self-contained arithmetic).
4. **Noise-region S/N** convention from OpenQuant in the review grid (right-click a baseline
   stretch), reusing MS-DIAL's peak-shape metrics.
