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

## One compound, several ions

An untargeted run does not report compounds, it reports ions: the protonated molecule, its sodium
and ammonium adducts, its isotopes, whatever fell apart in the source, each as a feature of its own.
Two and a half thousand features are a few hundred compounds seen several times over.

Three things have to agree before two features are called the same compound. They elute together
(within a tenth of the alignment's own tolerance), their heights rise and fall together across the
injections (a correlation of at least 0.8 — adducts of one molecule track each other because they
are one molecule), and the distance between their masses is one an adduct pair, an isotope, a dimer
or a loss of water or ammonia gives. Correlation alone groups a crowded region; mass alone groups
coincidences.

The ion the run measured best represents the compound; the rest carry **Ion of** in the table,
saying what they are and of which feature. **One row per compound** in the filter band hides them.
Nothing is deleted and nothing is merged: the heights stay per ion, and the grouping is a reading of
the table, not a change to it.

## Why so few features are named

A run that names three hundred of two and a half thousand features has usually not lost its
spectra; it has been scored against a library that does not fit it. The eight liver injections are
the worked example, with the alpha lipid library of 449 627 records:

| | features | with a product spectrum | named from MS/MS | named at all |
| --- | --- | --- | --- | --- |
| metabolomics, retention time in the score | 2665 | 2221 | 79 | 286 |
| lipidomics, retention time in the score | 2665 | 2221 | 216 | 286 |
| lipidomics, retention time out of the score | 2667 | 2224 | 858 | 1198 |

Read it from the top. **Five features in six carry a product spectrum**, so the deconvolution is
not the ceiling; what changes the count is how the spectrum is scored.

**The omics target.** `Target omics: Lipidomics` switches on MS-DIAL's lipid rules — the class from
the head-group fragments, the chains from the acyl losses — and nearly triples the confident names
on a lipid library without touching anything else. On a lipid project it is not optional.

**The library's retention times against yours.** Every record of that library carries a retention
time, and they lie between ten and eighteen minutes: the gradient the library was built for. These
injections elute between 0.3 and 8 minutes. With `Use retention information for MSP-based
annotation scoring` on, the retention term of every candidate is therefore near zero and drags the
total under the cut-off, so a correct spectral match is thrown away for eluting at the "wrong"
time. Turning it off — the **Use RT for scoring** box in the [[method-workspace]] — takes the names
from 286 to 1198. Leave it on only when the library's retention times were measured on the method
being run; then it is a real filter, and one of the few that separate isomers.

**What is left after that** is the library itself: a record has to exist, at the right adduct, with
enough fragments to score. That library is `[M+H]+`, `[M+Na]+` and `[M+NH4]+` only — which is what
the method searches — and its in-silico records carry a median of five peaks, so the matched-peak
percentage and the dot products are being computed over very little. Lowering `Total score cutoff`
buys more names of lower confidence; the ion table's **Level** column and the **Candidates** tab are
where that trade-off is judged, feature by feature.

## Why two runs can disagree

Peak detection, MS/MS assignment and deconvolution do not depend on the library and agree between
platforms. Annotation depends on it entirely. A project on Windows that registered its library as
`POS_GLDB_260406_2` and a run here against `Pos_GLDB_v0-1-0-alpha.msp` are not comparable by
name: two out of five of the Windows annotations sit at masses the alpha library has no record
within 0.01 Da of. `ResultCompare --check-library <project> <library.msp>` asks whether a library
could have produced a run's annotations at all; see [[command-line]]. To compare annotation, point
both at the same library build.
