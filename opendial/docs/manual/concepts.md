---
title: Concepts and vocabulary
section: Start
order: 3
summary: The words the application uses — injection, class, feature, alignment, annotation level, tag — and what is the same as MS-DIAL.
---

# Concepts and vocabulary

The application uses a small, fixed vocabulary. Most of it is MS-DIAL's; where OpenDIAL has a word
of its own it is said so. The [[glossary]] has the short definitions; this page explains the ideas.

## The batch

An **injection** — a **sample** in the Samples workspace — is one raw file, or one sample inside a
multi-sample `.wiff` batch file. The batch is the list of injections a project processes together.

Each injection has a **type**, which says what it is physically: **Sample**, **Blank**, **QC**
(a pooled quality-control injection, the same material every time) or **Standard**. The type is
what the drift correction and the abundance views read: the controls are the type `QC`, the
background is the type `Blank`. See [[statistics-workspace#Drift correction]].

Each injection also has a **class**, free text, which is the group the statistics compare:
`liver`, `plasma`, `treated`, `control`. MS-DIAL calls it the class too. Everything grouped "by
class" — the abundance bars, the per-class statistics, the models — groups on this.

The **analytical order** is the position in the injection sequence; the **batch** number groups
injections that were run in one sequence. Both matter to the drift correction, nowhere else.
**Dilution** and **comment** are kept for the record and written to exports.

## The method

The **method** is the full set of processing parameters — data type, peak detection, deconvolution,
annotation, alignment — as one plain-text file of `Key: value` lines, the format MS-DIAL's own
console reads. The Method workspace edits it as a form and as text. See [[method-workspace]] and
[[method-parameters]].

## What processing produces

For each injection, **peak picking** finds **peaks**: a chromatographic peak at one m/z, with a
retention time, an apex height, an area and a start and end. **Deconvolution** (MS-DIAL's MS2Dec)
assigns each peak a clean product-ion spectrum. **Annotation** matches that spectrum against the
library and names the peak.

**Alignment** then matches peaks across the injections into **features** — MS-DIAL calls them
**alignment spots**, and both words appear in the interface. A feature has one retention time and
one m/z, one name, and one peak per injection; where an injection had no peak, **gap filling**
integrates the chromatogram at that place anyway and marks the value as gap-filled. The **fill
percentage** is the share of injections in which the peak was actually detected.

The **representative** injection of a feature is the one whose peak scored best against the
library; its spectrum and its name are the feature's.

## Annotation levels

MS-DIAL writes the confidence of an annotation into the name itself, and OpenDIAL reads it back
out as the **level** column of the ion table:

| Name reads | Level | Meaning |
| --- | --- | --- |
| `PC 34:1` | confident | the MS/MS matched a library record above every cut-off |
| `low score: PC 34:1` | suggested | a match below the total-score cut-off, kept as a hint |
| `no MS2: PC 34:1` | m/z only | no product spectrum; the name rests on the precursor mass alone |
| `Unknown`, `w/o MS2: …` | *(unknown)* | nothing matched |

The lipid name itself is not the library record's name: MS-DIAL rewrites it from the fragments it
actually saw, so one record becomes `LPC 16:0` when only the species is supported and
`LPC 16:0/0:0` once a chain is. See [[annotation]].

## The review

A **tag** is one of MS-DIAL's five review flags — Confirmed, Low quality spectrum, Misannotation,
Coelution, Overannotation — with the same names and the same numeric ids, so a review done here
shows up in MS-DIAL and one done there shows up here. A feature is **reviewed** when it carries any
tag, or when the reviewer marked it so without one. See [[review-tags]].

**Curation** is everything the reviewer changes: tags, comments, a hand-picked name, a
re-integrated peak, a split isomer. Tags go to MS-DIAL's own tag file; the rest goes to a sidecar
of OpenDIAL's, or — for the peaks — back into the alignment result itself. See
[[projects-and-files]].

## Projects and results

An **OpenDIAL project** (`.odproj`) is a small JSON file: the batch, the method text, the results
folder, and the path of the MS-DIAL project once there is one. An **MS-DIAL project**
(`.mdproject` with its `.mddata`) is what the engine writes: the parameters, the file list and the
library, in MS-DIAL's own format, which the Windows application opens too. The **results folder**
holds both, plus every intermediate and exported file. See [[projects-and-files]].

## What is the same as MS-DIAL

- The engine: peak picking, MS2Dec, the annotators, the aligner, gap filling, the exporters — all upstream code, version 5.5.260817, unchanged.
- The method file and every parameter in it.
- The project (`.mdproject`), the per-file results (`.pai2`, `.dcl`) and the alignment result (`.arf2`), readable by the Windows application.
- The five tags and the tag file.
- The exports: `.mdpeak`, `.mdmsp`, `.mdalign`, `.qa.tsv`, `.mzTab`.

## What is OpenDIAL's own

- The raw-data layer: the mzML reader, the native `.wiff` reader, the msconvert bridge. See [[raw-data-formats]].
- The desktop application and its five workspaces.
- The comment, hand-picked name and reviewed flag sidecar; the reviewed-table export; the OpenQuant interop.
- The statistics workspace: principal components, clustering, molecular network, drift correction, the discriminant and orthogonal models.
- The survey-scan cache and the OpenDIAL project file.
