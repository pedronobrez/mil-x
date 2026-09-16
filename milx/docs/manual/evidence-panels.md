---
title: The evidence panels
section: Reviewing
order: 22
summary: The peak grid that is always on screen and the nine tabs below it — MS/MS, Isotopes, Candidates, Abundance, Feature map, Samples, Statistics, Trend, Other polarity — and the question each one answers.
---

# The evidence panels

Nine views, each answering one question a reviewer actually asks about the selected feature. The
first, the peak in every sample, is a panel of its own and is always on screen; the other eight
are tabs below it, with the product spectrum showing by default, so a result opens on the
chromatogram and the spectrum together. The divider between the two is draggable.

## Peaks

The chromatographic peak in every injection at once, one panel per injection, in a grid. Does the
feature exist, is it integrated the same way everywhere, is it aligned.

Each panel draws the extracted ion chromatogram of the feature's m/z at the method's centroid
tolerance, with the expected retention window shaded lightly (the feature's retention time ± five
times the alignment tolerance), the peak's own integration window shaded warmly, and a marker at
the apex. The title reads `sample · 1.23E5 · S/N 40`, with `not detected` for a gap-filled or
missing peak and `gap-filled` when it was. The selected injection's panel is framed in the accent
colour.

| Control | What it does |
| --- | --- |
| **Grid** `3 × 2` | columns and rows of the grid; pages when there are more injections than cells, with **◀** `1/2` **▶** |
| **Same Y** | one intensity scale for every panel, so a weak injection looks weak |
| **Link X** | one retention window for every panel |
| zoom mode | **Expected window** (around the feature's retention time), **Peak** (each injection's own integration window with a margin), **Full trace** |
| **Magnify** | enlarges the selected panel over the tabs; double-clicking a panel does the same, **Close** puts it back |

Clicking a panel selects its injection (in the Samples tab and the Trend plot too); `⇧`-dragging
across one draws an integration window — see [[reintegration]] for the strip above the grid. The
panels are loaded one injection at a time from the survey-scan cache; a thin bar shows while they
are still coming.

## MS/MS

The deconvoluted product spectrum of the representative injection, mirrored against the library
reference when the feature is annotated: measured up, library down, with the precursor marked and
the most intense peaks labelled. Beside it, **match scores** — the numbers behind the reported
annotation, in the order a reviewer reads them:

| Score | Meaning |
| --- | --- |
| **Total score** | MS-DIAL's combined score, the one the cut-off applies to |
| **Dot product** | the weighted dot product between measured and reference spectra |
| **Reverse dot product** | the same, counting only the reference's peaks — how much of the library spectrum was accounted for |
| **Simple dot product** | the unweighted one |
| **Matched peaks** and **%** | how many reference peaks were found, and what share |
| **Mass similarity** | how close the precursor mass was |
| **RT similarity** | when the library carried a retention time and the method used it |
| **Spectrum match** | yes or no: whether the spectrum passed the cut-offs at all |
| **Lipid evidence** | class, chains, or sn-position: which level of lipid structure the fragments supported |

The library reference comes from the project's own library when it is loaded, otherwise from the
MSP file the method names, read once per session. See [[annotation]].

## Isotopes

The MS1 isotope envelope of the representative injection, with the monoisotopic ion first and the
monoisotopic percentage in the title. An envelope that does not fit the formula, or a monoisotopic
ion that is not the base peak, is the signature of an isotope or adduct of another feature — tag
it Overannotation. `No MS1 isotope pattern stored` means the run kept none for this feature.

## Candidates

Every library match the run kept for the feature, best first, with the full score breakdown:
Candidate, Total, Dot, Reverse, Matched, Matched %, Mass sim., and ticks for **Class**, **Chains**
and **sn** evidence. The one the run reported is ticked **Reported**. **Use this annotation**
replaces the name with the selected candidate's; **Back to the automatic name** undoes that.

![the candidate list](images/review-candidates.png)

When the right compound is not among them, the **search library** strip asks the library again for
this feature with tolerances you choose — **MS1** precursor tolerance in Da, **MS2** fragment
tolerance in Da, and optionally **RT** with its tolerance in minutes — and with no score cut-off at
all, because a reviewer is looking for what exists rather than for what passes. **Search** runs it;
**Back to the run's matches** returns to the stored list. Without a product spectrum every record
at the same mass ties on score, so the closest mass decides the order. The search runs the same
annotator the pipeline used, so a score here means what a score there means. See
[[annotation#Searching the library again]].

![searching the library again](images/review-search.png)

## Abundance

One bar per injection, coloured by class. A feature as high in the blanks as in the samples is
background; one that scatters across the quality-control injections is not quantifiable.

## Feature map

Every filtered feature as a dot in retention time against m/z, coloured by class; the selected
feature is marked. Filter to one class and its members fall on a line: on a reversed-phase column
retention rises with acyl carbon number and falls with each double bond. A member off that line is
the first to re-examine. Clicking a dot selects that feature in the ion table.

![the feature map](images/review-feature-map.png)

## Samples

The per-injection numbers: **#** order, **Sample**, **Class**, **Type**, **RT**, **m/z**,
**Height**, **Area**, **S/N**, **Start** and **End** of the integration window, and
**Gap-filled**. Selecting a row selects that injection everywhere — its panel in the grid, its
point in the trend — and makes it the sample in focus for **This sample** in the integrate strip.

## Statistics

Per class: **n**, **Mean height**, **SD**, **% CV**, **Min**, **Max**, **Mean area**. A CV over
the QC injections of more than 30 % is the usual mark of a feature that will not quantify.

## Trend

Any per-injection metric against another, coloured by class, points joined in order: **Y** is
Height, Area, RT, m/z or S/N; **X** is the analytical order or any of the same. Height against
order is the drift check for one feature; the drift correction in the [[statistics-workspace]] is
the same check for all of them. Clicking a point selects the injection.

## Other polarity

The same compound as the other run of the batch measured it: this run's product spectrum above,
the other run's below. Empty until a polarity is linked from the toolbar and a feature both runs
saw is selected.

The two halves are not a match to be scored — a protonated molecule and a deprotonated one
fragment differently. What agrees is the neutral mass, the retention time and the height profile
across the injections, and the line under the mirror says all three, with the partner's name, m/z,
adduct, signal-to-noise and which run quantifies the compound. See [[polarity-merge]].

With **Split evidence** on, MS/MS on the left and this tab on the right is the layout linking a
polarity is for; the second column selects it on its own the first time.
