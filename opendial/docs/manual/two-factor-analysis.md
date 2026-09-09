---
title: Two factors
section: Workspaces
order: 20
summary: The Two factors page of the Statistics workspace — the two-way analysis of variance per feature, with the interaction, and ASCA over the whole matrix, on a design with two factors such as treatment and time.
---

# Two factors

Everything else in the [[statistics-workspace]] assumes one thing varies between the injections.
Many experiments vary two: treatment and time point, genotype and diet, class and batch. This page
takes the [[one-factor-analysis]]'s dataset and two factors from the sample table, and asks two
questions of it — one feature at a time, and of the matrix as a whole. The arithmetic is in
[[algorithms#Two factors]].

## Where the second factor comes from

The first factor is usually the **Class**. The second is typed in the **Factor** column of the
[[samples-workspace]] — `day0` and `day7`, `chow` and `HFD`, whatever the design's other axis is —
saved with the project, and joined to the result's injections by sample name. **Batch** and the
**Sample type** can stand as a factor too, which is how a batch effect is tested against the
classes. Either factor needs at least two levels; the page says so when one has not.

## The band

| Control | What it does |
| --- | --- |
| **Factor A**, **Factor B** | which two columns of the sample table are the design: **Class**, **Factor (Samples workspace)**, **Batch**, **Sample type** |
| **Interaction** | test whether the effect of one factor depends on the level of the other — the treatment that works at day 7 and not at day 0. Needs a replicate in at least one cell of the design; with one injection per cell the interaction cannot be told from the error, and the page says so and tests the main effects only |
| **adjust**, **α** | the multiple-testing adjustment and the level, as on the **Statistical test** page |
| **permutations** | for ASCA's test of each effect (200 by default) |
| **Compute** | fit every feature, then partition the matrix |
| **Export table…** | the ANOVA table as tab-separated text, with the cell means; see [[exports#The analysis tables]] |

The line under the band says what the design was — `2 × 3 design, 6 of 6 cells filled, 24
injections` — and how many features each effect moved at the level chosen.

## The two-way ANOVA

![the two factors page](images/statistics-two-factor.png)

One feature at a time, on the transformed values: the F and the p of factor A, of factor B, and of
their interaction, each p adjusted across the features. The table is sorted by the smallest of the
three adjusted p's, so the features that answer the design come first, and the columns say which
effect it was. The sums of squares are type II, by comparing nested least-squares fits, so an
unbalanced design — three replicates here, two there — is handled properly, and a cell that is
empty costs the interaction its degrees of freedom rather than the whole test.

Choosing a row draws the feature's boxes, one per cell of the design in the order of factor A,
coloured by factor B. This is the interaction plot in its honest form: parallel boxes across the
levels of B are a main effect, boxes that cross are an interaction, and the line under the chart
gives the three p's.

## ASCA

ANOVA-simultaneous component analysis (Smilde *et al.*, 2005) asks the same question of the whole
matrix at once. The centred, scaled matrix is taken apart into the part each factor explains —
the means of its levels — the part the interaction explains, and the residual; the bar chart says
what share of the variation each holds, and a permutation test whether that share is more than
shuffled labels would give (the bars are coloured by whether it is). Each part then gets a
principal-component model of its own, and the scores plot puts the injections on the chosen
effect's components with their residual added back, so the replicates' spread shows against the
separation the factor makes: two well-separated clouds are a factor the dataset answers; clouds
that overlap are a factor it does not, whatever a few single features say.

**Effect** chooses which part is drawn — factor A, factor B or the interaction — and the line
beside it gives the share, the permutation p, and how much of the effect its first two components
carry. A factor with two levels makes an effect with one component and no second; its scores then
take the residual's own first component as the vertical axis, and the line says so.

## Reading it

The ANOVA finds the features; ASCA says whether the design is visible in the dataset as a whole.
A factor with a hundred significant features and an ASCA share of 3 % at p 0.4 is a factor that
moved a few things a lot and nothing else; a factor with an ASCA share of 30 % at p 0.005 and few
significant features is one that moved everything a little, which the per-feature test cannot see
past the noise. Both are findings, of different kinds.

Time series with more than two points, repeated measures on the same subject, and designs with
three factors are not here; the two-class **Paired** test and this page's two factors are as far
as the design goes.
