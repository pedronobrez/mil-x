---
title: Algorithms
section: Under the hood
order: 50
summary: What each step of processing and each statistical view computes, in prose — peak picking, MS2Dec, annotation scoring, alignment, PCA, QC-RLSC, PLS-DA, OPLS-DA, clustering, the molecular network.
---

# Algorithms

The processing algorithms are MS-DIAL's, unchanged; they are summarised here so a result can be
read with the right expectations. The statistics are OpenDIAL's own and are described in enough
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

Every view starts from the same matrix: injections down the rows, features across the columns,
each value the peak height (or area), with missing and non-positive values replaced by the
smallest positive value of their feature, then log10-transformed, centred, and scaled — unit
variance, Pareto (divided by the square root of the standard deviation) or not at all.

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

### Clustering

Average-linkage agglomerative clustering of the injections over the distance one minus the Pearson
correlation of their feature profiles. The dendrogram joins two clusters at the height of their
mean pairwise distance.

### The molecular network

For every pair of features with product spectra, the modified cosine MS-DIAL and GNPS use: peaks
are matched greedily within the fragment tolerance, intensities are square-rooted and normalised
to the base peak, and the cosine is taken over the matched pairs. Pairs at or above the cut-off
become edges; features with no edge are counted and, by default, not drawn.
