---
title: Versions
section: Help
order: 61
summary: What each version of OpenDIAL brought; the notes are updated with every release.
---

# Versions

The version shown in **Help ▸ About OpenDIAL** and in the window's probe is the one stamped in
`opendial/Directory.Build.props`. This page names it, and a test fails the build when it does not.

## 0.5.0 — September 2026

The current version. It tracks MS-DIAL 5.5.260817.

**New**

- The **Two factors** page of the Statistics workspace: the two-way analysis of variance per feature with the interaction (type-II sums of squares, so unbalanced designs and empty cells are handled), and ASCA over the whole matrix with a permutation test per effect and a scores plot per effect. The second factor is a **Factor** column in the Samples workspace, saved with the project. See [[two-factor-analysis]].
- The manual in Portuguese, beside the English one: **Português** in the Help window, and a second PDF.

**Fixed**

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

- The pathway table is now BioPAN's own, transcribed from the tool's archived database: 97 reactions (51 between classes, 13 over the ether lipids, 3 over the sphinganine bases, 30 between fatty acids) with its 249 reaction–gene links; the twelve steps OpenDIAL adds are marked and switched on by **Beyond BioPAN**, off by default. The ether classes split into `O-` and `P-`, the ceramides and sphingomyelins into their sphinganine forms, as BioPAN has them.
- The reaction weights are compared as BioPAN compares them — the ratios themselves, not their logs — and the thresholds are BioPAN's, one-sided: 1.282, 1.645 (default), 2.054, 2.326.
- Every gene in the table is the human symbol; BioPAN's mouse `Scd1` and `Scd3` are `SCD` and `SCD5`.
- **Paired** on the Pathways page: BioPAN's paired comparison, the injections of the two classes taken one to one in order.
- A new mark for OpenDIAL: the same rounded square, gradient and white peak, with the whole chromatogram behind the peak instead of one co-eluting neighbour — the untargeted run, everything it contains. The two-peak mark, the deconvolution of one analyte, goes to OpenQuant. `tools/make_icon.py --export` writes either as PNGs, an `.iconset`, an `.icns`, an `.ico` and an SVG.
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
- The settings folder can be moved with `OPENDIAL_SETTINGS_DIR`.

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
