---
title: BioPAN in OpenDIAL — design notes
section: Under the hood
order: 53
summary: What LIPID MAPS' BioPAN does, how it does it, and how the Pathways page of the Statistics workspace reproduces it on the reviewed analytes — the plan that was built, and where it departs from the original.
---

# BioPAN in OpenDIAL — design notes

**BioPAN** (Bioinformatics Methodology for Pathway Analysis, LIPID MAPS, Gaud *et al.*,
*F1000Research* 2021) is the one pathway tool built for lipidomics as it is actually measured.
Where MetaboAnalyst's enrichment asks whether the lipids that changed share a set, BioPAN asks
which *reactions* — the enzymatic steps that turn one lipid into another — are running faster or
slower between two conditions, scores the pathways those reactions chain into, and names the
genes behind them. This page is the design behind the [[pathways]] page, which is BioPAN's method
built into the [[statistics-workspace]].

## What BioPAN does

**Input.** A table of lipid names against samples, with at least two replicates per condition,
in the form a lipidomics result comes in — sum composition (`PC 34:1`) or molecular species
(`PC 16:0_18:1`) — and the condition of each sample. The names are normalised with LipidLynxX;
the fatty-acyl species are used where given and summed to species otherwise.

**The reaction network.** A curated set of reactions over the mammalian lipid subclasses, at two
levels: between classes (PE → PC by PEMT, PC → LPC by a phospholipase A, DG → TG by DGAT, Cer → SM
by a sphingomyelin synthase, and so on) and between fatty acids (elongation by two carbons,
desaturation by one double bond). At the species level a reaction links the species whose
composition the enzyme would preserve.

**Scoring a reaction.** For each reaction and each sample, the weight is the ratio of the
product's abundance to the reactant's. The weights are compared between the two conditions with a
t-test; the p becomes a Z-score by the inverse normal CDF, with the sign of the change. A reaction
is *active* when its Z is past the threshold and *suppressed* when past it the other way.

**Scoring a pathway.** A pathway is a chain of reactions; its score combines its reactions'
Z-scores, normalised for the chain length, so that a long chain of modest reactions and a short
chain of strong ones compare fairly. Pathways past the threshold (|Z| > 1.645 for p < 0.05
one-sided; BioPAN offers several) are reported as active or suppressed, and each reaction in them
is mapped to its genes from LIPID MAPS' proteome database.

**Output.** An interactive graph of the lipid subclasses (or species) joined by the reactions,
green for the ones that went up and purple for the ones that went down, with the pathway tables,
the Z-scores, the p-values and the gene lists beside it; also a *predicted* mode that lists the
reactions whose product is present and reactant absent, as candidates.

## How OpenDIAL builds it

**The input is already there.** The [[statistics-workspace]] builds the confirmed analytes as
ratios to their class standards, with the class of every injection, and MS-DIAL's names carry
both the class and the composition, parsed for the [[one-factor-analysis]]'s enrichment. Nothing
is exported and re-imported, and the review — which features are real — goes in for free.

**The reaction database** is a text table shipped inside the engine, `reactions.tsv`: one line
per reaction with its id, reactant class, product class, how it maps species (*preserving*,
*removes* a chain, *adds* a chain, or *class* only), its genes and the enzyme's name. Sixty-odd
reactions over some thirty classes: BioPAN's mammalian network restricted to the subclasses
MS-DIAL annotates in an LC-MS run — the ceramide subclasses (`Cer_NS`, `Cer_AS`, …) fold into
`Cer`, `SPB` into `Sph`, `EtherPC` into `PC O-`, and the reactions over classes MS-DIAL does not
name (ceramide phosphoethanolamine, gangliosides, the CoA esters) are left out. A test holds the
table to the parser: every reaction names classes the lipid-name reader knows.

**The species level** follows from the class reaction and the composition, so it is a rule, not
a second table: a preserving reaction links species of the same sum composition; a chain-removing
one links a resolved species to the species left by dropping each of its chains in turn (`PC
16:0_18:1` → `LPC 16:0` and `LPC 18:1`; `TG 16:0_18:1_18:1` → `DG 34:1` and `DG 36:2`); a
chain-adding one is the same read backwards. A sum composition alone takes part only in the
preserving reactions. **The fatty-acid level** sums each chain over the resolved species that
carry it (sphingoid bases excepted) and links chains by elongation (`c:d` → `c+2:d`, ELOVL6 on
palmitate, ELOVL5 and ELOVL2 on the polyunsaturates, ELOVL1/3/7 on the long saturates),
desaturation at the steps mammals have (SCD on 16:0 and 18:0; FADS2 on 18:2, 18:3, 24:4, 24:5;
FADS1 on 20:3 and 20:4) and the peroxisomal shortening of 24:5 and 24:6.

**The scoring** reuses the engine's Welch test and its normal quantile: the weights are compared
on the log2 scale, Z is the two-sided p's quantile with the sign of the change, capped at about 8
where p underflows. Pathways are every simple chain of tested reactions up to the chosen length,
combined by Stouffer's method, Σ Z / √k — BioPAN's own normalisation differs by a constant and the
ranking is the same; the choice is stated here so nobody takes the two Z's for one number.

**The page** reuses the workspace's controls: the network is a spring-embedded graph with a fixed
seed (the same network always draws the same way), the reaction's injections are the box plot
every other page uses, the tables export through the same frame, and the whole thing answers to
the driving channel and the smoke test like the rest.

## Where it departs from BioPAN

- The reaction table is a transcription, not a copy: BioPAN's 94 reactions over 41 subclasses became the sixty-odd over the classes an MS-DIAL lipidomics run can confirm. The table is meant to be argued with and extended.
- Species-level acyl reactions need the fatty-acyl level, which MS-DIAL gives only where the MS/MS supported it; at the sum-composition level the chain reactions are ambiguous and are skipped, as BioPAN also does when the input has no chains.
- Stouffer's combination for the pathways, as above.
- A reaction is only testable when both its classes have a confirmed analyte other than the standard, since the standard divides itself out of the ratios. The **Not measured** table says what confirming one more class would open up, which BioPAN's predicted mode does not.
- The mammalian network only. Two conditions at a time; three or more classes go pairwise through the comparison's two classes.

Sources: Gaud E. *et al.*, "BioPAN: a web-based tool to explore mammalian lipidome metabolic
pathway on LIPID MAPS", *F1000Research* 2021; the BioPAN pages at lipidmaps.org/biopan; Nguyen A.
*et al.*, "Using lipidomics analysis to determine signalling and metabolic changes in cells",
*Current Opinion in Biotechnology* 2017, for the network; LipidLynxX for the name normalisation.
