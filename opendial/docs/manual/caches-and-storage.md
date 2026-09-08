---
title: Caches and storage
section: Data and files
order: 33
summary: The survey-scan cache that makes reopening a project immediate, the converted-mzML cache, the wiff-samples links, and where the settings live.
---

# Caches and storage

## The survey-scan cache

A vendor file costs the same read every time a project is opened — about 25 seconds for a 60 MB
ZenoTOF `.wiff` — and a review pass over eight of them used to start with three minutes of
waiting. After the first read, every spectrum of a file is written to a cache, and the Explorer,
the peak grid, the channel tree and the product spectra come from there:

| | |
| --- | --- |
| vendor library, one 60 MB `.wiff` | 24 882 ms |
| the same file from the cache | 87 ms |
| what is stored | 484 scans, 4 295 640 centroids, 32 MB |

It is a display cache, never a processing input: a run always reads the original file, so nothing
quantitative depends on it. Masses are kept as tenths of a millidalton in a 32-bit integer — two
orders finer than any tolerance a chromatogram is extracted with — which halves both the file and
the time to read it.

Entries are keyed by the file's own identity — path, size and last write time — so an edited or
replaced file never serves a stale one. The store is capped at 8 GB and evicted least-recently-used.

It lives in `~/Library/Caches/OpenDIAL/spectra` on macOS, `$XDG_CACHE_HOME/OpenDIAL/spectra` on
Linux and under `LocalAppData\OpenDIAL\spectra` on Windows; `OPENDIAL_CACHE` moves it.
[[settings]] shows its size and empties it. Deleting the folder by hand is safe: it costs one slow
read per file.

## The converted-mzML cache

A vendor file that goes through msconvert becomes `<name>.mzML` beside the source, or inside the
conversion cache folder set in [[settings]] (`OPENDIAL_MZML_CACHE` does the same). It is reused
while it is newer than the source; `OPENDIAL_MSCONVERT_FORCE=1` reconverts. These files are full
mzML and can be used by anything else that reads mzML.

## The wiff-samples links

A multi-sample `.wiff` gets a `wiff-samples` folder beside it holding one symbolic link per
sample (`name.s2.wiff` with its `.wiff.scan`), because the engine identifies an injection by its
path. They are recreated when missing and cost nothing; on a share that refuses links the file is
copied instead. See [[raw-data-formats#SCIEX .wiff]].

## The settings file

`settings.json` in the application-data folder — `~/.config/OpenDIAL` on macOS and Linux,
`%APPDATA%\OpenDIAL` on Windows, or wherever `OPENDIAL_SETTINGS_DIR` points. What it holds is in
[[settings#The settings file]].

## What a project does not store

Nothing from the caches: a project moved to another machine reads its raw files afresh. Nothing
from the statistics workspace: a drift correction or a fitted model is recomputed on demand and
never written. The curation is stored, beside the alignment; see [[projects-and-files]].
