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

**Batch** — the list of injections a project processes together; also the number that groups injections run in one sequence, for the drift correction.

**Candidate** — one library match kept for a feature; the best is the annotation.

**Centroid** — a spectrum reduced to one m/z and intensity per peak, as opposed to profile.

**Class** — the free-text group of an injection, which the statistics compare.

**Curation** — everything a reviewer changes: tags, comments, names, integration windows, splits.

**Deconvolution** — MS2Dec: assigning each peak a product-ion spectrum cleaned of co-eluting contributions.

**Drift** — the slow change of the instrument's response across a sequence; corrected against the QC injections. See [[statistics-workspace#Drift correction]].

**Feature** — one compound-ion across the injections: a retention time, an m/z, a name, one peak per injection.

**Fill percentage** — the share of injections in which a feature's peak was detected rather than gap-filled.

**Gap filling** — integrating the chromatogram at a feature's place in an injection where no peak was detected.

**IDA** — SCIEX's name for DDA.

**Isotope weight** — 0 for the monoisotopic ion, 1 for M+1, and so on; a feature marked 1 or more is an isotope of another.

**Loadings** — how much each feature contributes to a component, in the principal-component and discriminant plots.

**Method** — the processing parameters, as one `Key: value` text file. See [[method-parameters]].

**Molecular ion** — a feature that is not an isotope of another; the **Molecular ion** filter keeps only these.

**MS2Dec** — MS-DIAL's deconvolution algorithm.

**MSP** — the spectral-library format: one record per compound with its precursor, name, formula, retention time and product spectrum.

**Ontology** — MS-DIAL's word for the compound class; for a lipid, the lipid class.

**Orthogonal component** — in OPLS-DA, variation stripped out because it does not separate the classes.

**Peak** — one chromatographic peak of one injection at one m/z.

**Permutation test** — refitting a model on shuffled class labels many times to see how often chance does as well.

**Q²** — the share of the class membership a model predicts for injections it was not fitted on.

**QC** — a pooled quality-control injection, the same material every time; the type the drift correction reads.

**R²Y** — the share of the class membership a model reproduces on its own injections.

**Representative** — the injection whose peak scored best for a feature; its spectrum and name are the feature's.

**Reviewed** — a feature carrying any tag, or marked so by the reviewer.

**Scores** — where each injection lands on a model's components.

**S-plot** — covariance against correlation of every feature with the predictive component of an OPLS-DA model.

**Survey scan** — an MS1 scan.

**Tag** — one of MS-DIAL's five review flags. See [[review-tags]].

**Type** — what an injection is physically: Sample, Blank, QC or Standard.

**VIP** — variable importance in projection; how much of a model's separation rests on one feature.

**XIC / EIC** — an extracted ion chromatogram: intensity within a mass window against retention time.
