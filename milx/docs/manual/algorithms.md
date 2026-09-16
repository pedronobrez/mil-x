---
title: Algorithms
section: Under the hood
order: 50
summary: What each step of processing and each statistical view computes, in prose — peak picking, MS2Dec, annotation scoring, alignment, PCA, QC-RLSC, PLS-DA, OPLS-DA, clustering, the molecular network.
---

# Algorithms

The processing algorithms are MS-DIAL's, unchanged; they are summarised here so a result can be
read with the right expectations. The statistics are MIL-X's own and are described in enough
detail to reproduce.

## Processing

### Peak picking

Each raw file's survey scans are cut into m/z slices of the method's *mass slice width*, and an
extracted ion chromatogram is built for each. The chromatogram is smoothed (linear weighted moving
average by default, over the *smoothing level*), and peaks are detected by their first-derivative
shape: a rise, an apex, a fall, at least *minimum peak width* scans wide and *minimum peak height*
tall. Each peak's height is its apex intensity, its area the integral above zero, and its
signal-to-noise the apex over the local noise. Isotopes are grouped by the charge states up to
*max charge number*, and adducts by the list the method names, so `[M+Na]+` at +21.98 Da of an
`[M+H]+` peak is recognised as the same compound.

### Deconvolution (MS2Dec)

In DDA a product scan belongs to the peak whose m/z its precursor sits within the centroid
tolerance of; in SWATH and AIF, to every peak inside the isolation window. For each peak, MS2Dec
models the chromatographic profile of every product ion across the scans around the apex and keeps
the ions whose profiles follow the peak's own — co-eluting neighbours, modelled over the *sigma
window*, are subtracted. The result is one clean product spectrum per peak, written to the `.dcl`.

### Annotation

