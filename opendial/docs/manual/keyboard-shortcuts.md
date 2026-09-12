---
title: Keyboard shortcuts
section: Reference
order: 41
summary: Every shortcut in the application, the review keys, the chart gestures, and the manual's own keys.
---

# Keyboard shortcuts

`⌘` is the command key on a Mac; on Linux and Windows read it as `Ctrl`. Where both are listed,
both work everywhere.

## The window

| Shortcut | Action |
| --- | --- |
| `⌘1` / `Ctrl+1` | Explorer workspace |
| `⌘2` / `Ctrl+2` | Analytics workspace |
| `⌘3` / `Ctrl+3` | Method workspace |
| `⌘4` / `Ctrl+4` | Samples workspace |
| `⌘5` / `Ctrl+5` | Statistics workspace |
| `⌘N` | New project… |
| `⌘O` | Open project… |
| `⌘S` / `Ctrl+S` | Save project |
| `⌘R` | Process batch |
| `⌘L` | show or hide the log |
| `F1` | the manual, at the page for the current workspace |
| `⌘⇧?` | the same |
| `⌘Q` | Quit |

## Reviewing (Analytics workspace)

| Shortcut | Action |
| --- | --- |
| `⌘⇧1` / `Ctrl+Shift+1` | toggle **Confirmed** |
| `⌘⇧2` / `Ctrl+Shift+2` | toggle **Low quality spectrum** |
| `⌘⇧3` / `Ctrl+Shift+3` | toggle **Misannotation** |
| `⌘⇧4` / `Ctrl+Shift+4` | toggle **Coelution (mixed spectra)** |
| `⌘⇧5` / `Ctrl+Shift+5` | toggle **Overannotation** |
| `⌘⇧0` / `Ctrl+Shift+0` | clear every tag on the feature |
| `⌘⇧C` / `Ctrl+Shift+C` | **Confirm ▸** — Confirmed, and the next feature |
| `⌘⇧X` / `Ctrl+Shift+X` | **Reject ▸** — Misannotation, and the next feature |
| `⌘⇧N` / `Ctrl+Shift+N` | **Next unreviewed** |
| `⌘⇧↓` / `Ctrl+Shift+↓` | next feature |
| `⌘⇧↑` / `Ctrl+Shift+↑` | previous feature |

The review keys are the workspace keys with the shift key added: `⌘1` is a workspace, `⌘⇧1` is a
tag. They work whatever has the focus — the table, a chart, a text box — in the main window while
Analytics is showing and in the ion table's own window, and do nothing elsewhere. See
[[review-tags]].

## Charts

The same gestures in every chart — chromatograms, spectra, scatter plots:

| Gesture | Action |
| --- | --- |
| drag | zoom the X axis to the dragged range |
| wheel | zoom in and out around the pointer |
| right-drag | pan |
| double-click | fit everything again (**Overview** in the Explorer does the same) |
| `⇧`-drag | select a range: a retention-time range in the Explorer, an integration window in a peak panel |
| click | select the nearest scan (Explorer), sample (peak grid, trend) or feature (feature map, S-plot, loadings) |
| hover | a tooltip with the values under the pointer |

In the Explorer, **Select range** makes a plain drag select instead of zoom.

## The manual

| Shortcut | Action |
| --- | --- |
| `⌘F` / `Ctrl+F` | focus the search box |
| `Esc` | clear the search |
| `⌘[` / `Alt+←` | back |
| `⌘]` / `Alt+→` | forward |

## The Samples table

`⇧`-click selects a range of rows, `⌘`-click adds one; **Set type of selected** and **Set class of
selected** then apply to all of them.
