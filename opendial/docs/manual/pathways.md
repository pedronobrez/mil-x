---
title: Pathways
section: Workspaces
order: 19
summary: The pathway analysis after LIPID MAPS' BioPAN — every reaction of the mammalian lipid network weighted by product over reactant, compared between two classes, scored as a Z, and chained into pathways with the genes behind them.
---

# Pathways

The [[one-factor-analysis]] says which lipids changed. This page asks what *made* them change: which
of the enzymatic steps that turn one lipid into another ran faster in one class than in the other,
and which chains of steps did. It is LIPID MAPS' **BioPAN** (Gaud *et al.*, 2021) built into the
[[statistics-workspace]], reading the same dataset as every other page — the confirmed analytes as
ratios to their standards — so that a reaction here is scored on the same numbers the volcano plot
shows, and nothing has to be exported and pasted into a web form. The design notes are in
[[biopan-plan]]; the arithmetic is in [[algorithms#Pathways]].

## What it computes

The mammalian lipid network is BioPAN's own, transcribed from the tool's database: 51 reactions
between lipid classes (PE → PC by PEMT, PC → LPC by the phospholipases A2, LPC → PC by the LPCATs,
DG → TG by DGAT2, Cer → SM by the sphingomyelin synthases, PC → PS by PTDSS1, PA → PG, PA → PI,
PA → PS, the phosphoinositide kinases and phosphatases, and so on), 13 over the ether lipids
(alkyl `O-` and alkenyl `P-` forms of PC, PE, their lyso species and LPA), 3 over the sphinganine
bases, and 30 between fatty acids — 97 in all, over 40 classes, with the genes BioPAN names for
each. For every reaction whose reactant and product are both measured:

1. **The weight**, per injection: the product's abundance divided by the reactant's. A reaction
   running faster leaves more product per unit of reactant. An injection without the reactant has
   no weight; one without the product has weight zero.
2. **The comparison**: the weights of the first class against the weights of the second, with
   Welch's t-test on the weights themselves, as BioPAN's code does it.
3. **The Z-score**: the one-sided p in the direction of the change turned into a normal quantile,
   Z = Φ⁻¹(1 − p), with the sign of the change — BioPAN's `qnorm(1 - p)`. Z is positive when the
   reaction's weight is higher in the first class, negative when lower.
4. **The status**: *active* when Z is past the threshold (faster in the first class), *suppressed*
   when past it the other way, *unchanged* between.

Then the **pathways**: every chain of reactions that can be walked through the network, up to a
chosen length, scored by combining its reactions' Z-scores — the sum divided by the square root of
the number of steps, which is BioPAN's 1/√(n−1) · Σ Zᵢ over the n lipids of the chain — so that a
chain of consistent steps scores higher than any one of them and a chain whose steps cancel scores
near zero. A pathway is active or suppressed at the same threshold.

## The band

| Control | What it does |
| --- | --- |
| **Level** | **Lipid classes** — every confirmed PC summed against every confirmed PE; **Molecular species** — PE 34:1 against PC 34:1, PC 16:0_18:1 against LPC 16:0 and LPC 18:1, and so on, following the compositions MS-DIAL resolved; **Fatty acids** — each chain summed over the species that carry it, and the elongation and desaturation steps between chains |
| **\|Z\| ≥** | BioPAN's thresholds, one-sided: 1.282 (p 0.10), **1.645 (p 0.05, the default)**, 2.054 (p 0.02), 2.326 (p 0.01) |
| **chains up to** | how many reactions a pathway may chain (3 by default; 1 to 6) |
| **Show unchanged** | draw the reactions that did not pass the threshold too, in grey |
| **Beyond BioPAN** | also the steps OpenDIAL adds to the network for classes a lipidomics run confirms and BioPAN does not cover — Cer ↔ HexCer ↔ LacCer, HexCer ↔ SHexCer, Chol ↔ CE, FA ↔ CAR, MG → FA, PE → PA — marked `extension` in the table; off by default, so the default result is BioPAN's network and nothing else |
| **Compute** | score the network between the two classes chosen on the **Statistical test** page |
| **Export reactions…**, **Export pathways…**, **Export list…** | the tables as tab-separated text; see [[exports#The analysis tables]] |

The two classes compared are the ones the comparison uses — the **Statistical test**, **Fold
change** and **Volcano plot** pages share them. Both need two or more injections.

## The network

![the pathways page](images/statistics-pathways.png)

The classes (or species, or fatty acids) are the nodes and the reactions the arrows, pointing from
reactant to product. **Green** is a reaction running faster in the first class, **purple** one
running slower, grey one that did not pass the threshold, dotted one that could not be tested
because fewer than two injections in a class had both ends. The thicker the arrow the larger the
|Z|. A node is coloured by its own change — green where the class rose in the first class, purple
where it fell — so a green arrow into a green node is a reaction that ran faster *and* produced
more, and a green arrow into a grey node is one whose product was consumed as fast as it was made.

Click an arrow and its injections are drawn below, one box per class, product over reactant —
the numbers the test compared — with the reaction's enzyme, its p, its Z and its genes in the line
under. Pick a pathway in the table and its chain lights up in the network.

## The tables

**Reactions** — every reaction the level produced, most decisive first: its log2 change (first
class over second), Z, status and genes. **Pathways** — the chains, by |Z|: the chain itself, its
length *k*, its Z and its status. **Not measured** — the reactions with one end confirmed and the
other not: *PC → PS, PS missing* means that confirming a PS in the review would make PTDSS1
testable. This list is the page's way of asking for the review to go one class further; it is
BioPAN's "predicted" mode. **Nodes** — what each node summed: how many confirmed analytes went into
it and its own log2 change.

## Reading it

The reaction weights are ratios of two abundances, each of them a ratio to a standard, over a few
replicates, so the Z-scores are a rough instrument — BioPAN says the same of its own. Read the
network the way the volcano plot is read: the arrows far past the threshold, in a chain that makes
biochemical sense, are the finding; a single reaction just past 1.645 with three replicates is a
hint. A class that rose as a whole makes every reaction *into* it look active and every reaction
*out of* it look suppressed — PEMT active and PLA2 suppressed together say "PC went up", not "two
enzymes changed"; the **Lipid enrichment** page's class bars say the same thing more directly, and
the two pages are meant to be read together.

Two things the page needs from the review. A reaction is only testable when both its classes have
a confirmed analyte *other than the standard* — the standard divides itself out of the ratios, so
a class whose only confirmed feature is its standard does not appear. And the **Molecular species**
and **Fatty acids** levels need the chains MS-DIAL resolved: a feature named `PC 34:1` takes part
in the composition-preserving reactions (PE 34:1 → PC 34:1) but not in the chain reactions
(→ LPC), which need `PC 16:0_18:1`; the fatty-acid level uses only the resolved species.

## What it is not

The mammalian network only — a plant or yeast result gets the reactions that overlap and no more;
the table is a text file (`reactions.tsv` in the engine, one line per reaction with its id, the two
classes, how it maps species, its genes, the enzyme and its source) that another organism's network
can be dropped into. Two classes at a time, like BioPAN. The ether lipids are split into alkyl
(`O-PC`, `O-PE`, …) and alkenyl (`P-PC`, `P-PE`, …) by the name MS-DIAL wrote (`PC O-34:1` against
`PC P-34:1`), the ceramides and sphingomyelins into the sphingosine and the sphinganine (`dhCer`,
`dhSM`) forms by the subclass (`Cer_NDS`) or by a composition with no double bond. Cardiolipin,
the phosphoinositides and the sphingoid bases are in the network but rarely confirmed in a
positive-mode run, so they usually appear only in **Not measured**. The fatty-acid genes are the
mouse symbols BioPAN's database carries (`Scd1`, `Scd3`); the rest are human.
