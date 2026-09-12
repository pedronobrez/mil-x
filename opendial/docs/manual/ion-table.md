---
title: The ion table
section: Reviewing
order: 20
summary: Every aligned feature as a row — the columns, sorting and reordering, the verdict glyphs, the abundance bars, and the tear-off window.
---

# The ion table

Every aligned feature that passes the filter band, one per row, sortable on any column. It is the
spine of the [[analytics-workspace]]: the selected row is what every evidence panel shows and what
every tag lands on.

## The columns

| Column | What it holds |
| --- | --- |
| *(verdict)* | the review at a glance: `✓` Confirmed, `✕` Misannotation, `●` flagged to come back to (Low quality, Coelution or Overannotation); sortable by the tags |
| **ID** | the feature's alignment id, the number MS-DIAL and every export use |
| **RT** | the mean retention time across the injections, in minutes |
| **m/z** | the mean m/z, to four decimals |
| **Annotation** | the name — the reviewer's when one was hand-picked, otherwise the run's; `Unknown  m/z 760.5851` when there is none |
| **Level** | confident, suggested, m/z only, or blank; see [[concepts#Annotation levels]] |
| **Adduct** | `[M+H]+`, `[M+Na]+`, … |
| **Class** | the ontology MS-DIAL gave the compound, which for a lipid is its class |
| **Fill %** | the share of injections in which the peak was detected rather than gap-filled |
| **MS/MS** | `MS/MS` when at least one injection carried a product spectrum |
| **S/N** | the mean signal-to-noise |
| **Score** | the total match score of the reported annotation |
| **Height** | the mean apex height |
| **Iso** | `M` for a monoisotopic ion, `M+1`, `M+2` for a feature the run marked as an isotope of another; blank when it did not decide |
| **Abundance** | one bar per sample class, the mean height of that class — so a feature as high in the blanks as in the samples stands out while scrolling, without opening a panel; sorts by height |
| **Tags** | the tags, short form |
| **Comment** | the reviewer's comment |

Columns can be dragged into whatever order suits the work, resized, and sorted by clicking their
header; the order comes back on the next start.

## The header buttons

| Button | What it does |
| --- | --- |
| **▲** / **▼** | previous and next feature (`Alt+↑`, `Alt+↓`) |
| **Next unreviewed** | the next feature with no verdict, wrapping round (`Alt+N`); says so when there is none left in the filter |
| **Confirm all shown** | tags every feature the filter is showing as Confirmed, which is how a whole lipid class is accepted once its retention trend checks out |
| **Clear all shown** | removes every tag from the features shown |
| **Open in a window** / **Dock table** | tears the table off into a window of its own, or brings it back |

## The tear-off window

**Open in a window** moves the table into a second window, for a second screen. It hosts the same
control bound to the same state, so the selection, the filters and the tagging stay in step with
the panels on the main window; nothing about the review moves with it. While it is away, the peak
grid and the evidence take the whole width of the main window; the table's column does not stand
empty.

Three ways bring it back: **Dock table** in either window, closing the window, or dragging the
window by its title bar over the left part of the main window. As the title bar crosses the left
two fifths of the main window, the place the table will take lights up there — *Release to dock the
ion table here* — and letting go of the mouse there docks it; letting go anywhere else leaves the
window where it was dropped. Whether the table was torn off is remembered for the next start,
with the column order of each.

## Selection and navigation

A click selects; `Alt+↓` and `Alt+↑` move; `Alt+N` skips to the next unreviewed. The selected row
is kept when the filter changes and still contains it, and moves to the first row when it does not.
Clicking a dot in the feature map, a row of the VIP or S-plot tables, or a node of the molecular
network selects that feature here, clearing whatever filter hid it.

## What the table does not show

Per-injection numbers — height, area, retention time, integration window, gap filling — are in
the **Samples** evidence tab, and the per-class mean, standard deviation and CV in the
**Statistics** tab; see [[evidence-panels]].
