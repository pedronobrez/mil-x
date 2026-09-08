---
title: Method parameters
section: Reference
order: 42
summary: Every parameter of the method file — its key, its default, and what it does to the result — section by section.
---

# Method parameters

The method is a plain-text file of `Key: value` lines, the format MS-DIAL's console reads, edited
as a form in the [[method-workspace]]. This page lists every key the form models, with its default
for LC-MS and what changing it does. Keys are case-insensitive. Keys the form does not know are
kept verbatim under `#Other`.

The defaults are MS-DIAL's for a positive-mode LC-MS/MS DDA metabolomics run on a high-resolution
instrument. A Windows parameter export is converted into this format by
`tools/msdial_param_to_method.py` (see [[command-line]]), so a Windows run can be reproduced
exactly.

## Data type

| Key | Default | Meaning |
| --- | --- | --- |
| `MS1 data type` | `Centroid` | whether the survey scans are centroided or profile. Converted vendor files and natively read `.wiff` files are centroided; profile mzML needs `Profile` |
| `MS2 data type` | `Centroid` | the same for product scans |
| `Ion mode` | `Positive` | `Positive` or `Negative`; decides the adducts and the library polarity |
| `Target omics` | `Metabolomics` | `Metabolomics` or `Lipidomics`; lipidomics switches on MS-DIAL's lipid-name validation from fragments |
| `Acquisition type` | `DDA` | the default for new files: `DDA`, `SWATH` or `AIF`; each file can override it in the Samples workspace |
| `Machine category` | `LCMS` | `LCMS` or `GCMS`; written from the mode, not edited |

## Data collection

| Key | Default | Meaning |
| --- | --- | --- |
| `Retention time begin` / `end` | `0` / `100` | minutes; scans outside are ignored |
| `MS1 mass range begin` / `end` | `50` / `1500` | m/z; survey peaks outside are ignored |
| `MS2 mass range begin` / `end` | `50` / `1500` | m/z, for product scans |

## Centroid parameters

| Key | Default | Meaning |
| --- | --- | --- |
| `MS1 tolerance for centroid` | `0.01` | Da; the width a survey peak is centroided and extracted with — also the tolerance the review grid draws chromatograms at |
| `MS2 tolerance for centroid` | `0.025` | Da, for product scans |
| `Mass accuracy` | `0.01` | written equal to the MS1 centroid tolerance |

## Peak detection

