---
title: Getting started
section: Start
order: 2
summary: Installing the application, opening it for the first time, and a first project end to end.
---

# Getting started

## Where to get it

Every release carries a build for each platform, at
[github.com/pedronobrez/mil-x/releases](https://github.com/pedronobrez/mil-x/releases). They are
self-contained: the .NET runtime travels inside, so nothing has to be installed first.

| Platform | File | What it is |
| --- | --- | --- |
| macOS, Apple silicon | `OpenDIAL-<version>-macos-arm64.dmg` | the application bundle, to drag to Applications |
| Windows x64 | `OpenDIAL-<version>-windows-x64.msi` | an installer, per user, no administrator needed |
| Windows x64 | `OpenDIAL-<version>-windows-x64.zip` | the same application without an installer |
| Linux x86_64 | `OpenDIAL-<version>-linux-x86_64.tar.gz` | a folder to unpack and run |

None of them carries the SCIEX Clearcore2 SDK, because its licence forbids passing it on: `.wiff`
reading is set up on your own machine, once, by `scripts/fetch-sciex-assemblies.sh`. Without it
`.wiff` goes through msconvert and the application says so. See [[raw-data-formats]].

`SHA256SUMS` on the release page has the checksum of every file.

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

## Installing on Windows

The `.msi` installs into `%LOCALAPPDATA%\OpenDIAL` **for the current user**, so no administrator
is involved — which matters in a lab where the person analysing the data is rarely the person with
the admin password. It adds a Start menu entry, appears in **Add or remove programs**, and an
upgrade replaces the previous version rather than sitting beside it.

1. Double-click the `.msi`. Windows shows **"Windows protected your PC"**, because the build has no
   code-signing certificate: **More info ▸ Run anyway**. Only a certificate removes that warning.
2. Start it from the Start menu, or double-click any `.odproj` project — the installer registers
   that extension, with the application's icon.
3. `.mdproject` stays with MS-DIAL, which is probably installed on the same machine; OpenDIAL only
   adds itself to that file type's **Open with** list.

The `.zip` is the same application with no installer and nothing written to the registry: unpack it
anywhere and run `OpenDIAL.exe`. Unblock the zip before extracting (right-click ▸ **Properties ▸
Unblock**) or Windows marks every file inside it.

MS-DIAL 5 itself runs on Windows, and there it does more than this port does — ion mobility and
imaging among it. The Windows build exists so that a machine running Windows can open and continue
a review started on a Mac or on Linux, and so a mixed lab shares one set of files.

## Installing on Linux

Unpack the tarball and run the binary:

```bash
tar -xzf OpenDIAL-<version>-linux-x86_64.tar.gz
cd OpenDIAL-<version>-linux-x86_64
./OpenDIAL
```

If the executable bit did not survive the copy, `chmod +x OpenDIAL` puts it back. What the
distribution has to supply is the X11 client libraries and fontconfig, which every desktop Linux
already has; on a bare server or container image:

```bash
apt-get install -y libx11-6 libice6 libsm6 libfontconfig1 libicu-dev   # Debian, Ubuntu
dnf install -y libX11 libICE libSM fontconfig libicu                   # Fedora, RHEL
```

There is no desktop entry in the tarball: create one pointing at the binary if you want it in the
applications menu.

Building from source, for any of the three, is in [[building-and-testing]].

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
   `⌘⇧1` to `⌘⇧5` or **Confirm ▸** and **Reject ▸**. **Save review** writes the tags to the
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
