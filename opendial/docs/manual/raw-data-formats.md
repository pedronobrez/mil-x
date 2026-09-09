---
title: Raw data formats
section: Data and files
order: 31
summary: Which formats open directly, how SCIEX .wiff is read natively, how other vendor formats go through msconvert, the legacy formats, and what each route needs.
---

# Raw data formats

MS-DIAL reads raw data through a closed, Windows-only library. OpenDIAL replaces it with an open
layer that reads the open formats itself, reads SCIEX `.wiff` through the vendor's own managed
SDK, and hands everything else to ProteoWizard's msconvert once, keeping the result. The Format
badge in the [[samples-workspace]] says which route a file takes.

| Format | Route | Needs |
| --- | --- | --- |
| **mzML**, indexedmzML | read directly | nothing |
| **SCIEX `.wiff`** (+ `.wiff.scan`) | native reader | the SCIEX plugin in the bundle (`plugins/sciex`) |
| SCIEX `.wiff2` | native reader, through the `.wiff` beside it | the plugin, and the `.wiff` SCIEX OS writes with every `.wiff2`; a `.wiff2` on its own goes to msconvert |
| Thermo `.raw` | msconvert | msconvert or Docker |
| Agilent, Bruker `.d` folders | msconvert | msconvert or Docker; added with **Add folder…** |
| Shimadzu `.lcd`, `.qgd` | msconvert | msconvert or Docker |
| MS-DIAL `.ibf`, `.abf`, NetCDF `.cdf`, `.imzML` | the legacy MS-DIAL reader | its dll in `plugins/legacy`; `.abf` is Windows-only; `.cdf` also needs libnetcdf |

## mzML

Read by OpenDIAL's own streaming reader: zlib and MS-Numpress compression, 32- and 64-bit floats
and integers, referenceable parameter groups, chromatograms, ion mobility (drift time and 1/K0).
Two defects of the closed reader are deliberately not reproduced: the last peak of every spectrum
is kept (the closed reader drops it), and the base-peak m/z is the m/z of the most intense peak.
Every number is parsed with a period as the decimal mark whatever the machine's locale; see
[[troubleshooting#Processing]].

Keep the method's data type at **Centroid** for converted files: msconvert applies vendor peak
picking during conversion. Profile mzML needs **Profile** in the method.

## SCIEX .wiff

Read directly through SCIEX's Clearcore2 assemblies — the same route OpenQuant uses — in about a
second per file plus the vendor library's own read time. The `.wiff.scan` file has to sit beside
the `.wiff`. Three things the reader does that decide whether a run matches MS-DIAL on Windows:

- **The precursor of a dependent (IDA) experiment is taken per scan**, from the spectrum, not from the experiment's placeholder mass, which is the same in every dependent slot.
- **Profile spectra are centroided by SCIEX's own peak finder**, which fits the apex and reports it about a quarter higher than a local maximum would. Peak heights feed the minimum-height cut-off, so this is the difference between a height ratio of 0.85 and one of 1.000 against MS-DIAL. `OPENDIAL_WIFF_CENTROID` selects `sciex` (default), `msdial` (the local-maximum method) or `0` (keep profile, and set the method to Profile).
- **A multi-sample batch file becomes one injection per sample.** The application creates a `wiff-samples` folder beside the file with one link per sample (`name.s1.wiff`, `name.s2.wiff`, …, each with its `.wiff.scan` link) and remembers which sample each stands for; a project reopened later re-registers them from the `.sN` suffix. On a share that refuses links the file is copied instead.

**`.wiff2`.** SCIEX OS writes every acquisition twice — a `.wiff2`, its newer container, and a
`.wiff` for compatibility — both over the one `.wiff.scan` that holds the spectra. The SDK's own
`.wiff2` reader needs a native SQLite library that does not exist for this platform, so OpenDIAL
reads a `.wiff2` through the `.wiff` beside it: the same spectra, the same sample names, the log
says `read through …`. Adding a folder takes one file per acquisition — the `.wiff` — and a
`.wiff2` picked by hand beside its `.wiff` is swapped for the `.wiff` with a note in the message
line, so an injection is never processed twice. A `.wiff2` on its own, with no `.wiff` next to
it, is not claimed by the plugin and goes through msconvert; its badge says `wiff · msconvert`.

Without the plugin — a build made without the SDK, or the folder missing from the bundle — `.wiff`
falls back to msconvert too. And a file the plugin claims but cannot open is handed to msconvert
rather than failing the run; the log says which reader gave up and why.

## The msconvert bridge

Vendor files the application cannot read itself are treated as mzML that has not been produced
yet. On first access the file is converted with `msconvert --mzML --64 --zlib --filter
"peakPicking vendor msLevel=1-"`, the mzML is written beside the source (or into the conversion
cache folder from [[settings]]) and read from then on; a pre-converted `<name>.mzML` newer than
the source is used as it is. The Converting stage of the progress band shows it happening, once.

Two converters, chosen in [[settings]]:

1. **Docker** — Docker Desktop with the official `proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses` image, which bundles the vendor readers under Wine; on Apple Silicon it runs under x86-64 emulation, slowly (minutes per file) but exactly. `docker pull` the image once.
2. **A native msconvert** on the PATH or at the path you give — a Windows machine or a Linux box with the image can also convert, and the mzML copied across is picked up.

The environment variables that tune it are in [[environment-variables]]. Without a converter the
run stops at the first vendor file with a message naming both options.

## Legacy formats

The original MS-DIAL reader dll (`RawDataHandler-Vendor-UnSupported.dll`, from the upstream
NuGet package) can be dropped into `<app>/plugins/legacy/`, and `.ibf`, `.cdf` and `.imzML` are
then read through it by reflection. `.ibf` is pure managed and works; `.cdf` also needs
`libnetcdf` (`brew install netcdf`); `.abf` needs a Windows-only native library and cannot work on
macOS or Linux — convert it to mzML instead.

## Plugins

Additional readers implement `IRawFileReaderPlugin` and are loaded from `<app>/plugins` (any
subfolder) and from `$OPENDIAL_PLUGINS`. Lower priority numbers win when two can read a file. A
Thermo RawFileReader plugin is the obvious candidate: Thermo's .NET library runs on macOS and Linux
but is separately licensed, so it is not bundled.

## What the survey cache changes

None of the above is repeated when a project is reopened: after the first read the survey scans of
a file are kept on disk, and the Explorer, the peak grid and the spectra come from there in
milliseconds. Processing always reads the original file. See [[caches-and-storage]].
