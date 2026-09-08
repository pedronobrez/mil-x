---
title: The Statistics workspace
section: Workspaces
order: 16
summary: The dataset seen whole — principal components, drift correction against the controls, the discriminant and orthogonal models, clustering, and the molecular network.
---

# The Statistics workspace

Review works one feature at a time. This workspace is the other half: the dataset seen whole, so a
mislabelled injection, a batch that drifted or a class that fragments alike shows up before the
per-feature work starts. `⌘5` shows it; it needs a result with at least two injections. The
methods behind each view are in [[algorithms]].

## The options band

One band drives every reading on the page, because they are all readings of the same matrix:

| Option | What it changes |
| --- | --- |
| **Value** | peak height or peak area |
| **Transform** | log10, the usual choice, or none |
| **Scaling** | **Auto (unit variance)** gives every feature the same weight; **Pareto** is between; **None (centre only)** lets the abundant ones lead |
| **Annotated only** | drop the unknowns before computing |
| **Drift corrected** | feed every view the values corrected against the quality controls; appears once a correction has been run |
| **Recompute** | the components and the clustering again; they also recompute when an option changes |

Missing and non-positive values become the smallest positive value of their own feature, which is
what a log transform needs and what most pipelines do. The summary on the right says what was
computed: `2235 features across 8 injections · log10 transform · auto (unit variance)`.

## Principal components

![principal components](images/statistics-pca.png)

Scores on the left, one point per injection, coloured by class: replicates of a class should sit
together and the classes apart, and an injection that lands among the wrong group or far from
everything is the first to check. Loadings on the right, one point per feature, coloured by lipid
class: a feature far from the centre in the direction that separates the groups is what drives
them apart. The table gives the variance each component carries and the running total.

## Drift correction

![the drift correction](images/statistics-drift.png)

The instrument's response falls as the source fouls and jumps between batches, so the same
compound is not the same number at injection 5 and injection 80. The quality controls are the same
material every time, so whatever they do across the sequence is the instrument and not the
biology: fit that, divide it out, and what is left is comparable. This is the locally weighted
correction the metabolomics literature calls QC-RLSC.

**Correct against the controls** runs it, with the **Smoothing span** — the share of the controls
each local fit sees: wide follows the slow fall, narrow follows every wobble including the noise
(0.75 by default; 0.2 to 1.0). Each batch is corrected on its own. Where a batch has three or more
controls the drift inside it is traced with a tricube local linear fit; where it has only one or
two, there is not enough to trace a trend but enough to say what the batch was running at, so it is
levelled to the others with a single factor.

It is measured by what it is for. The table gives every feature's coefficient of variation over the
controls before and after, worst first; picking a row draws that feature's response across the
run, controls apart from samples, before above and after below. A control trace that falls away or
steps at a batch boundary is drift; after correcting it should be flat, and the samples should
have moved with it. A feature whose spread does not improve is one the controls could not speak
for. The line under the table says how many controls and batches were used and the median CV
before and after.

Nothing is written to the result. The corrected values live in the workspace and reach the other
views only while **Drift corrected** is ticked, so the correction is always something you can take
back. What it needs is in the [[samples-workspace]]: the injections marked `QC`, the injection
order, and the batch. Without at least three controls it corrects nothing and says so.

## Discriminant

![the discriminant model](images/statistics-discriminant.png)

The supervised counterpart of the principal components: it asks which features separate the
classes you declared, rather than which ones carry the most variance. Partial least squares
discriminant analysis (PLS-DA), fitted by NIPALS. **Fit against the classes** runs it, with the
number of **Components** (2 by default, 1 to 5) and the number of **Permutations** for the test
(200 by default; 0 skips it).

With a handful of injections it will separate anything, including noise, so the fit is never
reported on its own:

| Number | What it says |
| --- | --- |
| **R²Y** | how much of the class membership the model reproduces on the injections it was fitted to |
| **Q²** | how much it reproduces on an injection it never saw, by leave-one-out cross-validation |
| **permutation p** | how often shuffled labels did as well, over the shuffles you asked for |

