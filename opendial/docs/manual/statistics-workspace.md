---
title: The Statistics workspace
section: Workspaces
order: 16
summary: The dataset seen whole — eighteen pages down the left, from the data and its preprocessing through the tests, the volcano, the models, the clustering and the enrichment, every one reading the same dataset.
---

# The Statistics workspace

Review works one feature at a time. This workspace is the other half: the dataset seen whole, so
a mislabelled injection, a batch that drifted, a class of lipids that rose together or an analyte
that separates the groups shows up in one place. `⌘5` shows it; it needs a result with at least
two injections. The pages follow MetaboAnalyst's *Statistical Analysis (one factor)* module,
which is what most people in the field will have used before, with the same names for the same
things wherever that was possible, and with the lipid-specific parts — the internal standards, the
enrichment over classes and chains — added where a lipidomics result needs them. The methods
behind each page are in [[algorithms]]; the charts, their options and their export are in
[[chart-export]].

## One dataset, many pages

Every page reads the same dataset, and the dataset is built on the first page, **Data
processing** — which features, as what numbers, preprocessed how. Change the source, the
normalisation or the transformation there and every page changes with it; there is no page whose
numbers came from a different table. The band across the top says what the dataset is right now:

| Control | What it does |
| --- | --- |
| **Source** | what the features are: **Confirmed analytes, as ratios to a standard** (the lipidomics default, see [[internal-standards]]), **Confirmed analytes, raw**, or **Every feature** (the untargeted view) |
| **Internal standards…** | the dialog that chooses the standard each class is divided by |
| **Area** | peak area rather than peak height |
| **Annotated only** | when every feature is the source, drop the unknowns |
| **Drift corrected** | feed every page the values corrected against the quality controls; appears once a correction has been run on the **Drift correction** page |
| **Reload from the review** | appears when the review changed after the analysis was loaded — a feature confirmed or rejected since — and takes the confirmed set again |

A result with no feature tagged **Confirmed** opens on **Every feature**; confirm some in the
[[analytics-workspace]] and reload, and the ratio view becomes available. The summary on the right
says what came through: `61 features across 6 injections · area ratio to the class standard · log10`.

## The pages

They are listed down the left, in the order the analysis is usually read:

| Heading | Page | What it answers |
| --- | --- | --- |
| Data | **Data processing** | what the dataset is, and how it is cleaned, normalised, transformed and scaled |
| | **Normalisation check** | did the normalisation line the injections up, did the transformation make the features spread alike |
| One feature at a time | **Fold change** | how much each feature changed between two classes |
| | **Statistical test** | which features differ between two classes, and how surely |
| | **Volcano plot** | both at once: size of the change across, certainty up |
| | **ANOVA** | which features differ across three or more classes, and between which pairs |
| | **Correlations** | which features move together, or which injections look alike |
| | **Pattern search** | which features follow a given profile, or a given order of the classes |
| Models | **Principal components** | do the replicates sit together and the classes apart, without being told the classes |
| | **Discriminant** | which features separate the classes you declared, and whether that separation survives cross-validation |
| | **Orthogonal** | the same separation on one axis, with the S-plot |
| | **Random forest** | a non-linear ranking of the features, with its own out-of-bag error |
| Clustering | **Dendrogram** | which injections join, and at what height |
| | **Heatmap** | the top features against every injection, both clustered |
| | **K-means** | the injections partitioned into *k* groups without their labels |
| Enrichment | **Lipid enrichment** | whether the features that changed fall into a lipid class, a chain length or an unsaturation more often than chance |
| Quality | **Drift correction** | the instrument's drift traced on the controls and divided out |
| | **Molecular network** | which features fragment alike |

The pages under *One feature at a time*, *Clustering* (apart from the dendrogram), *Enrichment*
and the random forest are described in [[one-factor-analysis]]. The rest are below.

## Principal components

![principal components](images/statistics-pca.png)

