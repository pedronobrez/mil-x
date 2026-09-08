---
title: The Samples workspace
section: Workspaces
order: 11
summary: The batch table — which files are in the project, what each injection is, and how to label many at once.
---

# The Samples workspace

The batch: one row per injection, with everything the run and the statistics need to know about
it. `⌘4` shows it. A project with no results opens here; a processed project keeps it one tab away.

![the batch table](images/samples.png)

## The toolbar

| Control | What it does |
| --- | --- |
| **Add data files…** | picks raw files; each becomes a row, a multi-sample `.wiff` several |
| **Add folder…** | every supported raw file directly inside a folder, in name order; the only way to add Agilent or Bruker `.d` folders |
| **Import OpenQuant batch…** | the samples of an `.oqproj` with their type, group, dilution and comment; see [[openquant]] |
| **Remove** | the selected rows |
| **Clear all** | every row |
| **Set type of selected** ▸ **Apply** | the chosen type on every selected row |
| **Set class of selected** ▸ **Apply** | the typed class on every selected row; the box completes from the classes already in the batch |

Rows are selected with a click, `⇧`-click for a range, `⌘`-click to add one. The message beside
the toolbar reports what the last action did — how many were added, which files were skipped as
unsupported, a `.wiff` that could not be opened.

## The columns

| Column | Meaning |
| --- | --- |
| **#** | the analytical order, renumbered when rows are removed |
| **Use** | untick to keep a row in the batch but leave it out of the run |
| **File** | the file name; hovering shows the full path, and `(file not found)` when it is missing |
| **Sample** | the name the results carry; defaults to the file name, or the sample name inside a `.wiff` batch |
| **Type** | Sample, Blank, QC or Standard — see [[concepts#The batch]] |
| **Class** | the group the statistics compare; free text, `1` until you set it |
| **Acquisition** | DDA, SWATH or AIF — how the MS/MS was acquired; see [[processing#Acquisition types]] |
| **Order** | the injection order, editable, for the drift correction |
| **Batch** | the sequence the injection was run in, for the drift correction |
| **Dilution** | a factor kept for the record and exported |
| **Comment** | free text |
| **Format** | how the file will be read: `mzML`, `wiff · native`, `raw · msconvert`, `d · msconvert`, `ABF`, …; amber when msconvert is needed |

The status line under the table counts the injections by type, the classes, how many `.wiff`
samples are read natively, how many files go through msconvert, and how many are missing.

## Types and classes, and why both

The **type** says what the injection is physically; the **class** says which group it belongs to
in the comparison. A batch of liver extracts against blanks might have types `Sample, Sample,
Sample, Blank, Blank` and classes `liver, liver, liver, blank, blank`; a pooled QC injected every
tenth run has type `QC` and, usually, class `qc`. The drift correction reads the type — it corrects
against every injection typed `QC` — and the models read the class. See
[[statistics-workspace#Drift correction]].

Labelling is done once and kept in the project. An OpenQuant batch brings its labels with it:
`Quality Control` becomes `QC`, `Blank`, `Double Blank` and `Solvent` become `Blank`, and the
OpenQuant group becomes the class.

## Multi-sample .wiff files

A SCIEX `.wiff` can hold several injections. When the native reader is present the file is
expanded into one row per sample as it is added; each row keeps the sample's own name and index,
and the run reads the right one. Behind the scenes a `wiff-samples` folder beside the file holds
one link per sample (`name.s2.wiff`), because the engine identifies an injection by its path. See
[[raw-data-formats#SCIEX .wiff]].

## Unprocessed batches

A batch can be looked at before it is processed: the Explorer draws any file in it. **Process
batch** (`⌘R`) from any workspace starts the run; it refuses with a message when there are no
files, when the library named in the method does not exist, or when a run is already going.
