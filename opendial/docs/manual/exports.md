---
title: Exports
section: Reviewing
order: 25
summary: Every way a result leaves the application — the reviewed table, the re-exported matrix, the OpenQuant component list, the network tables, and what the run itself writes.
---

# Exports

## The reviewed table

**Export reviewed table…** on the Analytics toolbar writes the features the filter is showing, in
table order, as a tab-separated text file, with the review as columns of its own. MS-DIAL's own
alignment export has no tag column — it folds the tags into the free-text comment and cannot
express *reviewed* at all — so this is the file to take to a spreadsheet or a statistics script.

The columns: `Alignment ID`, `Average RT(min)`, `Average m/z`, `Metabolite name` (the hand-picked
one when there is one), `Annotation level` (confident, suggested, m/z only, or blank),
`Adduct`, `Ontology`, `Formula`, `INCHIKEY`, `Isotope` (`M`, `M+1`, …), `Fill %`, `MS/MS assigned`,
`S/N average`, `Total score`, `Representative file`, `Reviewed`, `Tags` (semicolon-separated
labels), `Comment`, `Manually annotated`, `Manually quantified`, and then one column per injection
holding its peak height. The second line names each injection's class, as MS-DIAL's matrices do.
Numbers are written with a period as the decimal mark whatever the machine's locale.

The suggested file name is `<project>_reviewed.txt`, in the results folder.

## The alignment matrix

**Process ▸ Re-export alignment matrix…** (also **Re-export…** on the toolbar) writes every
feature, filter or no filter, with its identity — `Alignment ID`, `Name`, `RT (min)`, `m/z`,
`Adduct`, `Formula`, `Ontology`, `Score`, `Fill %` — followed by one height column and one area
column per injection, as TSV. It is the quickest route to a full matrix without MS-DIAL's
statistics columns.

## The OpenQuant component list

**Export to OpenQuant…** turns the listed features into an OpenQuant component CSV — precursor,
strongest fragment, retention window — so a discovery list becomes a targeted method in one step.
The options and the columns are in [[openquant]].

## The network tables

**Export for Cytoscape…** in the Statistics workspace writes the molecular network's edge table
(source id, target id, similarity, mass difference) to the chosen file and the node table (id,
name, class, retention time, m/z, height, annotated) beside it with a `_nodes` suffix, both
tab-separated, ready for Cytoscape's import.

## What the run writes

Every run writes MS-DIAL's own exports into the results folder without being asked:

| File | What it is |
| --- | --- |
| `<sample>.mdpeak` | the peak table of one injection: every detected peak with its retention time, m/z, height, area, adduct, isotope, annotation and scores |
| `<sample>.mdmsp` | the deconvoluted MS/MS spectra of that injection, in MSP format |
| `AlignResult-<stamp>.mdalign` | the alignment table: every feature with its identity, the representative MS/MS, and the height of each injection, with per-class mean and standard deviation |
| `AlignResult-<stamp>.qa.tsv` | the QA matrix in long format — height, RT, m/z, S/N, MS/MS and reference-matched flags per feature and injection; off when the method's *Export QA height matrix* is unticked |
| `AlignResult-<stamp>.mdmsp` | the representative MS/MS of every feature |
| `AlignResult-<stamp>.mzTab` | the result as mzTab-M |
| `opendial_method.txt` | the method the run used |
| `Project-<stamp>.mdproject` + `.mddata` | the MS-DIAL project |

GC-MS runs write `<sample>.mdscan` scan tables instead of peak tables. Every file is listed in
[[projects-and-files#The results folder]].
