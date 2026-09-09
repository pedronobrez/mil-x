# OpenDIAL — MS-DIAL 5 for macOS (and Linux)

OpenDIAL is an open, cross-platform port of [MS-DIAL 5](https://github.com/systemsomicslab/MsdialWorkbench)
(untargeted metabolomics / lipidomics data processing, LGPL-3.0). The MS-DIAL engine is used
unchanged; the Windows-only pieces are replaced:

* **`OpenDIAL.RawData`** – an open raw-data layer that is a drop-in replacement for the closed
  `RawDataHandler` package: native mzML reader, **native SCIEX `.wiff`** (Clearcore2 plugin, the
  same route OpenQuant uses), other vendor formats (Thermo `.raw`, Agilent/Bruker `.d`,
  Shimadzu `.lcd`) through a ProteoWizard *msconvert* bridge, optional reflection bridge to the
  original dll for `.ibf`/`.cdf`/`.imzML`.
* **`MSDIALCUI` console** – built and published as a self-contained macOS ARM64/x64 or Linux binary.
* **`OpenDIAL.Desktop`** – a new Avalonia desktop application that shares OpenQuant's design language
  and shell (Explorer / Analytics / Method / Samples / Statistics workspaces, same palette, light and dark),
  with MS-DIAL's review loop: an ion table, its five curation tags, manual re-integration and isomer
  splitting, a library re-search, and the multivariate views.
* **`OpenDIAL.Interop.OpenQuant`** – export aligned features as an OpenQuant component table and import
  an OpenQuant `.oqproj` batch, so discovery in OpenDIAL flows into targeted quantitation in OpenQuant.
* **`OpenDIAL.Pipeline`** – headless orchestration library shared by the GUI and tests.

Everything is verified end to end on Apple Silicon with a synthetic LC-MS/MS DDA study
(`tools/make_synthetic_mzml.py`): 12/12 compounds annotated, alignment across 3 samples,
identical results to the Windows reader.

```bash
bash opendial/scripts/setup-macos.sh     # .NET 8 SDK (no sudo) + NuGet source
bash opendial/scripts/build-cli.sh       # -> opendial/dist/opendial-cli-osx-arm64/opendial-cli
bash opendial/scripts/test.sh            # unit tests + end-to-end synthetic run
opendial/scripts/smoke-ui.py --project X # drive the installed app and check it really works
opendial/scripts/smoke-ui.py --process DIR --library L.msp   # …and process, review, save and export a real batch
python3 opendial/scripts/build-manual.py # -> opendial/dist/OpenDIAL-manual-<version>.pdf (and .html)
bash opendial/scripts/ui-drive.sh        # the same primitives one at a time, for a screenshot
dotnet run --project opendial/src/OpenDIAL.Desktop -c Release -p:UseOpenRawData=true   # GUI (or scripts/build-gui.sh)
bash opendial/scripts/make-app-bundle.sh # -> opendial/dist/OpenDIAL.app (double-clickable, ad-hoc signed)
```

## Installing the desktop application on macOS

`scripts/make-app-bundle.sh` publishes the application, draws the icon (`tools/make_icon.py`) and
assembles `dist/OpenDIAL.app` — self-contained, with the SCIEX plugin, ad-hoc signed so Apple
Silicon will run it. Install it with `ditto dist/OpenDIAL.app /Applications/OpenDIAL.app`, which
keeps the signature intact. A locally built bundle carries no quarantine flag, so it launches
straight away; a copy that travels through a download or an AirDrop is quarantined, and that
one needs right-click ▸ **Open** ▸ **Open** once, because the bundle is not notarised.

While `/Applications/OpenDIAL.app` exists the packaging script leaves the document types to it
and unregisters the copy in `dist/`, so Finder never has two bundles claiming the same files.

Finder hands documents to the application, on a cold start and while it is already running:
`.odproj` and `.mdproject` projects open as projects, `.oqproj` imports an OpenQuant batch, and a raw
file (`.mzML`, `.wiff`, `.raw`, …) opens straight in the Explorer. Agilent/Bruker `.d` folders are
supported, but Finder cannot bind a folder to an application, so open those from **File ▸ Add folder…**.


![explorer](docs/images/explorer.png)

![analytics](docs/images/analytics.png)

A TripleTOF `.wiff` opened natively in the Explorer:

![wiff](docs/images/explorer-wiff.png)

## The manual

The complete user manual lives in [docs/manual/](docs/manual/): one Markdown page per topic, linked
with `[[wikilinks]]` the way an Obsidian vault is. The same pages are embedded in the application —
**Help ▸ OpenDIAL manual**, or F1 for the page of the workspace that is showing — with a search
box and cross-references, and compiled into one PDF by `scripts/build-manual.py`. `ManualTests`
holds the manual to the build: every link resolves, every control in the interface is mentioned,
and the Versions page names the current version. The manual is updated with every version.

Documentation: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) (reverse-engineering report),
[docs/SCIEX-IDA.md](docs/SCIEX-IDA.md) (how SCIEX IDA is read, and how a run compares with MS-DIAL),
[docs/REVIEW-WORKFLOW.md](docs/REVIEW-WORKFLOW.md) (the ion table, the tags and the review panels),
[docs/STATISTICS.md](docs/STATISTICS.md) (the one-factor analysis after MetaboAnalyst — area
ratios to internal standards, preprocessing, the tests, volcano, ANOVA, PCA, PLS-DA, OPLS-DA,
random forest, heatmap, k-means, lipid enrichment, BioPAN-style pathways — plus drift correction and the molecular network,
every chart exportable as SVG and PNG),
[docs/VISUAL-TESTS.md](docs/VISUAL-TESTS.md) (how the stored frames are kept machine-independent),
[docs/PORTING.md](docs/PORTING.md) (status, vendor formats, limitations),
[docs/OPENQUANT-ALIGNMENT.md](docs/OPENQUANT-ALIGNMENT.md) (what is shared with OpenQuant),
[docs/adr/](docs/adr/) (decisions), [upstream-patches/](upstream-patches/README.md)
(the four upstream changes).

OpenDIAL is not affiliated with RIKEN or the MS-DIAL authors. Please cite MS-DIAL when you use it.
