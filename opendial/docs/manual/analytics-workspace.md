---
title: The Analytics workspace
section: Workspaces
order: 15
summary: The review of a processed result — how the workspace is laid out, its toolbar and filter band, and where each part is explained.
---

# The Analytics workspace

Processing a lipidomics batch produces a few thousand aligned features, most of them wrong in some
way. What turns that into a result is the review pass, and MS-DIAL's is the one analysts know.
This workspace follows it: the ion table is the spine on the left, the evidence for the selected
feature is on the right, and the verdict is one keystroke away between them. `⌘2` shows it, and a
processed project opens on it.

![the review workspace](images/review-ion-table.png)

## The layout

- **The toolbar** — the run and the exports.
- **The filter band** — what the ion table shows.
- **The ion table** — every feature that passes the filter; see [[ion-table]]. It can be torn off into a window of its own.
- **The identity** — the selected feature's name, id, retention time, m/z, adduct, formula, class, score and fill, with a chip for its annotation level and one reading **hand-picked** when the reviewer chose the name.
- **The verdict** — the five tags, Confirm and Reject, and a comment box; see [[review-tags]].
- **The peak in every sample** — the feature's chromatogram in each injection, always on screen, with the integrate strip above it; see [[evidence-panels#Peaks]] and [[reintegration]].
- **The evidence tabs** below it — MS/MS first, then Isotopes, Candidates, Abundance, Feature map, Samples, Statistics, Trend; see [[evidence-panels]]. A divider between the two sets how much of the height each takes.

A result opens on the three things a reviewer needs to confirm an analyte: the ion table, the peak
in every sample, and the product spectrum against the library. Nothing has to be clicked open
first.

## The toolbar

| Control | What it does |
| --- | --- |
| **Process batch** | runs the pipeline (`⌘R`); see [[processing]] |
| **Re-export…** | the height and area matrix of every feature, as TSV |
| **Export to OpenQuant…** | the listed features as an OpenQuant component list; see [[openquant]] |
| **Export reviewed table…** | the listed features with the review as columns; see [[exports]] |
| **Open output folder** | the results folder in Finder |
| **Save review** | writes the tags to `<alignment>_tags.xml` — the file MS-DIAL reads — and the rest of the curation beside it; a dot on the button means there is something unsaved |

The summary on the right reads `2235 aligned spots across 8 sample(s) · 1804 annotated`, and after
an edit or a save, what was done.

## The filter band

The filters narrow the ion table and everything downstream of it — the feature map, the exports,
**Confirm all shown** — and they combine.

| Filter | What it is for |
| --- | --- |
| free text | name, class (ontology), adduct, formula, comment, an exact feature id, or an m/z written to four decimals |
| **m/z** from–to, **RT** from–to | the region of the map you are working through |
| annotation | **All**, **Confident**, **Suggested** (a low-score or m/z-only name), **Annotated** (any name), **Unknown** |
| class | one lipid class or ontology at a time, which is how a class-by-class pass runs |
| review state | **All**, **Untagged**, **Reviewed**, **Not reviewed**, or one specific tag |
| **MS/MS only** | drop the features annotated on mass alone |
| **Molecular ion** | drop the features the run marked as an isotope of another |
| **Hand-edited** | only what a reviewer changed, in the annotation or the integration |

The counts on the right are the confirmed features (accent pill), the misannotations (grey pill),
and `N of M features · R reviewed`. On a laptop screen the band scrolls sideways rather than
covering the counts.

The selected feature is kept when it survives a tighter filter, so narrowing while reviewing does
not throw the reviewer out of place. Jumping to a feature from the statistics workspace or the
network clears whatever filter hides it.

## The review loop, in short

1. Filter to a class, or to **Not reviewed**.
2. Read the peak grid, the MS/MS, the candidates.
3. `Alt+C` to confirm and move on, `Alt+X` to reject and move on, `Ctrl+1` to `Ctrl+5` for a specific tag, a comment if it needs one.
4. `Alt+N` to the next feature nobody has looked at.
5. **Save review**, or just leave: the review is also written when the workspace is left with unsaved tags.

Everything about the loop is in [[review-tags]].

## Results opened without a project

A results folder opened with **Open results folder…**, or an alignment read from an `.mdalign`
export rather than the binary files, can be reviewed and tagged but not edited: re-integration and
splitting need the alignment container, and the status bar says so.
