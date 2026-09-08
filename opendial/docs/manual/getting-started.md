---
title: Getting started
section: Start
order: 2
summary: Installing the application, opening it for the first time, and a first project end to end.
---

# Getting started

## Installing on macOS

OpenDIAL ships as a self-contained application bundle, `OpenDIAL.app`. It carries its own .NET
runtime and, when it was built with the SCIEX SDK, the native `.wiff` reader in
`Contents/MacOS/plugins/sciex`. Nothing else needs installing to read mzML or `.wiff` files.

1. Copy `OpenDIAL.app` to `/Applications`. If you built it yourself, use `ditto` rather than
   Finder's drag so the signature stays intact:
   ```bash
   ditto opendial/dist/OpenDIAL.app /Applications/OpenDIAL.app
   ```
2. Open it. A bundle built on the same machine launches straight away. A copy that arrived by
   download or AirDrop carries macOS's quarantine flag and, because the bundle is not notarised,
   needs **right-click ▸ Open ▸ Open** once; after that it opens normally.
3. On first launch the window opens empty, on the Samples workspace, with the wordmark and a hint.

The application registers the document types it understands, so from then on Finder opens
`.odproj` and `.mdproject` projects, `.oqproj` batches and raw files (`.mzML`, `.wiff`, `.raw`, …)
in OpenDIAL with a double click or a drag onto its icon. See [[projects-and-files]] and
[[raw-data-formats]] for what each does.

Building from source, and the Linux and Windows builds, are in [[building-and-testing]].

## The window in one minute

One window, five workspaces along the top, a status bar along the bottom. `⌘1` to `⌘5` (or
`Ctrl+1` to `Ctrl+5`) switch between them:

| Workspace | What it is for |
| --- | --- |
| **Explorer** | the raw files as chromatograms and spectra, before or after processing |
| **Analytics** | the review of a processed result: the ion table and the evidence for each feature |
| **Method** | the processing parameters |
| **Samples** | the batch: which files, what each one is |
| **Statistics** | the dataset as a whole: principal components, drift correction, models, clustering, the molecular network |

The rest of the shell — menus, the status bar, the log, the progress band — is described in
[[shell]].

## A first project, end to end

1. **File ▸ New project…** (`⌘N`). Give it a name and a folder; the project file and its results
   folder are created inside a folder of that name. See [[new-project-wizard]].
2. **Add data files…** or **Add folder…**. Every supported raw file in the folder is added, one row
   per injection; a multi-sample `.wiff` becomes one row per sample. Set the **Type** of each row
   (Sample, Blank, QC, Standard) and its **Class** — the group the statistics compare. Both can be
   set for a selection at once from the toolbar. See [[samples-workspace]].
3. Choose the method: the LC-MS defaults (DDA, positive, centroid), the GC-MS defaults, or a
   method file from an earlier run. Point it at an MSP spectral library if you have one; without
   one every feature stays unknown. See [[method-workspace]] and [[method-parameters]].
4. **Create project**, with **Process the batch right away** ticked, or press `⌘R` later. The
   progress band shows the stage and the file; the log (`⌘L`) shows every line the engine prints.
   Eight ZenoTOF acquisitions take a few minutes; the first read of a vendor file is the slow
   part and is cached for the next time. See [[processing]].
5. When it finishes the window lands on **Analytics** with the ion table filled. Select a feature,
   look at its peak in every sample, its MS/MS against the library, its candidates; tag it with
   `Ctrl+1` to `Ctrl+5` or **Confirm ▸** and **Reject ▸**. **Save review** writes the tags to the
   file MS-DIAL reads. See [[analytics-workspace]].
6. **Export reviewed table…** writes what the table is showing, with the review as columns.
   **Export to OpenQuant…** turns the list into a targeted method. See [[exports]].

Everything you did is in the project file: reopen it from **File ▸ Recent projects** and the
results, the review and the method come back.

## Opening something that already exists

- An OpenDIAL project (`.odproj`) or an MS-DIAL project (`.mdproject`): **File ▸ Open project…** (`⌘O`), or from Finder.
- A folder of results written by the console or by MS-DIAL, with no project file: **File ▸ Open results folder…**. The batch is reconstructed from the result files.
- A raw file, or a folder of them, just to look: drop it on the application, or **File ▸ Add data files…**. It opens in the Explorer, unprocessed.
- An OpenQuant batch (`.oqproj`): **File ▸ Import OpenQuant batch…** adds its samples with their types and groups. See [[openquant]].

A processed project opens on Analytics; an unprocessed one on Samples.
