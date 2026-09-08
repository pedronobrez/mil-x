---
title: Processing a batch
section: Workspaces
order: 13
summary: What happens when you press Process — the stages, the progress, what is written, how long it takes, and what can stop it.
---

# Processing a batch

**Process batch** — `⌘R`, the toolbar of the Analytics workspace, or the last step of the wizard —
runs the complete MS-DIAL workflow on the batch: reading the raw files, peak picking, MS/MS
deconvolution, annotation against the library, alignment across the injections, gap filling, and
the exports. It runs in the background; the window stays usable and switches to Analytics.

## Before it starts

The run refuses, with a message in the status bar, when the batch is empty (it switches to
Samples), when a run is already going, or when the method names an MSP library that does not
exist (it switches to Method). When no output folder is set, one is chosen: `results` beside the
project file, or `opendial_results` beside the first raw file for a batch that has no project.

## The stages

The progress band names them; the log (`⌘L`) has the detail.

| Stage | What happens |
| --- | --- |
| **Setup** | the output folder is created; the method is written to it as `opendial_method.txt`; the retention-time correction step is prepared |
| **Converting** | any vendor file the application cannot read natively goes through msconvert to mzML, once, and the mzML is kept for next time; see [[raw-data-formats]] |
| **Setup** — loading libraries | the MSP, LBM and text libraries named in the method are read; the log says how many records each holds |
| **Processing** | each injection: peak picking, deconvolution, annotation. Files run in parallel, half as many at a time as the method's thread count; the band shows the file and its percentage |
| **Export** | per-file peak tables (`.mdpeak`) and MS/MS spectra (`.mdmsp`) |
| **Alignment** | the peaks of every injection are matched into features against the reference file, gap-filled, and refined; then the alignment table, the QA matrix, the alignment spectra and the mzTab-M are written |
| **Project** | the MS-DIAL project (`.mdproject` + `.mddata`) is saved into the results folder |
| **Done** | the result is loaded into Analytics and Statistics, and the OpenDIAL project is saved if it has a path |

Every file the stages write is listed in [[projects-and-files#The results folder]].

## Acquisition types

Each injection has an acquisition type — DDA, SWATH or AIF — set in the Samples workspace and
defaulting to the method's. It decides how the product spectra of a peak are collected: with
**DDA** a product spectrum belongs to a peak only when its precursor sits within the centroid
tolerance of the peak's m/z; with **SWATH** and **AIF** anything inside the isolation window is
taken. A SCIEX IDA acquisition is DDA. Worth knowing when comparing with a Windows project: MS-DIAL
5.5 marked the IDA files of the validation dataset as SWATH, which is the looser rule; running the
same data as DDA assigns MS/MS to fewer peaks (897 against 1 218 on the first file of that set).

## How long it takes

The first read of a vendor file is the slow part — about 25 seconds for a 60 MB ZenoTOF `.wiff`
through the SCIEX library — followed by peak picking at a similar cost. Eight such injections
process and align in a few minutes on an Apple Silicon laptop. Reopening the project afterwards is
immediate, because the survey scans are cached after the first read; see [[caches-and-storage]].
A large MSP library (the 224 MB lipid library used for validation) takes a while to load and is
loaded once per run.

## Cancelling

**Cancel** on the band, or **Process ▸ Cancel run**, stops at the next checkpoint: between files,
between stages. What was written stays in the folder; nothing is loaded.

## When it fails

The status bar says `Run failed: …`, the log opens by itself with the full error, and nothing is
loaded. The usual causes are in [[troubleshooting]]: a raw file that could not be read, a library
path that moved, msconvert not found, a method value that could not be parsed.

## Numbers to expect

On the eight-injection ZenoTOF 7600 lipidomics run used to validate the port, against MS-DIAL 5.5
on Windows with the same parameters: 9 981 peaks against 9 852 across the samples; 1 889 of the
first sample's 1 906 peaks matched one to one with a height ratio of 1.000; 1 611 aligned features
against 1 731. The deconvoluted MS/MS spectra of shared peaks match one for one. Where the names
differ it traces to the library build, not the processing; see [[annotation#Why two runs can disagree]].
