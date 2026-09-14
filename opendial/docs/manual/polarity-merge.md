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

## Which side quantifies

The one that measured the compound better, by signal-to-noise, with the height as the tie-break.

Heights are **never added across the polarities**. The ionisation efficiencies are different, so
the sum of a positive height and a negative height is not a quantity of anything. One side carries
the number and the other confirms the identification.

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