| Key | Default | Meaning |
| --- | --- | --- |
| `Smoothing method` | `LinearWeightedMovingAverage` | also `SimpleMovingAverage`, `SavitzkyGolayFilter`, `BinomialFilter`, `LowessFilter`, `LoessFilter` |
| `Smoothing level` | `3` | the half-width of the smoothing window, in scans |
| `Minimum peak width` | `5` | scans; narrower peaks are dropped |
| `Minimum peak height` | `1000` | absolute intensity; the single biggest lever on how many peaks a run finds. Peak heights depend on the centroiding, which is why the SCIEX centroider matters (see [[raw-data-formats#SCIEX .wiff]]) |
| `Mass slice width` | `0.1` | Da; the width of the m/z slices the chromatograms are extracted in |
| `Max charge number` | `2` | the highest charge state considered when grouping isotopes |
| `Searched adduct ions` | `[M+H]+,[M+Na]+,[M+NH4]+` | comma-separated; the adducts the run groups peaks into and annotates with |

## Deconvolution

| Key | Default | Meaning |
| --- | --- | --- |
| `Sigma window value` | `0.5` | how wide, in peak widths, MS2Dec models a co-eluting contribution; larger tolerates worse chromatography |
| `Amplitude cut off` | `0` | absolute intensity below which product ions are dropped |
| `Keep isotope range` | `0.5` | Da; product ions this close above the precursor are kept as isotopes |
| `Exclude after precursor` | `True` | drop product ions heavier than the precursor |

## Identification

| Key | Default | Meaning |
| --- | --- | --- |
| `MSP file path` | | the spectral library; `.msp`, `.msp2`, `.lbm2`. Empty leaves every feature unknown |
| `Text DB file path` | | an optional retention-time/m/z text library |
| `RT tolerance for MSP-based annotation` | `0.5` | minutes, when retention time is used |
| `Mass range begin` / `end for MSP-based annotation` | `0` / `2000` | records outside are not searched |
| `Relative` / `Absolute amplitude cutoff for MSP-based annotation` | `0` / `0` | product ions below these are ignored when scoring |
| `Weighted dot product cutoff` | `0.4` | a match needs at least this |
| `Simple dot product cutoff` | `0.4` | |
| `Reverse dot product cutoff` | `0.4` | |
| `Matched peaks percentage cutoff` | `0.2` | at least this share of the reference peaks found |
| `Minimum spectrum match` | `1` | at least this many reference peaks found |
| `Total score cutoff` | `60` | per cent; below it a match is reported as `low score:` |
| `MS1 tolerance for MSP-based annotation` | `0.01` | Da; how far a library precursor may sit from the peak's m/z |
| `MS2 tolerance for MSP-based annotation` | `0.05` | Da; how far a fragment may sit from the reference's |
| `Use retention information for MSP-based annotation scoring` | `True` | add retention similarity to the score when the library carries retention times |
| `… filtering` | `False` | reject records outside the RT tolerance outright |
| `Only report top hit for MSP-based annotation` | `True` | keep one candidate per peak; `False` keeps the others for the Candidates tab |

The scores themselves are explained in [[evidence-panels#MS/MS]] and [[annotation]].

## GC-MS

Present only in GC-MS mode.

| Key | Default | Meaning |
| --- | --- | --- |
| `Retention type` | `RT` | `RT` or `RI`; whether identification uses retention time or retention index |
| `RI compound type` | `Alkanes` | `Alkanes` or `Fames`; what the index is calibrated on |
| `Alignment index type` | `RT` | `RT` or `RI`; what alignment uses |
| `RI index file pathes` | | a tab-separated table: raw file path, RI dictionary path — one line per file; the dictionary itself is two columns, carbon number and retention time. Required, for every file, when `RI` is used |

## Alignment

| Key | Default | Meaning |
| --- | --- | --- |
| `Alignment reference file ID` | `0` | the injection the others are aligned to, by its position in the batch (0 is the first) |
| `Retention time tolerance for alignment` | `0.1` | minutes; how far apart two peaks may sit and still be one feature — also five times this is the expected window the review grid shades |
| `MS1 tolerance for alignment` | `0.015` | Da |
| `Retention time factor for alignment` | `0.5` | the weight of retention time in the matching score |
| `MS1 factor for alignment` | `0.5` | the weight of m/z |
| `Peak count filter` | `0` | per cent; a feature detected in fewer injections than this is dropped |
| `N percent detected in one group` | `0` | per cent; a feature must be detected in at least this share of one class |
| `Gap filling by compulsion` | `True` | integrate the chromatogram where no peak was detected, and mark the value gap-filled |
| `Together with alignment` | `True` | align after peak picking; `False` stops after the per-file results |

## Export and process

| Key | Default | Meaning |
| --- | --- | --- |
| `Is height matrix export` | `True` | write the `.qa.tsv` QA matrix |
| `Number of threads` | the machine's cores, at least 2 | files are processed in parallel, half as many at a time as this |

## A complete file

```
#Data type
MS1 data type: Centroid
MS2 data type: Centroid
Ion mode: Positive
Target omics: Metabolomics
Acquisition type: DDA
Machine category: LCMS

#Data collection parameters
Retention time begin: 0.0
Retention time end: 100.0
MS1 mass range begin: 50.0
MS1 mass range end: 1500.0
MS2 mass range begin: 50.0
MS2 mass range end: 1500.0

#Centroid parameters
MS1 tolerance for centroid: 0.01
MS2 tolerance for centroid: 0.025

#Peak detection parameters
Smoothing method: LinearWeightedMovingAverage
Smoothing level: 3
Minimum peak width: 5
Minimum peak height: 1000
Mass slice width: 0.1
Mass accuracy: 0.01
Max charge number: 2

#Deconvolution parameters
Sigma window value: 0.5
Amplitude cut off: 0.0
Keep isotope range: 0.5
Exclude after precursor: True

#Adduct list
Searched adduct ions: [M+H]+,[M+Na]+,[M+NH4]+

#MSP file and MS/MS identification setting
MSP file path: /data/libraries/Pos_GLDB_v0-1-0-alpha.msp
RT tolerance for MSP-based annotation: 0.5
Mass range begin for MSP-based annotation: 0.0
Mass range end for MSP-based annotation: 2000.0
Relative amplitude cutoff for MSP-based annotation: 0.0
Absolute amplitude cutoff for MSP-based annotation: 0.0
Weighted dot product cutoff for MSP-based annotation: 0.4
Simple dot product cutoff for MSP-based annotation: 0.4
Reverse dot product cutoff for MSP-based annotation: 0.4
Matched peaks percentage cutoff for MSP-based annotation: 0.2
Minimum spectrum match for MSP-based annotation: 1.0
Total score cutoff for MSP-based annotation: 60.0
MS1 tolerance for MSP-based annotation: 0.01
MS2 tolerance for MSP-based annotation: 0.05
Use retention information for MSP-based annotation scoring: True
Use retention information for MSP-based annotation filtering: False
Only report top hit for MSP-based annotation: True

#Alignment parameters setting
Alignment reference file ID: 0
Retention time tolerance for alignment: 0.1
MS1 tolerance for alignment: 0.015
Retention time factor for alignment: 0.5
MS1 factor for alignment: 0.5
Peak count filter: 0.0
N percent detected in one group: 0.0
Gap filling by compulsion: True
Together with alignment: True

#Export
Is height matrix export: True

#Process
Number of threads: 8
```