Each deconvoluted spectrum is scored against every library record within the MS1 tolerance of
its precursor. The weighted dot product (intensities square-rooted and mass-weighted), the simple
dot product, the reverse dot product (over the reference's peaks only), the matched-peak
percentage and the mass similarity are combined into the total score; retention similarity is
added when the library has retention times and the method says so. Every cut-off in
[[method-parameters#Identification]] has to pass; the best record is the annotation, and in
lipidomics its name is rewritten from the fragments observed. See [[annotation]].

### Alignment and gap filling

The peaks of every injection are matched to those of the reference injection by a score that
weighs retention-time distance and m/z distance by the two *factors*, within the two tolerances.
Matched peaks become one feature; a peak nothing matches becomes a feature on its own. For each
feature and injection where no peak was found, gap filling integrates the extracted ion
chromatogram inside the feature's retention window anyway, so the matrix has no holes, and marks
the value gap-filled. Filters then drop features detected in too few injections. The result is the
`.arf2` container the review edits.

### Re-integration

Inside the window the reviewer draws, height is the apex, area the trapezoid under the trace in
intensity × seconds, and the baseline-corrected area subtracts the line joining the two edges;
start, apex and end move to the first, highest and last scan inside. See [[reintegration]].

## Statistics

Every page starts from the same table: injections down the rows, features across the columns.
What the values are, and what is done to them before a page reads them, is the first section.

### Relative abundance

With the ratio source, the features are the spots tagged Confirmed; each is assigned to the lipid
class its ontology names (or the class parsed from its name), and every class with a standard has
its analytes divided, injection by injection, by the standard's area (or height) in that
injection: `ratio[i, j] = area[i, j] / area[i, standard of class(j)]`. A zero or missing standard
makes the ratio missing. The standard is left out; a class without one keeps its raw values, and
the report names it.

### Lipid names

A name is parsed into class, carbons and double bonds: `PC 34:1`, `PC 16:0_18:1` (chains summed),
`SM 18:1;O2/23:0` (chains summed, the oxygen ignored), `Cer d18:1/17:0`, `TG 52:2|TG 16:0_18:1_18:1`
(the part before the bar), with the class taken from the ontology when MS-DIAL gave one. A
standard is a name carrying a label — `d7`, `d9`, `(d7)`, `-d5`, `13C`, `IS` as a token — and an
odd-chain species is one whose total carbons are odd (15:0, 17:0, 19:0 chains). The score that
suggests a standard is 100 for a labelled standard of the class, 40 for an odd-chain species of the
class, 20 for a standard of another class, and a class is suggested one only at 40 or more.

### Preprocessing

In MetaboAnalyst's order. *Missing values*: a feature absent in more than the allowed share of the
injections is dropped; the remaining gaps become a fraction of the feature's minimum positive
value (a fifth by default), its mean, its median, or — KNN — the mean of the ten features that
correlate best with it over the injections both have. *Filter*: the features are ranked by the
chosen statistic (interquartile range, standard deviation, median absolute deviation, RSD, mean
or median) and the lowest fraction dropped; empty, the fraction is MetaboAnalyst's rule by count
(0 under 250, 5 % under 500, 10 % under 1000, 25 % beyond). *QC RSD*: a feature whose relative
standard deviation over the QC injections exceeds the limit is dropped. *Sample normalisation*:
by the injection's sum or median; by the probabilistic quotient — the median of the injection's
ratios to the median profile of all injections (or of the QC injections); or by one reference
feature. *Transformation*: log10, log2, ln, square root or cube root, with a non-positive value
floored at a tenth of the smallest positive one first. *Scaling*, for the models only: centring,
then division by the standard deviation (auto) or its square root (Pareto).

### One-factor statistics

*Two groups.* Welch's t-test by default, with the Welch–Satterthwaite degrees of freedom; the
pooled-variance Student test on request; the paired t-test on the differences. Mann–Whitney's U
with the exact distribution by the counting recursion up to twenty per group without ties and the
normal approximation with tie correction otherwise; the Wilcoxon signed-rank test likewise. The
fold change is the ratio of the class means on the normalised values before transformation; the
tests run on the transformed ones. *Several groups.* The one-way ANOVA F, with Fisher's least
significant difference between every pair as the post-hoc; Kruskal–Wallis' H with the
chi-square approximation, and pairwise Mann–Whitney as its post-hoc. *Adjustment.*
Benjamini–Hochberg (step-up, monotone), Holm (step-down) and Bonferroni.

*Correlations.* Pearson; Spearman as Pearson on mid-ranks; Kendall's τ-b with the tie-corrected
normal approximation for its p. The pattern search is the same correlation of every feature with
a given feature's profile, or with the class order as integers.

*Distributions.* Lanczos log-gamma; the regularised incomplete gamma by series and continued
fraction; the regularised incomplete beta by Lentz's continued fraction; Student's t, Fisher's F
and the chi-square from them; the normal CDF by the complementary error function and its
quantile by Acklam's rational approximation; the hypergeometric upper tail by summed log-binomials.
Each is tested against tabulated values.

### Clustering, heatmap and k-means

Agglomerative clustering over any of four distances — Euclidean, one minus Pearson, one minus
Spearman, Manhattan — with average, complete, single or Ward linkage, the last three by the
Lance–Williams update. The heatmap clusters the features by the same rule and, optionally, the
injections, on the standardised rows (each feature centred and divided by its standard deviation
across the injections). K-means with k-means++ seeding, Lloyd's iterations to convergence, twenty
restarts and the lowest within-cluster sum of squares kept.

### Random forest

Breiman's: each tree grown on a bootstrap of the injections, at each node the best Gini split
among a random √p of the features, to purity; the out-of-bag injections of each tree are
classified by it and the majority over trees is the prediction, whose error rate is the out-of-bag
error. Importance is the mean over trees of the fall in out-of-bag accuracy when the feature's
values are permuted, and the total Gini decrease attributable to the feature.

### Two factors

*The two-way ANOVA*, per feature on the transformed values, with type-II sums of squares by
model comparison: least-squares fits of the models B, A, A+B and A+B+A×B (treatment coding, an
intercept), each by the normal equations with pivoting so a dependent column costs rank rather
than a crash; SS(A|B) = RSS(B) − RSS(A+B), SS(B|A) = RSS(A) − RSS(A+B), SS(A×B) = RSS(A+B) −
RSS(full); the degrees of freedom of an effect are the rank it adds, the residual's are n minus
the rank of the full model, and F and p follow. A design without a replicated cell has no
interaction to test and says so. Each effect's p is adjusted across the features on its own.
*ASCA*: the scaled matrix is centred, each factor's part is its level means, the interaction's
the cell means less the main effects, the residual what remains; the share of an effect is its
sum of squares over the matrix's; its permutation p shuffles the factor's labels over the matrix
with the other effects removed (the interaction's over the cell labels with the main effects
removed) and counts how often the shuffled sum of squares reaches the observed; each part gets a
PCA by the Gram matrix, and the scores are the part plus the residual projected on its loadings.

### Pathways

BioPAN's network and method on the normalised linear values. Every feature is placed in the
network by its class — the ontology folded to BioPAN's names, the ethers split by the `O-`/`P-`
mark in the name, the sphinganine forms by subclass or a saturated composition — and, at the
species level, by its sum composition. A node's abundance in an injection is the sum of its features; at the fatty-acid
level the nodes are the free fatty acids measured, by composition. The edges are the reaction table's at the class
level; at the species level a preserving reaction joins equal compositions, a chain-removing one
joins two species whose compositions differ by a free fatty acid measured in the dataset, and a
chain-adding one two whose compositions differ by an acyl-CoA measured in the dataset (BioPAN's
rule); at the fatty-acid level the thirty chain steps of BioPAN's table and no others. For
every edge with both ends present, the weight per injection is product over reactant (no weight
without the reactant, zero without the product); the weights of the two classes are compared by
Welch's t-test where each has two or more; Z is the normal quantile of 1 − p/2 with the sign of
the difference of means — BioPAN's one-sided `qnorm(1 − p)` — with p floored at 10⁻¹⁵. A pathway is a simple directed chain of tested edges up
to the chosen length, found by depth-first search from every node (bounded at twenty thousand
chains), and its Z is Σ Z_i / √k; active and suppressed are |Z| at or past the threshold either
way. The predicted list is the class-level reactions with exactly one end measured.

### Enrichment

Every feature belongs to the sets its parsed name gives it — its lipid class, its carbon number,
its degree of unsaturation, and, optionally, its sum composition. For each set with at least the
minimum number of members among the tested features, the hypergeometric probability of at least
the observed number of hits among the significant features, adjusted as the comparison was. The
class change is the mean and median log2 fold change over the class's features with the count up
and down and a one-sample t-test of the log2 fold changes against zero; the chain map is the mean
log2 fold change per (carbons, double bonds) cell of a class.

### Principal components

A metabolomics matrix has far more features than injections, so the components come from the
injection-by-injection Gram matrix `X·Xᵀ` rather than the feature covariance: with eight
injections that is an 8×8 eigenproblem (solved by Jacobi rotation) instead of a 1600×1600 one, and
it gives exactly the same components. Scores are the eigenvectors scaled by the square roots of
their eigenvalues; loadings are `Xᵀ·scores` normalised; the explained variance is each eigenvalue's
share of the total.

### Drift correction (QC-RLSC)

For every feature, batch by batch: the values of the QC injections against their injection order
are fitted with a locally weighted linear regression (tricube weights over the nearest fraction
*span* of the controls), the fitted curve is evaluated at every injection's order, and every value
is divided by it and multiplied by the overall QC mean, so corrected values keep the size of the
originals. A batch with fewer than three controls is levelled by a single factor — its QC mean
against the overall QC mean — instead. A feature is skipped when fewer than three controls in
total have a positive value. The coefficient of variation over the controls before and after is
the measure. Nothing is written to the result.

### The discriminant model (PLS-DA)

The classes are dummy-coded into `Y` (one column per class), and partial least squares is fitted by
NIPALS for the requested number of components, giving scores, weights and loadings. R²Y is the
share of `Y` the fit reproduces; Q² is the share predicted for each injection when the model is
fitted on the others (leave-one-out), with centring and scaling redone inside each fold from the
training injections alone. The permutation p is the share of *k* shuffles of the class labels whose
Q² reached the real one. VIP for each feature is the usual weighted sum of squared weights over
the components. With two classes the side a feature goes with is read off the sign of its
first-component weight against the class means of the first-component scores.

The cross-validation and the permutations are done in the space the injections span: the features
are reduced once per fold to the Gram matrix of the training injections and the cross-products with
the held-out one, and every refit is a few operations on *n*×*n* matrices. A test holds this path
to the plain one to nine decimals; on 2 083 features and eight injections, 200 permutations take
about 400 ms.

### The orthogonal model (OPLS-DA)

Trygg and Wold's orthogonal signal correction: with two classes `Y` is one column; the weight
vector `w` is the direction of `Xᵀy`; for each orthogonal component the part of the first NIPALS
loading that is orthogonal to `w` is taken as `w_ortho`, its score `t_ortho = X·w_ortho` and loading
removed from `X`, and this is repeated the requested number of times. One predictive component is
then fitted on what is left. The S-plot puts each feature's covariance with the predictive score
across and its correlation with it up. R²Y, Q² and the permutation p are computed the same way as
for the plain model, in the same reduced space, so the two are held to the same standard.

### The molecular network

For every pair of features with product spectra, the modified cosine MS-DIAL and GNPS use: peaks
are matched greedily within the fragment tolerance, intensities are square-rooted and normalised
to the base peak, and the cosine is taken over the matched pairs. Pairs at or above the cut-off
become edges; features with no edge are counted and, by default, not drawn.
