---
title: The two polarities of one batch
section: Reviewing
order: 27
summary: Pairing the positive and the negative run of the same samples — how compounds are matched on the neutral molecule, what the ion table then shows, and which side quantifies.
---

# The two polarities of one batch

A phospholipid answers in positive and a free fatty acid in negative, so the two runs of one batch
are two halves of the same picture. Until they are linked they are two projects, two alignments and
two reviews, joined by hand in a spreadsheet.

**Link polarity…** in the [[analytics-workspace]] toolbar asks for the alignment result of the same
batch run the other way — the `.arf2` in its output folder — and reconciles the two, compound by
compound. The button then reads **Unlink polarity**, which forgets the pairing without touching
anything on disk.

## What is matched, and on what

Not the m/z: the same molecule has a different m/z in each polarity. What is matched is the
**neutral molecule** behind the ion. The same compound is `[M+H]+` here and `[M-H]-` there, 2.0146
daltons apart, and the neutral mass behind each is the same number.

Three things have to agree before two features are called one compound — the same three as the ion
identity grouping one polarity over, described in [[annotation#One compound, several ions]]:

| | |
| --- | --- |
| **the neutral masses meet** | within 0.01 Da, after each ion is turned into its neutral by the adduct the run assigned it. An adduct the table does not know leaves the neutral blank rather than guessing a proton |
| **they elute together** | within 0.1 minute, which is what the same LC method run twice gives |
| **their heights agree** | the two runs are the *same* samples, so a compound's height profile across the injections is a signature. Profiles that contradict each other are refused |

Each feature is spoken for once: the strongest offer wins, and the second-best offer for the same
feature is a coincidence, not a second molecule.

## What the ion table then shows

Four columns, described with the others in [[ion-table#The columns]]:

- **Neutral** — the neutral mass behind the ion. It is filled in whether or not a polarity is
  linked, because it is what identifies a compound rather than an ion.
- **Pol** — `±` when both runs saw this compound, `+` or `−` when only this one did.
- **Other polarity** — what the other run saw here: its name when it had one, its m/z, its
  retention time, and the correlation between the two height profiles.
- **Quantify** — which side carries the number.

The **polarity** box in the filter band narrows the table to **Seen in both** or **Only in this
polarity**. It is enabled only once a polarity is linked.

## Reading both spectra at once

The evidence tabs gain **Other polarity**: this run's product spectrum above, the other run's
below, for the same compound. Turn on **Split evidence** and the workspace puts MS/MS on the left
and Other polarity on the right — which is what linking a polarity is for. The second column
selects that tab on its own the first time a polarity is linked.

The two halves are **not** expected to match peak for peak: a protonated molecule and a
deprotonated one fall apart differently, so this is not a score and nothing is computed from it.
What agrees is the neutral mass, the retention time and the way the heights rise and fall across
the injections — and what you get on screen is the same molecule fragmented twice, which is a
different order of confidence from one spectrum alone.

Under the mirror is the partner's line: its name in that run, its m/z and adduct, its retention
time, its signal-to-noise, the correlation between the two height profiles, the neutral mass, and
which run quantifies the compound.

## One compound, one verdict

**Tag both polarities** in the toolbar, on by default once a polarity is linked, carries the review
across the pair: confirming a compound here confirms it in the other run, and rejecting it rejects
it there. Features only one polarity saw are left alone.

Undo works across both. Taking a decision back here takes it back there, in the same keystroke,
because what travels is the whole tag state of a paired feature rather than each edit — so the two
reviews cannot drift apart.

Both reviews are written by **Save review**: this run's to its own `_tags.xml`, the other run's to
its own, each still readable by MS-DIAL.

Switch the toggle off to review the two polarities independently. Note what the default means: a
tag the other run already carried on a paired feature is replaced by this one, because the pair is
one compound and one compound has one verdict.

## Which side quantifies

The one that measured the compound better, by signal-to-noise, with the height as the tie-break.

Heights are **never added across the polarities**. The ionisation efficiencies are different, so
the sum of a positive height and a negative height is not a quantity of anything. One side carries
the number and the other confirms the identification.

## One project, one run

A batch acquired both ways does not need two projects. The Samples workspace has a **Polarity**
column, guessed from each file's name — `…_pos`, `…-NEG` — and editable where the guess is wrong.
A marker has to be a word of its own: `regeneration.wiff` is not negative.

When the batch holds both, **Process batch** runs twice: the positive injections into
`positive/` under the output folder, the negative into `negative/`, each with its own ion mode.
The two results are then paired automatically, and the workspace opens on the positive run with
the link already made. A batch that is all one polarity runs exactly as it always did.

## The statistics see compounds, not ions

With a polarity linked, every model — the PCA, the clustering, the heat map, the volcano, the
tests — is computed on the two runs reconciled: a compound both polarities saw appears **once**,
with the numbers of the run that measured it better, on the injections both runs share. What only
one polarity saw comes across as it is.

This is not tidiness. A matrix where half the rows are copies of the other half fits every model on
a lie: the clustering finds the copies, the principal components spend themselves on them, and the
false-discovery correction is applied to a feature count that is not real.

An injection the pairing could not match reads as *not measured* rather than zero — the difference
between no number and a number that says nothing is there.

The statistics summary says what it is working on: `2,140 compound(s) across 8 injection(s) · 1,806
measured in both polarities · 231 positive only · 103 negative only`.

## Where the pairing is kept

Beside the alignment, as `<positive alignment>_polarity-pairs.json` — plain JSON, in the same habit
as the curation sidecar described in [[projects-and-files]]. It holds the tolerances it was made
with, because a pairing whose parameters are lost is not a result.

A result that was linked opens linked: the pairing is *made again* from the two alignments rather
than trusted from the file, because the feature ids in it mean nothing if either result has been
processed again since.

The [[exports#The run report]] names the pairing when there is one.

## What this does not do yet

The compounds only the other polarity saw are **counted, not listed**: the run you opened is still
the spine of the review, and its ion table shows its own features. Reviewing the other run's
features means opening that result. The same is true of the statistics, which are still computed on
one alignment at a time.
