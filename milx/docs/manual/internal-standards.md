---
title: Internal standards and relative abundance
section: Workspaces
order: 18
summary: How the confirmed analytes become area ratios to the internal standard of their class, the dialog that chooses the standard, how the choice is suggested, and where it is kept.
---

# Internal standards and relative abundance

A lipidomics result is not reported in peak areas. Each analyte is reported as the ratio of its
area to the area of an internal standard of the same class, spiked into every sample at the same
amount before extraction, so that whatever happened to the sample — the extraction yield, the
dilution, the drift, the ion suppression at that retention time — happened to the standard too and
divides out. That ratio is the *relative abundance*, and it is the default source of the
[[statistics-workspace]]: **Confirmed analytes, as ratios to a standard**.

Two things go into it, and both come from the review. The analytes are the features tagged
**Confirmed** in the [[analytics-workspace]] — nothing unreviewed goes into a ratio, which is the
point of reviewing. The standard of each class is chosen in the dialog below, from the confirmed
features of that class, so a standard has to be confirmed like any other feature before it can
be used.

## The dialog

![the standards dialog](images/standards-dialog.png)

**Internal standards…** in the band opens it. One row per lipid class with at least one confirmed
analyte, showing how many, and a list to choose the standard from: the class's own candidates
first, best first — a **standard of this class** (a deuterated or otherwise labelled compound: the
name says `d7`, `d9`, `13C`, `(d7)`, `-d5`, `IS`), then an **odd chain, this class** (a 15:0,
17:0 or 19:0 chain, which does not occur naturally in most tissues and is the usual unlabelled
standard), then the **standards of other classes**, then every other confirmed feature by name —
with each candidate's retention time, m/z and mean height so it can be told from its neighbours. A
class set to **none — keep the raw area** goes through as areas, and the summary in the band says
so (`raw: CAR, EtherPC`).

**Suggest again** puts the suggested standard back in every class; **Clear all** sets every class
to none; **Use these standards** applies the choice and rebuilds the dataset; **Cancel** leaves
things as they were. The suggestion is made when the result is loaded, so a result whose review
confirmed the standards opens with the ratios already computed.

## What the suggestion does

For each class, the confirmed feature with the highest score: 100 for a labelled standard of the
class, 40 for an odd-chain species of the class, and nothing otherwise — a standard of another
class scores 20 and is offered but never suggested, because dividing PE by a PC standard is a
choice to make on purpose. The standard itself is left out of the ratios (its ratio to itself is
one), so a class with a standard and five analytes gives five ratios. On the validation review of
the liver batch the odd-chain rule picked `PC 33:1`, `LPC 19:1`, `SM 18:1;O2/23:0`, `PE 17:0_17:0`,
`Cer d18:1/17:0` and `LPE 13:0`, which is what was spiked.

## Where the choice is kept

In the review's sidecar beside the alignment file (`<alignment>_curation.json`, described in
[[projects-and-files#The curation sidecar]]), under `InternalStandards`, keyed by class, saved with the rest of the
review. Reopen the project and the ratios come back as they were. The choice is part of the review
in the same sense the tags are: change it and the review is dirty until it is saved.

## When the review changes

The analysis takes the confirmed set when it is loaded. Confirm or reject a feature afterwards and
the band shows **Reload from the review**; press it and the confirmed set, the standards and the
dataset are taken again. It is a button rather than automatic because the tests and the models
downstream take a moment on a large dataset, and a reviewer tagging through a list does not want
the statistics recomputed on every keystroke.

## What to check

- Every class you care about has a standard, or you have decided it should not. The band lists the classes going through raw.
- The standard's own peak is good in every injection: open it in the ion table and look at the review grid. A standard that is missing or gap-filled in one injection makes every ratio in that class wrong for that injection, and nothing downstream can tell.
- The standards do not differ between the classes being compared: on the **Statistical test** page with **Confirmed analytes, raw** as the source, the standards should have a fold change near one. If one does not, the spiking or the extraction differed, and the ratios are correcting for it — which is what they are for, but it is worth knowing.
