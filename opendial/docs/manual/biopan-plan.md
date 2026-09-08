---
title: BioPAN — feasibility and plan
section: Under the hood
order: 53
summary: What LIPID MAPS' BioPAN does, how it does it, and how it would be built into OpenDIAL as a pathway module reading the confirmed analytes — a plan, not yet a feature.
---

# BioPAN — feasibility and plan

**BioPAN** (Bioinformatics Methodology for Pathway Analysis, LIPID MAPS, Gaud *et al.* 2021) is the
one pathway tool built for lipidomics as it is actually measured. Where MetaboAnalyst's enrichment
asks whether the lipids that changed share a set, BioPAN asks which *reactions* — the enzymatic
steps that turn one lipid into another — are running faster or slower between two conditions,
scores the pathways those reactions chain into, and names the genes behind them. This page is the
plan for it as a module of OpenDIAL. It is not built; nothing on this page is in the application.

## What BioPAN does

**Input.** A table of lipid names against samples, with at least two replicates per condition, in
the form a lipidomics result comes in — sum composition (`PC 34:1`) or molecular species
(`PC 16:0_18:1`) — and the condition of each sample. The names are normalised with LipidLynxX;
the fatty-acyl species are used where given and summed to species otherwise.

**The reaction network.** A curated set of about 94 reactions over 41 lipid subclasses in the
mammalian pathways, at two levels: between classes (PE → PC by PEMT, PC → LPC by a phospholipase A,
DG → TG by DGAT, Cer → SM by a sphingomyelin synthase, and so on) and between fatty acids
(elongation by two carbons, desaturation by one double bond). At the species level a reaction
links the species whose composition the enzyme would preserve: `PE 34:1 → PC 34:1`, `DG 34:1 →
TG 52:2` only with the added chain accounted for. This is the database; the rest is arithmetic.

**Scoring a reaction.** For each reaction *i* and each sample, the weight is the ratio of the
product's abundance to the reactant's, `w_i = product / reactant`. The weights are compared
between the two conditions with a t-test; the p becomes a Z-score by the inverse normal CDF, with
the sign of the change. A reaction is *active* when its Z is positive past the threshold and
*suppressed* when negative.

**Scoring a pathway.** A pathway is a chain of *k* reactions; its score is the mean of its
reactions' Z-scores corrected for the chain length, `Z = (1/√(k−1)) · Σ Z_i`, so that a long chain
of modest reactions and a short chain of strong ones compare fairly. Pathways past the threshold
(|Z| > 1.645 for p < 0.05; BioPAN offers 1.282, 1.645, 2.054 and 2.326) are reported as active or
suppressed, and each reaction in them is mapped to its genes (PEMT, PTDSS1, PLA2G2E, DGAT1/2,
SGMS1/2, ELOVL and FADS families, …) from LIPID MAPS' proteome database.

**Output.** An interactive graph of the lipid subclasses (or species) joined by the reactions,
green for the ones that went up and purple for the ones that went down, with the pathway tables,
the Z-scores, the p-values and the gene lists beside it; also a *predicted* mode that lists the
reactions whose product is present and reactant absent, as candidates.

## Why it fits OpenDIAL

The input is exactly the dataset the [[statistics-workspace]] already builds: the confirmed
analytes as ratios to their class standards, with the class of every injection, and MS-DIAL's
names carry both the class and the composition, parsed already for the [[one-factor-analysis]]'s
enrichment. Nothing has to be exported and re-imported, and the review — which features are real
— goes in for free, which no web tool gets. The arithmetic is small: a two-group test per reaction
(the same Welch test the comparison uses), a normal quantile, a sum. The work is the network.

## The plan

**1. The reaction database (the real work).** A curated table shipped with the application, in
a text format that can be read, argued with and extended:

