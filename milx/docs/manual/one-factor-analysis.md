---
title: One-factor analysis
section: Workspaces
order: 17
summary: The pages of the Statistics workspace that follow MetaboAnalyst's one-factor module — the data processing, the normalisation check, fold change, the tests, the volcano plot, ANOVA, correlations, pattern search, the random forest, the heatmap, k-means and the lipid enrichment.
---

# One-factor analysis

*One factor* means one thing varies between the injections — the class: treated against control,
liver against blank, three doses. These pages of the [[statistics-workspace]] take a dataset with
that one factor through the sequence MetaboAnalyst's one-factor module walks: clean it, normalise
it, transform it, then ask, one feature at a time, which ones changed, and, over all of them at
once, what pattern the change has. Every page reads the one dataset built on the first page, so
nothing here is computed on numbers another page has not seen. The methods are in
[[algorithms#One-factor statistics]].

## Data processing

![the data processing page](images/statistics-data.png)

The page that makes the dataset. The **Source** box at the top says what the features are (see
[[internal-standards]] for the ratios); the two panels below say what is done to them, in the
order it is done:

**Missing values and filter**

| Option | What it does | Default |
| --- | --- | --- |
| **Drop features missing in more than … % of the injections** | a feature absent (no peak, or zero) in more than this share of the injections is dropped before anything else | 50 % |
| **Replace the rest by** | what an absent value becomes: **1/5 of the minimum** of its feature (MetaboAnalyst's default, a stand-in for a value below the limit of detection), **half of the minimum**, the **minimum**, the **mean**, the **median**, the mean of the **K nearest features** by correlation (KNN), or **keep the gaps** and let each method skip them | 1/5 of the minimum |
| **Variance filter** | drop the flattest features, the ones with the least to say: by **interquartile range**, **standard deviation**, **median absolute deviation**, **relative standard deviation** (the noisiest, rather than the flattest), **mean intensity** or **median intensity** (the faintest); or **none** | interquartile range |
| **Removing the bottom … %** | how many to drop; left empty, MetaboAnalyst's rule applies — none under 250 features, 5 % up to 500, 10 % up to 1000, 25 % beyond | empty |
| **Drop features with QC RSD above … %** | a feature whose relative standard deviation over the quality controls is worse than this is not reproducible enough to test; 0 turns it off, and it needs injections typed `QC` in the [[samples-workspace]] | 0 |

**Normalisation, transformation, scaling**

| Option | What it does | Default |
| --- | --- | --- |
| **Sample normalisation** | make the injections comparable to one another: **None**; **Sum** (each injection divided by its total, for a constant total); **Median**; **Probabilistic quotient** (each injection scaled by the median of its ratios to the median profile — PQN, the usual choice for a dilution effect); **Quotient to the controls** (the same, against the mean profile of the QC injections); or **Reference feature** (divide by one feature, chosen in the next box — an internal standard that is not class-specific) | None |
| **Reference feature** | the feature the reference normalisation divides by | — |
| **Transformation** | **Log10** (the usual choice; every value strictly positive after imputation), **Log2**, **Natural log**, **Square root**, **Cube root**, or **None**. A non-positive value that survived is floored at a tenth of the smallest positive one, as MetaboAnalyst does | Log10 |
| **Scaling (models only)** | what the multivariate models see: **Auto (unit variance)** gives every feature the same weight; **Pareto** (divided by the square root of the standard deviation) is between; **Mean centre only** lets the abundant ones lead. The fold changes, the tests and the boxes never see the scaling | Auto |

Ratios to a standard are already normalised, injection by injection, by the standard: the
default source needs no sample normalisation, and it is set to **None** for that reason. The
untargeted view of every feature is where **Probabilistic quotient** or **Sum** earns its place.

**Apply** runs the sequence and refreshes every page; **Apply when the source changes** (on by
default) does it whenever the source, the value or the standards change, so the pages are never
showing a dataset the band does not describe. **Export normalised data…** writes the transformed
table — one row per feature, one column per injection — as a tab-separated file, which is the
table to take to another tool. The two lines at the bottom say what happened: `61 features in · 0
dropped for missing values · 3 value(s) imputed · 0 dropped by the filter · 61 out · no sample
normalisation · log10 · auto scaling`, and which value became which.

## Normalisation check

![the normalisation check](images/statistics-normalisation.png)

MetaboAnalyst's before-and-after view, drawn from the same data. Above, one box per injection —
the spread of every feature's value in that injection — before (on the log scale, so the boxes are
readable) and after. The injections should line up after normalisation; a box that still stands
apart is an injection to look at, and often an injection that was diluted or that failed. Below,
twenty features drawn across the injections, before and after: they should spread alike after the
transformation, and a spread still skewed to one side is a transformation that did not do its
job. With the ratio source and no normalisation, before and after differ only by the transform.

## Fold change

The size of the change between two classes, one feature at a time, without a test. Choose the two
classes (the first **over** the second: `treated` over `control` means a fold change of 3 is
threefold higher in the treated) and the **threshold ×** that counts as a change (2 by default),
then **Compare**. The chart puts every feature along the bottom in table order and its log2 fold
change up, coloured when it passes the threshold — *up* is higher in the first class, *down* is
lower — and the table lists the fold change on the linear and the log2 scale. The fold change is
computed on the normalised values before the transformation, as a ratio of the class means, which
is what MetaboAnalyst does; on the log scale a ratio of means and a difference of means are not the
same thing. Clicking a point or a row draws the feature's boxes on the **Statistical test** page.

## Statistical test

![the statistical test page](images/statistics-test.png)

Which features differ between two classes, and how surely. The options:

| Option | What it does |
| --- | --- |
| the two classes | which are compared; only classes with two or more injections are offered |
| **Non-parametric** | the Mann–Whitney U test on ranks instead of the t-test — for when the values are not roughly normal, or a few are wild. With **Paired** it becomes the Wilcoxon signed-rank test |
| **Equal variances** | the pooled-variance Student t-test instead of Welch's, which does not assume the two classes spread alike. Welch is the default because it costs almost nothing when the variances are equal and is right when they are not |
| **Paired** | the injections of the two classes correspond one to one, in order — the same animal before and after; needs the same number in each class |
| **adjust** | what to do about testing hundreds of features at once: **FDR (Benjamini–Hochberg)**, the default and MetaboAnalyst's; **Holm**; **Bonferroni**; or **None (raw p)** |
| **α** | the level a feature has to reach, on the adjusted p, to count as significant (0.05) |

**Test** runs it; the line under the band says what was done — `liver (4) against blank (2) over
61 features by Welch's t-test · 27 at FDR ≤ 0.05`. The table gives, per feature, the fold change,
the statistic (t, or U), the p and the adjusted p, sorted as you like. Choosing a row draws the
feature on the right, one box per class with its injections as points, and **Open in the ion
table** jumps to it in the [[analytics-workspace]] to look at the peaks themselves. **Export
table…** writes the whole table; see [[exports#The analysis tables]].

The same comparison, with the same options, feeds the fold change, the volcano and the
enrichment: they are one computation shown four ways.

## Volcano plot

![the volcano plot](images/statistics-volcano.png)

Both things at once: the log2 fold change across, −log10 of the p up, so the features that changed
a lot *and* reliably sit in the top corners. The vertical dotted lines are the fold-change
threshold either way; the horizontal one is α on the adjusted p; the corners past both are
coloured — red *up* (higher in the first class), blue *down* — and named. The **fold change ≥**,
the adjustment and the **≤** level are the same options as on the test page, and **Redraw** applies
them. Clicking a point draws its boxes on the right. A feature past the fold-change line but under
the p line changed a lot in one or two injections and not the others; one past the p line but near
the middle changed reliably and a little. Both are worth knowing about; neither is a finding.

## ANOVA

Three or more classes. The one-way analysis of variance asks, per feature, whether the class
means differ at all; **Kruskal–Wallis** asks the same on ranks. **adjust** and **α** are as on the
test page; **Compute** runs it. The table gives the F (or H) statistic, the p and the adjusted p;
the chart underneath puts every feature along the bottom and −log10 p up, so the tall ones are the
ones to look at. Choosing a feature draws its boxes, one per class, and the panel under them
gives the **post-hoc** test — Fisher's LSD between every pair of classes (a Mann–Whitney between
each pair, for the rank test) — so a feature the ANOVA flags can be read as *high dose against
control, p 0.002; low dose against control, no*. With exactly two classes the page says to use the
two-group test instead.

## Correlations

Which features move together across the injections, or which injections look alike. The heatmap
is drawn over the **top** *n* features (25 by default) chosen by **most variable**, **lowest p in
the comparison** or **lowest p in the ANOVA**, with the **Pearson**, **Spearman** or **Kendall**
coefficient; **Between injections instead** draws the injection-by-injection matrix, where
replicates should be the warm blocks on the diagonal and an injection that correlates with nothing
is the one to check. **Compute** builds it; clicking a row of the feature matrix opens that
feature in the ion table.

## Pattern search

The reverse question: given a profile, which features follow it. **A feature's profile** takes one
feature — an internal standard, a marker you trust — and ranks every other by its correlation with
it across the injections; **The class order** takes a trend written as the classes in order
(`control, low, high`, in the box) and ranks the features by their correlation with that ramp, so
the ones that rise with the dose come first. Pearson, Spearman or Kendall, as before. **Search**
runs it; the bars are the correlations, the table has the p for each, and clicking a bar opens the
feature. **Export table…** writes the ranking.

## Random forest

A different kind of ranking. Five hundred decision trees (**Trees**), each grown on a bootstrap
sample of the injections with a random handful of the features tried at every split (**features
per split**, √p by default), vote on the class of every injection they did not see; the share they
get wrong is the **out-of-bag error**, an honest estimate that needs no separate cross-validation,
and the confusion table on the right says which classes were mistaken for which. The importance of
a feature is the fall in that accuracy when its values are shuffled — **mean decrease in
accuracy** — with the Gini decrease beside it in the table. **Grow the forest** runs it; it takes
a second or two. Clicking a bar opens the feature.

The forest is non-linear and does not care about scaling, so a feature it ranks that the
discriminant model does not is worth a look. With few injections and many features it will fit
the batch; the out-of-bag error is the number that says whether it would fit the next one, and it
should be read before the ranking is.

## Heatmap

![the clustered heatmap](images/statistics-heatmap.png)

The **top** *n* features (25 by default) chosen by **lowest p in the comparison**, **lowest p in
the ANOVA**, **most variable** or **every feature**, drawn against every injection, both sides
clustered with the **distance** and **linkage** of your choosing (Euclidean and average by default,
as MetaboAnalyst). **Standardise rows** puts every feature on its own z-score across the
injections, so the colours compare features rather than abundances — on, as a rule; off, the
abundant features drown the rest. **Cluster injections** clusters the columns too; off, they stay
in table order, which is the right choice when the run order matters. **Build** draws it.

The colour strips name the class of each injection along the top and the lipid class of each
feature down the side; the trees are the dendrograms. Clicking a row opens the feature in the ion
table. The colour scale, the labels, the trees and the values are in the chart's options — see
[[chart-export]].

**A red cell in a blank is not an abundance.** A standardised row says one thing only: how this
injection compares with the rest of that row. A feature that is barely there in every injection
still has a reddest cell, and it lands wherever the noise happened to be highest — often a solvent
blank, where a gap-filled integral of nothing can sit a little above the samples. Hover any cell
and the tooltip gives the number the colour was standardised from, under the z-score; that is what
separates a red cell that means something from a red cell that means the row is empty. To see the
rows as abundances instead, turn **Standardise rows** off — and expect the few abundant features to
take the whole scale.

## K-means

The injections partitioned into *k* groups by their whole profiles, without being told their
classes (**Clusters (k)**, 2 by default; **Run**). The plot puts them on the first two principal
components coloured by cluster; the panel below lists the members of each cluster with its class.
When the clusters reproduce the classes the classes are real in the data; when a cluster mixes
them, or one injection sits in the wrong cluster, that is what the design looks like without its
labels. Twenty restarts with k-means++ seeding, the best kept, so it does not depend on luck.

## Lipid enrichment

![the lipid enrichment page](images/statistics-enrichment.png)

MetaboAnalyst's enrichment asks whether the metabolites that changed fall into a pathway more
often than chance would put them there. Lipids have no pathway list worth the name at the level of
a species, but they have their classes, their chain lengths and their unsaturation, and those are
the sets here:

| Set kind | Example | What it means when enriched |
| --- | --- | --- |
| lipid class | `PC`, `TG`, `Cer` | a whole class moved — a synthesis pathway, or a membrane remodelling |
| chain length | `34 carbons`, `38 carbons` | lipids built on the same fatty acids moved together |
| unsaturation | `saturated`, `monounsaturated`, `4 double bonds` | a desaturase, or the dietary fatty acids |
| species | `PC 34:1` (with **Species sets too**) | the same sum composition across classes — several adducts or isomers of one lipid, mostly a check |

The **Significant set** is the features the comparison found (**Significant in the comparison**),
the ANOVA found, or the top of the forest; **minimum set size** drops sets too small to test
(3 by default). **Compute** runs the over-representation analysis: a hypergeometric test of the
hits in each set against the features tested, adjusted as on the test page. The bars are −log10 p
per set, the dotted line p 0.05; the table gives the hits, the size, the enrichment ratio (hits
over expected) and the p's. **Export table…** writes it with the members of each set.

Two more readings of the same comparison sit below. **Each class as a whole** is the mean log2
fold change of every feature in the class, so a class that rose or fell as a body shows as one
bar. The **chain map** is a grid of the class's species — carbons down, double bonds across — each
cell the mean log2 fold change of the lipids with that composition, with the number printed: the
place to see that the longer, more unsaturated species rose while the short saturated ones fell,
which a list of p-values does not show. **Chain map of** picks the class, or all of them together.

The sets are read from the names MS-DIAL gave the features, so an unknown belongs to no set and
counts only in the total tested; the parsing is in [[algorithms#Lipid names]]. The [[pathways]]
page asks the next question — which reactions moved the classes that moved.

## What the pages do not do

Batch effects between studies, time series, and two factors at once. The tests assume the
injections are independent (except **Paired**) and the classes are the only thing that differs on
purpose; the drift correction on the [[statistics-workspace#Drift correction]] page is the one
concession to the instrument.