A high R²Y with a low Q² is a model that has memorised its own samples. The line under the plot is
the verdict rather than the fit: it says the separation *survives cross-validation* only when Q² is
at least 0.4 and p at most 0.05, and otherwise says plainly to read the plot as a picture of this
batch and not as a finding. It will say so on labels with nothing behind them. Centring and scaling
are redone inside every cross-validation fold from the training injections alone, because doing it
once over the whole matrix lets the held-out injection speak for its own prediction and hands noise
a respectable Q².

The table ranks the features by **VIP**, the usual measure of how much of the separation rests on
each one; above one is the customary mark of a feature that matters. With exactly two classes,
**Higher in** names the class the feature goes with. Clicking a row opens that feature in the ion
table.

The permutation test refits the model a few hundred times over, which on a couple of thousand
features would take minutes done naively. It does not: the features are reduced once to the
products between the injections, and every refit works in the space the injections span, so two
thousand features and eight injections fit and permute in under half a second.

## Orthogonal

![the orthogonal model](images/statistics-orthogonal.png)

The same question as the discriminant model, in a tidier shape (OPLS-DA). A plain model spreads
the separation across every component it fits, mixed in with variation that has nothing to do with
the classes — the run order, the extraction, the animal. This one strips that out first, one
component at a time, and what is left is a single predictive component carrying the whole
separation. The score plot then reads directly: left to right is the difference between your
classes, up and down is everything else that was in the way.

Two classes only, because the predictive part is one direction and a direction has two ends. With
three or more it says so and sends you to the plain model.

**Fit the orthogonal model** runs it. **Orthogonal components** is how many rounds of stripping to
do (1 by default; 0 to 5). One is the usual answer. More is worth trying when the injections still spread along the vertical axis, and
worth suspecting when it starts improving the fit without improving the Q²: at that point it is
removing the signal along with the nuisance.

**The S-plot** on the right is the point of the whole rotation. Across is how much of the
separation each feature carries (its covariance with the predictive component), up is how
consistently it carries it (its correlation), and only the two far corners are worth acting on. A
feature high up but near the middle is reliable and tiny; one far out but low down is one loud
injection pretending to be a difference. Clicking a point, or a row of the table under it, opens
that feature in the ion table. The table lists the corners in order, with the class each feature
is higher in, its covariance, its correlation and its VIP.

The rotation is a way of looking, not a result. It fits no better than the plain model and it is
held to the same two numbers, Q² and the permutation p, printed in the same place. A model that
does not survive them does not survive them rotated.

On the eight-injection run in the validation data, liver against blank gives R²Y 0.98 and Q² 0.91
at p 0.020, with 42 % of the features on the predictive component and 29 % stripped out; the
corners of the S-plot are free fatty acids at about 1.5 min, which is what separates a real liver
extract from a blank. The same labels shuffled give Q² −1.55 and p 0.81.

## Clustering

![hierarchical clustering](images/statistics-clustering.png)

Average linkage over one minus the Pearson correlation of the injection profiles. Two injections
join at the height where their profiles stop agreeing, so replicates should join low and the
blanks should hang off on their own. One that joins the wrong group is a mix-up or a bad run.

## Molecular network

![the molecular network](images/statistics-network.png)

Features whose deconvoluted product spectra look alike are joined, by the intensity-weighted cosine
over matched fragments that MS-DIAL and GNPS both use. One lipid class fragments the same way and
forms a cluster, so a feature named as one class sitting inside another's cluster is worth a second
look, and an unknown next to a named cluster is a candidate for the same family. Clicking a node
opens that feature in the ion table.

**Similarity ≥** is the cut-off for an edge (0.7 by default) and **fragment tol.** the mass
tolerance for two fragments to count as the same (0.05 Da). Features joined to nothing are counted
but not drawn: with a few thousand of them the picture is all singletons and no clusters. **Show
unlinked** puts them back. **Export for Cytoscape…** writes the edge table and, beside it, a
`_nodes` table; see [[exports]].

Building the network reads every deconvoluted spectrum, so it is a button — **Build network** —
rather than something that happens on every option change. Only features with MS/MS take part.

## What is not here

Multi-block and multi-level designs — paired samples, repeated measures, several kinds of
measurement on the same subjects. Everything above assumes one table and independent injections.
