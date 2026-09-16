---
title: Projects and files
section: Data and files
order: 30
summary: The MIL-X project file, the MS-DIAL project, every file in a results folder and what each holds, the curation files, backups, and how paths are kept.
---

# Projects and files

## The MIL-X project (.milx)

A small JSON file — the batch, the method and where the results are — that reopens the whole
session. It lives beside the results, is written by the wizard and by **Save project** (`⌘S`), and
is what **File ▸ Recent projects** lists.

Until 1.0 the same file was `.odproj`. One from then still opens everywhere a `.milx` does, and the
next **Save** writes it as `.milx` beside the old file, which is left alone.

```json
{
  "Version": 1,
  "Name": "liver-cohort-A",
  "Mode": "LCMS",
  "OutputFolder": "results",
  "MdprojectPath": "results/Project-2609071200.mdproject",
  "MethodText": "#Data type\nMS1 data type: Centroid\n…",
  "Samples": [
    {
      "Path": "../raw/260406-Teste-51-I.wiff",
      "Name": "260406-Teste-51-I",
      "Class": "liver",
      "SampleType": "Sample",
      "Acquisition": "DDA",
      "AnalyticalOrder": 1,
      "Batch": 1,
      "Dilution": 1,
      "Comment": "",
      "Included": true,
      "SampleIndex": 0
    }
  ]
}
```

| Field | Meaning |
| --- | --- |
| `Mode` | `LCMS` or `GCMS`: which engine the run uses |
| `OutputFolder` | where the run writes; relative to the project file when it is nearby |
| `MdprojectPath` | the MS-DIAL project of the last run, once there is one; what **Open project** loads the results from |
| `MethodText` | the whole method file, verbatim; see [[method-workspace]] |
| `Samples[].Path` | the raw file, relative when it is within two folders of the project, absolute otherwise |
| `Samples[].SampleType` | `Sample`, `Blank`, `QC` or `Standard` |
| `Samples[].Acquisition` | `DDA`, `SWATH` or `AIF` |
| `Samples[].SampleIndex` | which sample of a multi-sample `.wiff` this row is (0 for any other file) |

Paths are written relative when they stay inside a nearby tree — a project moved together with
its raw files still opens — and absolute when a relative path would climb three folders or more,
which is no more portable. Opening the project resolves them again.

## The MS-DIAL project (.mdproject + .mddata)

What the engine writes at the end of a run: `Project-<yyMMddHHmm>.mdproject`, a zip holding the
parameters, the file list, the alignment file list and the library, with the `.mddata` beside it.
MS-DIAL on Windows opens it; MIL-X opens it with **Open project…** or from Finder, and rebuilds
the batch from it when there is no `.milx`. It is the one file that carries the library inside
it, which is why a project opened this way can search its library without the MSP file being on
the machine.

**A project can be moved.** The paths inside it are absolute — where its dataset and its raw files
were when it was written — and those mean nothing on another machine: a different user name is
enough, and a Windows path is not a path at all on macOS or Linux. When the stored path is missing,
MIL-X looks for the dataset by name beside the project file, one level up, and in the folders
next to it, which is where a copied run keeps it. So a results folder handed over on a memory stick
opens, and one copied from a Windows machine opens too. What it cannot find is a dataset left
behind: copy the whole folder, not only the `.mdproject`.

## The results folder

Everything a run writes, in one folder. `<sample>` is the sample name, `<stamp>` a time stamp of
the run.

**Per injection**