```
# reaction   reactant   product   level     genes
PEMT         PE         PC        class     PEMT
PLA2         PC         LPC       class     PLA2G2A;PLA2G2E;PLA2G4A
LPCAT        LPC        PC        class     LPCAT1;LPCAT2;LPCAT3
DGAT         DG         TG        class     DGAT1;DGAT2
SMS          Cer        SM        class     SGMS1;SGMS2
CERS         Sph        Cer       class     CERS1..CERS6
ELOVL        FA(n)      FA(n+2)   fattyacid ELOVL1..ELOVL7
FADS         FA(n:d)    FA(n:d+1) fattyacid FADS1;FADS2;SCD
```

The 94 reactions of BioPAN are published with the tool and the paper; the plan is to transcribe
them, with the gene mapping, and to keep the file under the same test as the manual — every
reaction names two classes the parser knows. The species-level linking (which `PC 34:1` a `PE
34:1` becomes) follows from the class reaction and the composition, so it is a rule, not a table:
a class reaction preserves the composition; an acyl reaction changes one chain by the elongation
or desaturation step; a DG → TG reaction adds a chain and needs the fatty-acyl level to be
resolved, and is done at the class level otherwise.

**2. The scoring, in the engine.** A `Pathways` module beside the statistics: build the weights
per sample from the analysis table (product over reactant; at the class level, the sum of the
class's confirmed species, which is also what the class-change bars already compute), test them
between the two chosen classes, convert to Z, chain the reactions into the pathways the database
defines (every path through the class graph up to a length limit, as BioPAN does), score, and
threshold. Reuse: `Univariate.TTest`, `Distributions.NormalQuantile`, `LipidNames.Parse`. About
four hundred lines and a day's tests against BioPAN's own example output, which is published.

**3. The page.** A **Pathways** page under *Enrichment* in the workspace: the two classes and the
threshold in its band; the class graph drawn with the existing `NetworkGraph` control (nodes the
classes, edges the reactions, coloured by Z — green up, purple down, grey untested because a class
is missing); the reaction table with Z, p and genes; the pathway table with the chain, its score
and its genes; the predicted reactions as a third table. Clicking a node opens the class in the
chain map; clicking a reaction draws the weight's boxes per class, the way a feature's are drawn
now. Export of every table and of the graph as SVG through the same frame.

**4. What it needs from the review.** A reaction is only testable when both its reactant and its
product are confirmed, and the sum of a class is only right when its standard divided it out. So
the page will say, per reaction, why it was not tested — *no confirmed LPC*, *PE has no standard*
— which is the feedback the reviewer needs to go back and confirm the missing piece.

## What it would not do, and the risks

- **Species-level acyl reactions need the fatty-acyl level** (`PC 16:0_18:1`, not `PC 34:1`). MS-DIAL gives that level only where the MS/MS supported it; at the sum-composition level the acyl reactions are ambiguous and BioPAN itself works at the class level then. The module would do the same and say so.
- **The mammalian network only.** BioPAN's reactions are mammalian; a plant or yeast dataset gets the parts that overlap and no more. The database format lets another organism's table be dropped in.
- **Two conditions at a time**, like BioPAN. Three or more classes go pairwise.
- **The Z from a t-test on ratios of two noisy things** is a rough instrument with three replicates; the page would carry the same warning the discriminant model does.
- **Curation effort.** Transcribing and checking 94 reactions and their genes against LIPID MAPS is the bulk of the work, and it is the part that decides whether the result is trustworthy; the arithmetic is an afternoon.

## Verdict

Feasible, and a good fit: the input is already there, reviewed; the arithmetic reuses what the
workspace has; the page reuses its controls. The cost is the reaction database, which is a week of
careful transcription and testing, and that is what to budget before starting. Sources: Gaud E.
*et al.*, "BioPAN: a web-based tool to explore mammalian lipidome metabolic pathway on LIPID
MAPS", *F1000Research* 2021; the BioPAN pages at lipidmaps.org/biopan; LipidLynxX for the name
normalisation.
