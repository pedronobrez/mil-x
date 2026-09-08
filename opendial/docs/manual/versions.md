---
title: Versions
section: Help
order: 61
summary: What each version of OpenDIAL brought; the notes are updated with every release.
---

# Versions

The version shown in **Help ▸ About OpenDIAL** and in the window's probe is the one stamped in
`opendial/Directory.Build.props`. This page names it, and a test fails the build when it does not.

## 0.2.0 — September 2026

The current version. It tracks MS-DIAL 5.5.260817.

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
known trap gets its sentence on the page it belongs to, this page gets its notes, and the PDF is
rebuilt. The manual's tests hold the first part; the second is a habit.