| File | What it holds |
| --- | --- |
| `<sample>_<stamp>.pai2` | the detected peaks (MS-DIAL's peak-area information, MessagePack) |
| `<sample>_<stamp>.dcl` | the deconvoluted MS/MS spectrum of every peak |
| `<sample>_<stamp>_tags.xml` | the per-peak tags, which the run writes empty |
| `<sample>_<stamp>.rtc` | the retention-time correction, when one was computed |
| `<sample>.mdpeak` | the peak table export |
| `<sample>.mdmsp` | the MS/MS spectra export |
| `<sample>.mdscan` | GC-MS runs: the scan table |

**The alignment**

| File | What it holds |
| --- | --- |
| `AlignResult-<stamp>.arf2` | the alignment result: every feature with its per-injection peaks — the container the review edits |
| `AlignResult-<stamp>_PeakProperties.arf2` | the per-injection peak properties of the features |
| `AlignResult-<stamp>_DriftSopts.arf2` | the drift-time spots (ion mobility; empty otherwise) |
| `AlignResult-<stamp>.EIC.aef` | the extracted ion chromatograms the aligner kept |
| `AlignResult-<stamp>.dcl` | the representative MS/MS of every feature |
| `AlignResult-<stamp>_tags.xml` | the review tags, in MS-DIAL's schema; see [[review-tags]] |
| `AlignResult-<stamp>_curation.json` | MIL-X's sidecar: comments, hand-picked names, reviewed flags |
| `AlignResult-<stamp>.mdalign` | the alignment table export |
| `AlignResult-<stamp>.qa.tsv` | the QA matrix, long format |
| `AlignResult-<stamp>.mdmsp` | the representative MS/MS export |
| `AlignResult-<stamp>.mzTab` | mzTab-M |
| `*.before-curation` | the alignment files as they were before the first hand edit of a session |

**The run**

| File | What it holds |
| --- | --- |
| `milx_method.txt` | the method the run used |
| `Project-<stamp>.mdproject`, `.mddata` | the MS-DIAL project |

The container is written as `.arf2` while MS-DIAL's file list names it `.arf`; both spellings
refer to the same file, and the backups keep whichever exists.

## The tag file

`<alignment>_tags.xml` is MS-DIAL's own:

```xml
<PeakSpotTags>
  <Definitions>
    <Tag><Id>1</Id><Label>Confirmed</Label></Tag>
    …
  </Definitions>
  <Peaks>
    <Peak Id="123"><Tag>1</Tag></Peak>
    <Peak Id="456"><Tag>4</Tag><Tag>5</Tag></Peak>
  </Peaks>
</PeakSpotTags>
```

The ids are MS-DIAL's (1 Confirmed, 2 Low quality spectrum, 3 Misannotation, 4 Coelution, 5
Overannotation). An unreadable tag file is ignored rather than stopping the results from opening.

## The curation sidecar

`<alignment>_curation.json` holds what MS-DIAL keeps inside its binary files instead, so a tag-only
review never rewrites them:

```json
{ "Version": 1, "Spots": [ { "Id": 123, "Comment": "shoulder", "ManualName": "PC 34:1", "Reviewed": true } ],
  "InternalStandards": { "PC": 88, "PE": 412 } }
```

Only spots with something to say are listed; the file is deleted when there is nothing left.
`InternalStandards` is the standard chosen for each lipid class in the Statistics workspace,
as the alignment ID of the confirmed feature (see [[internal-standards]]); a class set to none is
absent.

## Backups

The first hand edit of a session — a re-integration or a split — copies the alignment container,
its peak properties and its drift spots to `<name>.before-curation` before **Save review**
overwrites them. Later edits in the same session do not replace the backup, so it always holds the
result as the run produced it. To undo every edit, rename the backups back over the originals.

## Opening a results folder without a project

**File ▸ Open results folder…** reads whatever is there: the newest `.mdproject` when there is
one, otherwise the `.pai2` files themselves. In the second case the batch is reconstructed with
the sample names, the raw files found beside them by name, one class per file, and the method from
`milx_method.txt` when it exists — enough to review, not enough to rerun without checking the
batch first.

## Where the application keeps its own files

The settings (`settings.json`), the survey-scan cache and the converted-mzML cache are described
in [[caches-and-storage]] and [[settings]].
