---
title: Annotation, scores and the library
section: Reviewing
order: 24
summary: How MS-DIAL names a feature, what the levels and scores mean, how lipid names are built from fragments, searching the library again, hand-picking a name, and why two runs can disagree.
---

# Annotation, scores and the library

## How a feature gets its name

During processing, every peak's deconvoluted product spectrum is scored against every library
record within the MS1 tolerance of its precursor (0.01 Da by default). The score combines the
weighted dot product, the reverse dot product, the matched-peak percentage, the mass similarity
and — when the method says so — the retention-time similarity. A record passes when every cut-off
in the method is met: total score (60 % by default), weighted, simple and reverse dot products
(0.4), matched-peak percentage (0.2), minimum spectrum matches (1). The best passing record is the
annotation; the others are kept as **candidates**. With **Only report top hit** off, more are kept.
Every threshold is in [[method-parameters#Identification]].

A peak with no product spectrum can still be named on precursor mass alone, as `no MS2: name`; a
match below the total-score cut-off is kept as `low score: name`. The ion table reads these
prefixes back as the **level** — confident, suggested, m/z only — and the annotation filter uses
them. See [[concepts#Annotation levels]].

An aligned feature takes its name from its **representative** injection: the one whose peak scored
best. So the same compound can carry a slightly different name in two runs when a different
injection won.

## Lipid names

The name a lipid feature carries is not the library record's name. MS-DIAL rewrites it from the
fragments it observed, so one record becomes `LPC 16:0` at species level, `LPC 16:0/0:0` once a
chain is supported, and a full sn-position only when the fragments say so. The **Lipid evidence**
line in the MS/MS scores says which level was reached, and the candidate table ticks **Class**,
**Chains** and **sn**. Coming from Windows: OpenDIAL runs the same code and agrees — of 1 462
features shared between a Windows run and a macOS run of the same data, three carry a different
name while both sides scored the same record, and those three flip in both directions.

## The libraries

The method names up to three:

- an **MSP spectral library** — the main one; `.msp`, `.msp2` or `.lbm2`;
- a **text library** of retention time and m/z, for annotation by mass and time alone;
- an **LBM** lipid library, when the method file names one.

The MSP is loaded once per run and, for the review, once per session: it is what the MS/MS mirror
draws the reference from and what the library search reads. When the project carries the library
inside it (an `.mdproject` does), that copy is used; otherwise the file the method names is read,
so the path has to still exist. A run with no library leaves every feature unknown.

## Searching the library again

The run keeps only its best few matches. When the right compound is not among them the Candidates
tab asks the library again for this feature, with your own tolerances — **MS1** in Da, **MS2** in
Da, optionally **RT** in minutes — and no score cut-off at all, because a reviewer is looking for
what exists rather than for what passes. The search runs the same annotator the pipeline used, so a
score here means what a score there means; without a product spectrum every record at the same
mass ties, and the closest mass decides. **Back to the run's matches** returns to the stored list.
Widening MS1 to 0.05 Da is the usual first move; widening MS2 helps a noisy spectrum.

## Hand-picking a name

**Use this annotation** takes the selected candidate — from the run's list or from a search — as
the feature's name. It replaces the name in the ion table, the exports and the statistics, shows
a **hand-picked** chip, and is written to `<alignment>_curation.json` on **Save review**. The
run's own annotation is not lost: **Back to the automatic name** restores it. The reviewed table
marks such features as *Manually annotated*.

## Why two runs can disagree

Peak detection, MS/MS assignment and deconvolution do not depend on the library and agree between
platforms. Annotation depends on it entirely. A project on Windows that registered its library as
`POS_GLDB_260406_2` and a run here against `Pos_GLDB_v0-1-0-alpha.msp` are not comparable by
name: two out of five of the Windows annotations sit at masses the alpha library has no record
within 0.01 Da of. `ResultCompare --check-library <project> <library.msp>` asks whether a library
could have produced a run's annotations at all; see [[command-line]]. To compare annotation, point
both at the same library build.