Scores on the left, one point per injection, coloured by class, with the 95 % confidence ellipse
of each class: replicates of a class should sit together and the classes apart, and an injection
that lands among the wrong group or far from everything is the first to check. Loadings on the
right, one point per feature, coloured by lipid class: a feature far from the centre in the
direction that separates the groups is what drives them apart. The ten farthest from the centre
are named; clicking any opens it in the ion table. The two boxes above the scores choose which
components are drawn (**PC1** against **PC2** by default, up to PC5); the scree plot and the table
underneath give the variance each component carries and the running total.

The components are computed on the scaled dataset — auto-scaling by default, so every feature
weighs the same; Pareto or centring only are on the **Data processing** page.

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

The bar chart on the right ranks the features by **VIP**, the usual measure of how much of the
separation rests on each one; above one — the dotted line — is the customary mark of a feature
that matters. Beside each bar, one cell per class shows the feature's mean level in that class,
the way MetaboAnalyst's VIP plot does, so the direction of the change is read without leaving the
page. With exactly two classes, **Higher in** in the table names the class the feature goes
with. Clicking a bar or a row opens that feature in the ion table.

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
do (1 by default; 0 to 5). One is the usual answer. More is worth trying when the injections still
spread along the vertical axis, and worth suspecting when it starts improving the fit without
improving the Q²: at that point it is removing the signal along with the nuisance.

**The S-plot** on the right is the point of the whole rotation. Across is how much of the
separation each feature carries (its covariance with the predictive component), up is how
consistently it carries it (its correlation), and only the two far corners are worth acting on. A
feature high up but near the middle is reliable and tiny; one far out but low down is one loud
injection pretending to be a difference. The eight strongest corners are named. Clicking a point,
or a row of the table under it, opens that feature in the ion table. The table lists the corners
in order, with the class each feature is higher in, its covariance, its correlation and its VIP.

The rotation is a way of looking, not a result. It fits no better than the plain model and it is
held to the same two numbers, Q² and the permutation p, printed in the same place. A model that
does not survive them does not survive them rotated.

On the eight-injection run in the validation data, liver against blank gives R²Y 0.98 and Q² 0.91
at p 0.020, with 42 % of the features on the predictive component and 29 % stripped out; the
corners of the S-plot are free fatty acids at about 1.5 min, which is what separates a real liver
extract from a blank. The same labels shuffled give Q² −1.55 and p 0.81.

## Dendrogram

![hierarchical clustering](images/statistics-clustering.png)

The injections clustered over their whole profiles. Two injections join at the height where their
profiles stop agreeing, so replicates should join low and the blanks should hang off on their own;
one that joins the wrong group is a mix-up or a bad run. **Distance** is what "agreeing" means —
**Euclidean**, **Pearson** (one minus the correlation, the default, which ignores overall
intensity), **Spearman** (the same on ranks) or **Manhattan** — and **Linkage** is how a cluster's
distance to another is taken from its members' — **Average** (the default), **Complete**,
**Single** or **Ward**. The same choices are on the **Heatmap** page, where the features are
clustered too.

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
pages only while **Drift corrected** is ticked in the band, in which case they go in before the
preprocessing, so the ratios, the tests and the models all read them. What it needs is in the
[[samples-workspace]]: the injections marked `QC`, the injection order, and the batch. Without at
least three controls it corrects nothing and says so.

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
rather than something that happens on every option change. Only features with MS/MS take part;
the network reads the result's features directly, not the analysis dataset, so it is the one page
the source does not change, apart from **Annotated only**.

## What is not here

Multi-block and multi-level designs — paired samples beyond the two-class **Paired** test,
repeated measures, several kinds of measurement on the same subjects, and two-factor designs with
an interaction. Everything above assumes one factor and independent injections. Pathway analysis
over reactions rather than sets — LIPID MAPS' BioPAN — is planned and described in [[biopan-plan]].
