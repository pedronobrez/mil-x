---
title: Charts, their options and their export
section: Reviewing
order: 26
summary: What every chart in the Statistics workspace can be told about its title, labels, sizes, palette and colour scale, and how it is written out as SVG or PNG.
---

# Charts, their options and their export

Every chart in the [[statistics-workspace]] sits in a frame with two controls at its top right:
**⚙** opens the chart's options, and **Export…** writes it out as a picture. The frame does not
care which chart it holds; the same two controls work on the scatter plots, the box plots, the bar
charts, the heatmaps, the dendrogram and the network.

## The options

| Option | What it changes | Which charts |
| --- | --- | --- |
| **Title** | a title drawn above the plot; empty by default, because the eyebrow above the frame already says what the chart is, and a figure in a paper wants its own | all |
| **X label**, **Y label** | the axis labels, prefilled with the chart's own | scatter, box, bar, rank |
| **Point size** | the radius of a point (4.2 by default; 1.5 to 12) | scatter, box |
| **Font scale** | every piece of text on the chart together (1 by default; 0.7 to 2.2) — turn it up for a figure that will be shrunk | all |
| **Palette** | the colours of the groups: **Tableau** (the default, ten colours), **Okabe-Ito** (eight, safe for every kind of colour blindness), **Grey** (for a black-and-white figure) or **Accent** | scatter, box, bar, rank |
| **Colour scale** | the scale of a heatmap: **Blue–white–red** (diverging, for z-scores and fold changes, white at zero), **Viridis** (sequential, perceptually uniform), **Greys**, or **Green–black–red** (the microarray convention) | heatmap, chain map, correlations |
| **Grid** | the grid lines behind the plot | scatter, box, bar |
| **Legend** | the legend of the groups | scatter, line |
| **Labels** | the names beside the points, or the row labels of a heatmap | scatter, heatmap |
| **Ellipses** | the 95 % confidence ellipse of each class on a score plot | scatter |
| **Trees** | the dendrograms beside a heatmap | heatmap |
| **Values** | the numbers printed in the cells | heatmap, chain map |

An option applies to that chart and stays while the workspace is open; it is not written
anywhere. The options a chart does not have are greyed out.

## Export

**Export…** offers **SVG** and **PNG at 2×, 4× and 6×** the on-screen size, and asks where to
save; the suggested name is the chart's eyebrow.

**SVG** is the one to take to a figure. It is drawn from the same code that draws the screen —
every line is a line, every point a circle, every label a text element in the chart's font — so
it opens in Illustrator, Inkscape or a browser at any size with nothing to redraw, and the labels
can be edited in place. The background is the chart's own (white in the light theme, the panel
colour in the dark one).

**PNG** is the same picture rasterised at a multiple of the screen's resolution, so a chart 600
pixels wide on screen is 2400 pixels at 4×, which at 300 dpi is an eight-inch figure. The 6× is
for a full-page one. Both are written with the options as they are set at that moment: set the
font scale and the palette first, then export.

## From a script

The frames are visible to the driving channel described in [[building-and-testing#The smoke test]],
and a chart can be exported without the save panel by naming the path;
`ChartExport.SaveSvg` and `ChartExport.SavePng` are the two calls, and the tests write every
chart of every page both ways to prove they can.
