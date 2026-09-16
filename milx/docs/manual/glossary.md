---
title: Glossary
section: Reference
order: 45
summary: Short definitions of the terms used in the application and this manual.
---

# Glossary

**Acquisition type** — how the MS/MS was acquired: DDA (data-dependent, one precursor per scan), SWATH (data-independent, fixed isolation windows) or AIF (all-ion fragmentation). Decides which product scans belong to a peak. See [[processing#Acquisition types]].

**Adduct** — the ion form a compound was detected as: `[M+H]+`, `[M+Na]+`, `[M+NH4]+`, …

**Alignment** — matching the peaks of every injection into one list of features by retention time and m/z. See [[algorithms#Alignment and gap filling]].

**Alignment spot** — MS-DIAL's word for a feature.

**Analytical order** — the position of an injection in the sequence it was run in.

**Annotation** — the name given to a feature by matching its spectrum against a library; also the act. See [[annotation]].

**Annotation level** — confident, suggested or m/z only, read from the name MS-DIAL wrote. See [[concepts#Annotation levels]].

**Area ratio** — an analyte's peak area divided by the area of its class's internal standard in the same injection; the *relative abundance* a lipidomics result is reported in. See [[internal-standards]].

**ASCA** — ANOVA-simultaneous component analysis: the matrix taken apart into what each factor of a design explains, each part looked at with its own components. See [[two-factor-analysis]].

**Auto-scaling** — dividing every feature by its standard deviation after centring, so each weighs the same in a model; *unit variance* scaling.

**Batch** — the list of injections a project processes together; also the number that groups injections run in one sequence, for the drift correction.

**BioPAN** — LIPID MAPS' pathway analysis for lipidomics: reactions of the lipid network weighted by product over reactant and compared between two conditions. Built into the [[pathways]] page.

**Candidate** — one library match kept for a feature; the best is the annotation.

**Centroid** — a spectrum reduced to one m/z and intensity per peak, as opposed to profile.

**Class** — the free-text group of an injection, which the statistics compare.

**Curation** — everything a reviewer changes: tags, comments, names, integration windows, splits.

**Deconvolution** — MS2Dec: assigning each peak a product-ion spectrum cleaned of co-eluting contributions.

**Drift** — the slow change of the instrument's response across a sequence; corrected against the QC injections. See [[statistics-workspace#Drift correction]].

**FDR** — the false discovery rate: the share of the features called significant that are expected to be false. Benjamini–Hochberg controls it, and its adjusted p is the default on the test pages.

**Feature** — one compound-ion across the injections: a retention time, an m/z, a name, one peak per injection.

**Fill percentage** — the share of injections in which a feature's peak was detected rather than gap-filled.

**Fold change** — the ratio of a feature's mean in one class to its mean in another, on the normalised values before the transformation; log2 of it on the volcano plot.

**Gap filling** — integrating the chromatogram at a feature's place in an injection where no peak was detected.

**Hypergeometric test** — the test of over-representation: the chance of drawing at least this many members of a set among the significant features, when they are drawn from the tested ones without replacement.

**IDA** — SCIEX's name for DDA.

**Imputation** — replacing a missing value with an estimate: a fraction of the feature's minimum, its mean, or the mean of the nearest features. See [[one-factor-analysis#Data processing]].

**Interaction** — when the effect of one factor depends on the level of the other; the treatment that works at one time point and not at another.

**Internal standard** — a compound of a lipid class, labelled or odd-chain, spiked into every sample at the same amount so that the class's analytes can be reported as ratios to it. See [[internal-standards]].

**Isotope weight** — 0 for the monoisotopic ion, 1 for M+1, and so on; a feature marked 1 or more is an isotope of another.

**KNN** — k nearest neighbours; as an imputation, the mean of the *k* features that correlate best with the one missing a value.

**Loadings** — how much each feature contributes to a component, in the principal-component and discriminant plots.

**Method** — the processing parameters, as one `Key: value` text file. See [[method-parameters]].

**Molecular ion** — a feature that is not an isotope of another; the **Molecular ion** filter keeps only these.

**MS2Dec** — MS-DIAL's deconvolution algorithm.

**MSP** — the spectral-library format: one record per compound with its precursor, name, formula, retention time and product spectrum.

**Ontology** — MS-DIAL's word for the compound class; for a lipid, the lipid class.

**Orthogonal component** — in OPLS-DA, variation stripped out because it does not separate the classes.

**Over-representation (ORA)** — asking whether the features that changed fall into a set more often than chance would put them there. See [[one-factor-analysis#Lipid enrichment]].

**Pareto scaling** — dividing every feature by the square root of its standard deviation after centring; between auto-scaling and centring only.

**Peak** — one chromatographic peak of one injection at one m/z.

**Permutation test** — refitting a model on shuffled class labels many times to see how often chance does as well.

**PQN** — probabilistic quotient normalisation: each injection scaled by the median of its feature-by-feature ratios to a reference profile; the usual correction for a dilution effect.

**Q²** — the share of the class membership a model predicts for injections it was not fitted on.

**QC** — a pooled quality-control injection, the same material every time; the type the drift correction reads.

**Reaction weight** — a reaction's product abundance divided by its reactant abundance in one injection; what the pathway analysis compares between classes.

**Relative abundance** — see *area ratio*.

**Representative** — the injection whose peak scored best for a feature; its spectrum and name are the feature's.

**Reviewed** — a feature carrying any tag, or marked so by the reviewer.

**R²Y** — the share of the class membership a model reproduces on its own injections.

**Scores** — where each injection lands on a model's components.

**S-plot** — covariance against correlation of every feature with the predictive component of an OPLS-DA model.

**Stouffer's method** — combining Z-scores by summing them and dividing by the square root of their number; how a pathway's Z is made from its reactions'.

**Survey scan** — an MS1 scan.

**Tag** — one of MS-DIAL's five review flags. See [[review-tags]].

**Two-way ANOVA** — the analysis of variance with two factors: the effect of each, and their interaction, tested on one feature. See [[two-factor-analysis]].

**Type** — what an injection is physically: Sample, Blank, QC or Standard.

**VIP** — variable importance in projection; how much of a model's separation rests on one feature.

**Volcano plot** — every feature's log2 fold change across against −log10 of its p up, so the features that changed a lot and reliably sit in the top corners.

**Welch's t-test** — the two-group t-test that does not assume the two classes spread alike; the default comparison.

**XIC / EIC** — an extracted ion chromatogram: intensity within a mass window against retention time.


**Z-score** — a value minus its mean, divided by its standard deviation; what a standardised heatmap row shows.
