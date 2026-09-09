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
*removes* a chain, *adds* a chain, *class* only, or a fatty-acid step), its genes, the enzyme's
name and its source. The BioPAN part is a transcription of the tool's own database — the
`biopan_reaction` and `biopan_reaction_gene` tables of the archived source on OSF — checked line
by line: 51 reactions between classes, 13 over the ether lipids, 3 over the sphinganine bases and
30 between fatty acids, 97 in all, with BioPAN's 249 reaction–gene links as they stand, two
edits apart: FA 24:6 → FA 26:6, which BioPAN's table writes as FA(34:6), a slip; and the mouse
Scd1 and Scd3 on the two desaturations given as the human SCD and SCD5, so the table is human
symbols throughout. Below them, marked
`extension`, the twelve steps OpenDIAL adds for classes a run confirms and BioPAN does not cover,
switched on by **Beyond BioPAN** and off by default. MS-DIAL's ontologies fold onto BioPAN's
nodes: `Cer_NS`, `Cer_AS` and the other sphingosine subclasses into `Cer`, the `DS` subclasses and
any saturated composition into `dhCer` (`dhSM` likewise), `Sph` into `SPB`, `DHSph` into `dhSPB`,
`EtherPC` into `O-PC` or `P-PC` by the name. A test holds the table to the parser: every reaction
names classes the lipid-name reader knows, and the counts are the database's.

**The species level** follows BioPAN's rule (`is_valid_reaction` in `lib_parse_data.r`): a
preserving reaction links species of the same sum composition; a chain-removing one links a
reactant species to a product species when the difference of their compositions is a free fatty
acid measured in the dataset (`PC 34:1` → `LPC 16:0` needs `FA 18:1`); a chain-adding one when the
difference is an acyl-CoA measured in the dataset (`LPC 16:0` → `PC 34:1` needs `FACoA 18:1`).
Resolved chains are not consulted, as BioPAN does not consult them. **The fatty-acid level** is
the free fatty acids measured — the `FA` class, as BioPAN's `fa_processed_gr` — linked by the
thirty steps of its table.

**The scoring** follows BioPAN's R code (`lib_pathway_analysis.r`): the weights are the ratios
themselves, an injection without the reactant is dropped and one without the product weighs zero;
the two classes are compared by `t.test` — Welch, or paired when **Paired** is ticked — and Z is `qnorm(1 - p)` of the
one-sided p in the direction of the change, which OpenDIAL computes as the two-sided p's quantile
with the sign of the difference of means, the same number, capped at about 8 where p underflows.
Pathways are every simple chain of tested reactions up to the chosen length, scored
`sum(z) / sqrt(size)` over the chain's reactions, as BioPAN's `get_sub_pathway_zscore` does; the
paper writes it 1/√(n−1) · Σ Zᵢ over the chain's n lipids, the same thing.

**The page** reuses the workspace's controls: the network is a spring-embedded graph with a fixed
seed (the same network always draws the same way), the reaction's injections are the box plot
every other page uses, the tables export through the same frame, and the whole thing answers to
the driving channel and the smoke test like the rest.

## Where it departs from BioPAN

- The **Beyond BioPAN** extensions, off by default and marked in the table.
- A reaction is only testable when both its classes have a confirmed analyte other than the standard, since the standard divides itself out of the ratios. The **Not measured** table says what confirming one more class would open up, which BioPAN's predicted mode does not.
- The mammalian network only. Two conditions at a time; three or more classes go pairwise through the comparison's two classes.

Sources: Gaud E. *et al.*, "BioPAN: a web-based tool to explore mammalian lipidome metabolic
pathway on LIPID MAPS", *F1000Research* 2021; the BioPAN pages at lipidmaps.org/biopan; Nguyen A.
*et al.*, "Using lipidomics analysis to determine signalling and metabolic changes in cells",
*Current Opinion in Biotechnology* 2017, for the network; LipidLynxX for the name normalisation.
