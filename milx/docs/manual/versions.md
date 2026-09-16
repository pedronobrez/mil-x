---
title: Versions
section: Help
order: 61
summary: What each version brought, from OpenDIAL 0.1 to MIL-X 1.0; the notes are updated with every release.
---

# Versions

The version shown in **Help ▸ About MIL-X** and in the window's probe is the one stamped in
`milx/Directory.Build.props`. This page names it, and a test fails the build when it does not.

## 1.0.0 — September 2026

The current version. It tracks MS-DIAL 5.5.260817.

**The name.** OpenDIAL is now **MIL-X**: *Multi-omics Identification Laboratory*, and X for
exploration. Its targeted counterpart, today's OpenQuant, becomes MIL-Q at its own 1.0; the two
are one family — *explore, then quantify* — which **Export to OpenQuant…** already does. The mark
is a cow's head, face on: MIL-X is said like *milks*, and MS-DIAL's icon is a rotary telephone, so
the pun is a homage to the parent project. See [[about#The name]].

**What the rename changes for you**

| Until 0.9 | From 1.0 | What happens to the old one |
| --- | --- | --- |
| `OpenDIAL.app`, `OpenDIAL.exe`, `OpenDIAL` | `MIL-X.app`, `MIL-X.exe`, `MIL-X` | on Windows the installer replaces OpenDIAL 0.9 (same upgrade code); on macOS delete `OpenDIAL.app` yourself |
| `.odproj` | `.milx` | still opens, from Finder, Explorer and **Open project…**; the next **Save** writes `.milx` beside it |
| `opendial_method.txt` in a results folder | `milx_method.txt` | still read |
| `~/.config/OpenDIAL`, `%APPDATA%\OpenDIAL` | `~/.config/MIL-X`, `%APPDATA%\MIL-X` | read once, on the first start, while the new folder does not exist |
| `~/Library/Caches/OpenDIAL` | `~/Library/Caches/MIL-X` | a cache; delete the old one |
| `OPENDIAL_*` variables | `MILX_*` | still honoured in 1.0 — see [[environment-variables]] |
| `%LOCALAPPDATA%\OpenDIAL` | `%LOCALAPPDATA%\MIL-X` | the installer moves the install |
| `org.opendial.desktop` | `io.github.pedronobrez.milx` | macOS asks for access to your folders once more |
| `opendial/` in the repository | `milx/` | every script path in this manual changed with it |

A results folder did not change: the MS-DIAL files, `_tags.xml`, the curation and polarity files
are exactly what 0.9 wrote, and 0.9 still opens them.

**New since 0.9.0**

- **Native `.wiff` out of the box, on every platform.** The releases carry the SCIEX components
  their licence's Appendix A names as redistributable, with that licence beside them in
  `plugins/sciex`. Nothing has to be fetched or installed. See [[raw-data-formats]].
- **A project can be moved.** When the `.mdproject` a project points at is gone, the results are
  looked for beside the project, one folder up and in the sibling folders, on any platform's
  separators. See [[projects-and-files#The MIL-X project (.milx)]].
- **The 95 % region on the score plots, in both definitions.** The default is the χ² region
  MetaboAnalyst draws; **F region** is its other one, wider for a small class. See
  [[statistics-workspace#Principal components]].
- **Two more builds**: Linux on arm64, and macOS on Intel.
- **A batch through the Windows build, end to end**, on a real machine, through the installer: the
  training demo, four injections, native `.wiff`. Starting and working are now the same claim.

**What 1.0 still is not**

- **Signed.** Every platform still greets a new user with a warning — "Windows protected your PC",
  "unidentified developer". A Windows code-signing certificate and an Apple notarisation are the
  fix, and neither is a change to the program.
- **Verified on a real negative-mode batch.** The polarity merge is checked against synthetic pairs
  and against a real alignment mirrored on itself; it has not yet seen a batch acquired in
  negative mode. [[polarity-merge]] says so too.
- **Photographed under its own name.** Some screenshots in this manual were taken on 0.9 and show
  *OpenDIAL* in the title bar. The interface under it is the current one.

After 1.0: a library manager, the SIRIUS hand-off, a molecular network on the modified cosine,
calibration curves, and a processing queue.

## 0.9.0 — September 2026

Tracks MS-DIAL 5.5.260817. The last version named OpenDIAL.

**The first version you can download.** Until now the application was something you built. Every
release from here carries a build for macOS, Windows and Linux —
[github.com/pedronobrez/mil-x/releases](https://github.com/pedronobrez/mil-x/releases) — with a
Windows installer that registers `.odproj`. The 0.9.0 downloads were rebuilt after the release to
carry SCIEX's redistributable components, so `.wiff` reads natively from them too. See
[[getting-started]].

**New**

- **One project for a batch acquired both ways.** The Samples workspace has a **Polarity** column,
  guessed from each file's name and editable. When a batch holds both, **Process batch** runs twice
  — positive into `positive/`, negative into `negative/`, each with its own ion mode — and pairs
  the two results by itself. A batch that is all one polarity runs exactly as it did.
- **The statistics see compounds instead of ions.** With a polarity linked, every model is computed
  on the two runs reconciled: a compound both saw appears once, with the numbers of the run that
  measured it better, on the injections both share. A matrix where half the rows are copies of the
  other half fits every model on a lie. See [[polarity-merge]].

## 0.8.0 — September 2026

Tracks MS-DIAL 5.5.260817.

**New**

- **Both polarities of a compound, side by side.** A new evidence tab, **Other polarity**, mirrors
  this run's product spectrum against the other run's for the same compound, with the partner's
  name, m/z, adduct, retention time, signal-to-noise and the correlation between the two height
  profiles beneath it. With **Split evidence** on, MS/MS on the left and Other polarity on the
  right is the layout linking a polarity is for — and the second column now selects it on its own.
- **One compound, one verdict.** **Tag both polarities**, on by default once a polarity is linked,
  carries the review across the pair: confirming a compound here confirms it in the other run, and
  undo takes it back in both. Both reviews are written by **Save review**, each to its own
  `_tags.xml`. See [[polarity-merge]].

## 0.7.0 — September 2026

Tracks MS-DIAL 5.5.260817.

**New**

- **The two polarities of one batch, reconciled.** **Link polarity…** in the review toolbar pairs
  the result on screen with the same batch run the other way. Compounds are matched on the neutral
  molecule — the same molecule is `[M+H]+` here and `[M-H]-` there — within 0.01 Da, within 0.1
  minute, and only when their height profiles across the injections agree, which is a strong signal
  because the two runs are the same samples. The ion table gains **Neutral**, **Pol**, **Other
  polarity** and **Quantify**, and the filter band a polarity box. Heights are never added across
  the polarities: the side that measured a compound better quantifies it and the other confirms it.
  The pairing is written beside the alignment as `_polarity-pairs.json` and a linked result opens
  linked. See [[polarity-merge]].
- **The neutral mass in the ion table**, filled in from the adduct whether or not a polarity is
  linked. The adduct table it comes from knows the doubly charged ions and the dimers, not only the
  singly charged ones.

## 0.6.0 — September 2026

Tracks MS-DIAL 5.5.260817.

**New**

- **The library is read rather than assumed.** The Method workspace says what the chosen `.msp` holds — how many records, which adducts, what range of mass and of time — and warns when its retention times belong to another gradient. **Ignore its retention times** leaves them out of the score; **Calibrate to this run…** fits them to the times this run measured for what it named, writes a calibrated copy beside the original and points the method at it. See [[annotation#Why so few features are named]].
- **Retention time is out of the annotation score by default.** Almost every public library carries times from the gradient it was built on, and scoring against them throws correct spectral matches away: on the liver batch it was the difference between 286 names and 1198. Turn it back on when the library was measured on the method being run.
- **One compound, not several ions.** The adducts, isotopes, in-source fragments and dimers of a compound are gathered behind the ion the run measured best: they elute together, their heights rise and fall together across the injections, and the distance between their masses is one an adduct pair gives. The ion table shows what each row is in the **Ion of** column, and **One row per compound** in the filter band hides the rest.
- **Blank %** in the ion table and in the filters: the mean height in the injections typed Blank against the mean in the samples. It is the first cut of any untargeted run, and the answer to a red heatmap cell in a solvent.
- **Undo for the review** (`⌘Z`, `⌘⇧Z`, and the Edit menu). A whole **Confirm all shown** over a filter of two thousand features goes back in one step, and the status line says what was taken back.
- **Run report…** writes the run down: the counts, the library, the injections, the method, the log and the figures on screen, as one HTML file with the figures inlined as vectors. It opens in a browser and prints to PDF.

**Changed**

- The mirror keeps the width when the evidence area is split: below a pane width where both fit, the match scores step aside.

**Fixed**

- A run of eight files reported that it had read none of them natively, while its own log showed the SCIEX reader on every one. The count was scanned out of the run log, which keeps only its last few thousand lines, and eight minutes of alignment progress pushed the beginning of the run out of it. The run counts as the lines arrive now.

## 0.5.0 — September 2026

It tracks MS-DIAL 5.5.260817.

**New**

- The **Two factors** page of the Statistics workspace: the two-way analysis of variance per feature with the interaction (type-II sums of squares, so unbalanced designs and empty cells are handled), and ASCA over the whole matrix with a permutation test per effect and a scores plot per effect. The second factor is a **Factor** column in the Samples workspace, saved with the project. See [[two-factor-analysis]].
- The smoke test lands on a feature whose spectrum is mirrored against the library, checks the panel says so, and writes that mirror out as a figure; the probe's `selectFeature` takes `where: "mirrored"` and waits for the spectrum to load, which it was not doing before. A project with no MS/MS makes the step step aside instead of failing.
- Six things the first full review pass asked for. **Next** follows the order the ion table is sorted in, so a pass down a column of S/N confirms down the screen instead of jumping by feature id. **Reject all shown** sits beside Confirm all shown. The filter band gains **S/N ≥**. **Split evidence** puts two sets of evidence tabs side by side — the mirror beside the class statistics — each on its own tab. The charts answer a trackpad: two fingers sideways walk along the axis, `⌘` with two fingers stretches the intensity axis, and a double click puts both back. And the two graphs, the molecular network and the pathway map, are no longer pictures: the wheel zooms about the pointer, the background drags, a network node can be pulled somewhere clearer, and a double click lays them flat again.
- A figure leaves the application looking the way the figure wants rather than the way the window does. **Export…**, and **Export as a picture…** in the right-click menu every chart now has, open one dialog: format, resolution, **theme** — light, dark, or as on screen, opening on light so a dark session still gives a light figure — background (the theme's paper, white, or nothing at all), and the size of the type. The preview is the export itself, and the choices are remembered. See [[chart-export]].
- Every chart in Explorer and Analytics can be exported, not only those of the Statistics workspace: the chromatogram, the spectrum, the peak panels, the mirror, the isotope envelope, the feature map and the abundance bars.
- The manual in Portuguese, beside the English one: **Português** in the Help window, and a second PDF.

**Changed**

- The Analytics workspace opens on what confirming an analyte needs: the ion table, the peak in every sample, and the product spectrum, all on screen at once. The peak grid is a panel of its own above the evidence tabs instead of the first tab, and MS/MS is the tab showing by default; a divider between the two sets their heights. The grid's size is one **Grid** `3 × 2` control.
- The ion table's window docks back by being dragged over the left part of the main window: the place it will take lights up as the title bar crosses it, and letting go there docks it. While the table is away, the peaks and the spectrum take the whole width; its column no longer stands empty.

- The review keys are `⌘⇧` (`Ctrl+Shift`) with a digit for a tag, with C and X for the verdicts, with N for the next unreviewed feature and with the arrows for the next and previous one. They are bound on the windows, so they work whatever has the focus, in the main window while Analytics shows and in the ion table's own window.

**Fixed**

- The molecular network tested a click against the layout's own coordinates rather than the panel's, so on any panel that was not exactly the size of the layout a node could not be selected at all.
- The mirror's *measured* and *reference* labels chose their corner by peak height, which is not what collides with them: the m/z written above a peak is several times wider than the peak. Each label now goes where its own half of the plot has nothing drawn.
- A heatmap cell in a blank could read as red without saying why. A standardised row only compares an injection with the rest of its own row, so a feature that is noise everywhere still has a reddest cell; hovering a cell now also gives the number the colour was standardised from.
- A PNG of a chart with a title or an axis label drew both at the wrong size above 1×: a title twice as tall as it should be, the axis label off the edge, while everything around them scaled properly. The picture is now drawn by the same code as the screen and the SVG, at every resolution.
- A session that ended with the ion table in its own window crashed on the next start: the window was asked for before the main one was on screen. It now opens once the main window has.
- The tag keys never fired: they were written as `Ctrl` with a bare digit, which Avalonia reads as another key; and `Alt+C` fired only while the focus sat inside the review.
- The spectrum mirror's *measured* and *reference* labels drew over the m/z of a base peak at the right edge; each now takes the emptier side of its half.
- After a save, the Analytics toolbar's message drew over its own buttons; it now takes the space the buttons leave and trims, with the whole sentence in its tooltip.
- The msconvert bridge is verified end to end on Apple Silicon: ProteoWizard's `small.RAW` converts through colima's x86-64 QEMU machine, from the command line and from the installed application, in about 30 seconds. Rosetta cannot run the image's Wine, and the manual and the converter's message now say so and give the colima route.
- The smoke test clicked at the wrong place whenever the pointer was resting on something with a tooltip: a tooltip is a window of its own, and the system lists it before the real one, so the frame the click was worked out from was the tooltip's. The frame is now asked for by the window's name, and every click says on screen where it went.
- The smoke test failed now and then on a shortcut that had in fact never been delivered: a key goes to whatever is frontmost when it is sent, and the front can be taken in the moment between asking for it and the key going down. It now checks the window got the front, presses again when nothing happened, names the application that was holding the front when it gives up, and waits out the seconds in which the system refuses to launch the bundle it has just put away.
- The manual's code spans, search highlights, table headers and rules took their colours from the light theme whatever the window's theme, so a dark window drew its code white on white. Every brush on a page now follows the window it is in, and changes with the theme.

## 0.4.2 — September 2026

It tracks MS-DIAL 5.5.260817.

**New**

- SCIEX `.wiff2` acquisitions are read natively, through the `.wiff` SCIEX OS writes beside every one of them: the same spectra and sample names, verified on the ZenoTOF batch. A `.wiff2` on its own still goes through msconvert, and says so.
- One injection per acquisition: adding a folder with a `.wiff`/`.wiff2` pair takes the `.wiff`, and a `.wiff2` picked beside its `.wiff` is swapped for it with a note, so nothing is processed twice.
- A raw-file plugin that claims a file and cannot open it hands the file to the msconvert bridge instead of failing the run.

## 0.4.1 — September 2026

It tracks MS-DIAL 5.5.260817.

**Changed**

- The pathway table is now BioPAN's own, transcribed from the tool's archived database: 97 reactions (51 between classes, 13 over the ether lipids, 3 over the sphinganine bases, 30 between fatty acids) with its 249 reaction–gene links; the twelve steps MIL-X adds are marked and switched on by **Beyond BioPAN**, off by default. The ether classes split into `O-` and `P-`, the ceramides and sphingomyelins into their sphinganine forms, as BioPAN has them.
- The reaction weights are compared as BioPAN compares them — the ratios themselves, not their logs — and the thresholds are BioPAN's, one-sided: 1.282, 1.645 (default), 2.054, 2.326.
- Every gene in the table is the human symbol; BioPAN's mouse `Scd1` and `Scd3` are `SCD` and `SCD5`.
- **Paired** on the Pathways page: BioPAN's paired comparison, the injections of the two classes taken one to one in order.
- A new mark for MIL-X: the same rounded square, gradient and white peak, with the whole chromatogram behind the peak instead of one co-eluting neighbour — the untargeted run, everything it contains. The two-peak mark, the deconvolution of one analyte, goes to OpenQuant. `tools/make_icon.py --export` writes either as PNGs, an `.iconset`, an `.icns`, an `.ico` and an SVG.
- A chart's legend goes to the corner with the least under it, so the volcano plot's named corner stays readable; the heatmap's value name sits under the colour bar instead of across its numbers.
- The species level follows BioPAN's rule for the chain reactions: a step that releases a chain is drawn only when that fatty acid is measured, one that adds a chain only when its acyl-CoA is. The fatty-acid level is the free fatty acids measured, as BioPAN's is, not the chains of the other lipids.
- The design notes say where the two agree (the pathway score is the same number) and where they do not (the species-level chain rule, the extensions).

## 0.4.0 — September 2026

It tracks MS-DIAL 5.5.260817.

**New**

- The **Pathways** page of the Statistics workspace: LIPID MAPS' BioPAN on the reviewed analytes — the mammalian lipid reaction network (sixty-odd reactions over some thirty classes, with their genes) weighted by product over reactant in every injection, compared between the two classes of the comparison, scored as a Z, and chained into pathways; at the class, molecular-species and fatty-acid levels; drawn as a network with the reactions' injections beside it, exported as tables and as SVG or PNG. See [[pathways]] and [[biopan-plan]].
- The smoke test builds the heatmap, computes the enrichment and scores the pathways by clicking their buttons, and the probe reports what each answered.

**Fixed**

- The probe did not rewrite its state when the one-factor analysis changed, so a heatmap could be on screen while the probe said it was not built.
- The smoke test filtered the ion table by a feature's id, which for feature 0 matched half the table; it filters by the m/z as printed.

## 0.3.0 — September 2026

It tracks MS-DIAL 5.5.260817.

**New**

- The Statistics workspace rebuilt around MetaboAnalyst's one-factor module, with its pages down the left: **Data processing** (missing values, the variance and QC-RSD filters, sample normalisation, transformation, scaling), the **Normalisation check**, **Fold change**, the **Statistical test** (Welch, Student, paired, Mann–Whitney, Wilcoxon; FDR, Holm, Bonferroni), the **Volcano plot**, **ANOVA** and Kruskal–Wallis with Fisher's LSD post-hoc, **Correlations**, **Pattern search**, the **Random forest**, the clustered **Heatmap**, **K-means**, and the **Lipid enrichment** (over-representation over classes, chain lengths and unsaturation, the class changes and the chain map). See [[one-factor-analysis]].
- The **relative abundance** source: confirmed analytes as area ratios to the internal standard of their class, chosen per class in the **Internal standards…** dialog, suggested from labelled and odd-chain names, and kept in the curation sidecar. See [[internal-standards]].
- Every chart in a frame with options — title, labels, point size, font scale, palette, colour scale, grid, legend, labels, ellipses, trees, values — and **Export…** as SVG (real lines and text) or PNG at 2×, 4× and 6×. See [[chart-export]].
- The principal components page gained the scree plot, the 95 % confidence ellipses, the choice of components and the named top loadings; the discriminant page a VIP chart with the class means beside each bar; the dendrogram its distance and linkage choices.
- **Reload from the review** when the review changes after the analysis was loaded; **Drift corrected** now feeds the corrected values in before the preprocessing, so the ratios and the tests read them too.
- The engine: `Distributions` (gamma, beta, normal, Student, Fisher, chi-square, hypergeometric), `Preprocessing`, `Univariate`, `Correlations`, `Clustering` (four distances, four linkages, k-means++), `RandomForest`, `Enrichment`, `LipidNames`, `RelativeAbundance` and `AnalysisTable`, each with tests against hand-computed values and against the reviewed liver batch.
- The plan for a BioPAN-style pathway module, [[biopan-plan]].

**Changed**

- The options band of the Statistics workspace is now the **Source** band; the transform and the scaling moved to the **Data processing** page, where the rest of the preprocessing is.
- The manual gained four pages and eleven figures; the visual regression tests cover the new pages and the standards dialog.

## 0.2.0 — September 2026

It tracks MS-DIAL 5.5.260817.

**New**

- This manual: in the application under **Help**, with search and cross-references, and as a PDF built from the same pages.
- The **Orthogonal** tab of the Statistics workspace: OPLS-DA with the S-plot, held to the same cross-validation and permutation test as the discriminant model.
- The **Drift correction** tab: QC-RLSC against the quality controls, batch by batch, with a levelling fallback for batches with fewer than three controls, and a **Drift corrected** switch that feeds the corrected values to every other view.
- The **Discriminant** tab: PLS-DA with VIP, leave-one-out Q² and a permutation test, with cross-validation done in the space the injections span so two thousand features permute in under half a second.
- The survey-scan cache, which makes reopening a project immediate, and a product-spectrum cache beside it.
- The ion table can be torn off into its own window, its columns reordered, and both remembered.
- The smoke test that drives the installed bundle, in two forms: opening a project, and processing a batch through re-integration, saving and export.
- The **abundance** column of the ion table, the library re-search from the Candidates tab, and the **Molecular ion** and **Hand-edited** filters.

**Fixed**

- The five workspace shortcuts, which had never fired: `Cmd+5` parsed to the wrong key.
- The review filter band drawing over the counts on a laptop screen.
- The cross-validation of the discriminant model, which let the held-out injection influence its own prediction and gave noise a respectable Q².
- The drift correction skipping batches with two controls.
- Opening a project with a large library crashing the process (an upstream `LargeListMessagePack` defect).
- Visual regression baselines describing the machine that made them rather than the application: both fonts are now carried in the bundle.

**Changed**

- The version is stamped once, in `Directory.Build.props`, and shown in About.
- The settings folder can be moved with `MILX_SETTINGS_DIR`.

## 0.1.0 — September 2026

The first working port: the open raw-data layer with the mzML reader and the msconvert bridge,
the native SCIEX `.wiff` reader, the console built for macOS, the desktop application with the
Explorer, Analytics, Method and Samples workspaces, the review loop with MS-DIAL's five tags, manual
re-integration and isomer splitting, the OpenQuant interop, the principal components, clustering
and molecular network, and the headless and visual tests.

## Updating this page

Every version updates the manual with it: any new workspace, control, setting, file, script or
known trap gets its sentence on the page it belongs to, this page gets its notes, and the PDFs are
rebuilt, in both languages. The manual's tests hold the first part; the second is a habit.
