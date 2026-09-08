---
title: Tags and verdicts
section: Reviewing
order: 21
summary: MS-DIAL's five review flags, the keyboard that sets them, Confirm and Reject, comments, bulk tagging, and where the review is stored.
---

# Tags and verdicts

The verdict on a feature is one or more of MS-DIAL's five flags. They have the same names and the
same numeric ids as in MS-DIAL, so a review done here shows up in the Windows application, and
one done there shows up here.

## The five tags

| Key | Tag | Means |
| --- | --- | --- |
| `Ctrl+1` | **Confirmed** | checked, and right |
| `Ctrl+2` | **Low quality spectrum** | too weak or noisy to judge |
| `Ctrl+3` | **Misannotation** | the name is wrong |
| `Ctrl+4` | **Coelution (mixed spectra)** | two compounds under one peak |
| `Ctrl+5` | **Overannotation** | an isotope, adduct or in-source fragment of something else, or a name claiming more than the spectrum shows |
| `Ctrl+0` | *(clear)* | removes every tag from the feature |

They are not exclusive: a feature can be both Coelution and Overannotation. Each key toggles its
tag; the verdict strip's buttons — **Confirmed**, **Low quality**, **Misannotation**,
**Coelution**, **Overannotation**, **Clear** — do the same with the mouse, and light up when the
tag is on. The ion table shows the tags in short form and, in its first column, `✓` for Confirmed,
`✕` for Misannotation and `●` for anything flagged to come back to.

## Confirm and Reject

**Confirm ▸** (`Alt+C`) sets Confirmed, clears Misannotation, and moves to the next feature.
**Reject ▸** (`Alt+X`) sets Misannotation, clears Confirmed, and moves on. Together with
**Next unreviewed** (`Alt+N`), which skips what is already decided, they are the whole of a fast
pass.

## Reviewed

A feature counts as **reviewed** when it carries any tag, or when the reviewer marked it so
without one. The filter band's **Reviewed** and **Not reviewed** states and the count in the band
read this. A feature with a comment but no tag is not reviewed.

## Comments and hand-picked names

The box under the verdict strip takes a free-text comment on the feature, shown in the ion table's
Comment column and written to the reviewed-table export. A name chosen from the candidate list
with **Use this annotation** replaces the run's name in the table and everywhere downstream, and
shows a **hand-picked** chip beside the feature's identity; **Back to the automatic name** undoes
it. See [[annotation]].

## Tagging many at once

**Confirm all shown** tags every feature the filter band is showing as Confirmed and clears
Misannotation on them; **Clear all shown** removes every tag from them. Reviewing a lipid class is
usually a matter of filtering to it, checking its retention-time trend in the feature map, and
accepting the whole set.

## Where the review is stored

**Save review** on the toolbar — or leaving the workspace with unsaved tags, which saves them too —
writes two files beside the alignment result:

- `<alignment>_tags.xml` — the tags, in MS-DIAL's own schema. MS-DIAL reads and writes this file; curation done on Windows shows up here, and curation done here shows up there.
- `<alignment>_curation.json` — the comments, the hand-picked names and the reviewed flags, which MS-DIAL keeps inside its binary alignment file. Writing them to a sidecar leaves the binary files untouched by a tag-only review.

When peaks were re-integrated or split, **Save review** also writes the alignment result itself
back, keeping the files as they were before the first edit of the session with a
`.before-curation` suffix; see [[reintegration]]. The dot on the button means there is something
unsaved. Both files are described in [[projects-and-files]].
