---
title: Exports
section: Reviewing
order: 25
summary: Every way a result leaves the application — the reviewed table, the re-exported matrix, the OpenQuant component list, the analysis tables and the charts, the network tables, and what the run itself writes.
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

## The analysis tables

Every page of the [[one-factor-analysis]] that produces a table has an **Export table…** button
beside its options, and the **Data processing** page has **Export normalised data…**. All of them
write tab-separated text with a header line, numbers with a period as the decimal mark, into a
file named for the table:

| File | Columns |
| --- | --- |
| `normalized-data.tsv` | `Feature id`, `Feature`, `Class`, then one column per injection with the transformed value — the dataset every page reads, ready for another tool |
| `comparison.tsv` | `Feature id`, `Feature`, `Class`, the two class means, `Fold change`, `log2 FC`, `Statistic`, `p`, and the adjusted p under the name of the adjustment (`FDR`, `Holm`, `Bonferroni`) |
| `anova.tsv` | `Feature id`, `Feature`, `Class`, `Statistic`, `p`, the adjusted p, and `Post hoc` — every pair with its p, semicolon-separated |
| `pattern-search.tsv` | `Feature id`, `Feature`, `Class`, `Correlation`, `p` |
| `random-forest.tsv` | `Feature id`, `Feature`, `Class`, `Mean decrease accuracy`, `Mean decrease Gini` |
| `enrichment.tsv` | `Set`, `Kind`, `Size`, `Hits`, `Expected`, `Enrichment ratio`, `p`, the adjusted p, and `Members` — the significant features in the set, semicolon-separated |
| `reactions.tsv` | `Reaction` (the table's id), `Reactant`, `Product`, the log2 change of the weight (first class over second), `p`, `Z`, `Status`, `Genes`, `Enzyme` — from the [[pathways]] page |
| `pathways.tsv` | `Pathway` (the chain), `Reactions` (its length), `Z`, `Status`, `Genes` |
| `predicted-reactions.tsv` | `Reaction`, `Measured`, `Not measured`, `Genes` — the reactions one more confirmed class would open up |

`Feature id` is MS-DIAL's alignment ID, the same number as in the reviewed table and the ion
table, so the tables join. The internal standards themselves are not in the ratio tables.

## The charts

Every chart in the Statistics workspace writes itself out as **SVG** or as **PNG** at two, four
or six times its on-screen size, from the **Export…** menu at its top right; see [[chart-export]].

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
