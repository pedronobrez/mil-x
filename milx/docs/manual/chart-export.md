---
title: Charts, their options and their export
section: Reviewing
order: 26
summary: What every chart can be told about its title, labels, sizes, palette and colour scale, and how it leaves the application as a figure — SVG or PNG, light or dark, on paper or on nothing.
---

# Charts, their options and their export

Every chart in the [[statistics-workspace]] sits in a frame with two controls at its top right:
**⚙** opens the chart's options, and **Export…** writes it out as a picture. The frame does not
care which chart it holds; the same two controls work on the scatter plots, the box plots, the bar
charts, the heatmaps, the dendrogram and the network.

Every other chart in the application — the chromatogram and the spectrum in the
[[explorer-workspace]], the peak panels, the mirror, the isotope envelope, the feature map and the
abundance bars of the [[analytics-workspace]] — has the export without the frame: **right-click the
chart** and choose **Export as a picture…**. The panels there are full of evidence and have no room
for a strip of buttons, so the command lives in the menu instead. On a chart where the right button
pans, a right *drag* still pans; only a right click that does not move offers the menu.

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
| **Legend** | the legend of the groups; it sits in whichever corner of the plot has the least under it, so it never covers the named points of a volcano plot | scatter, line |
| **Labels** | the names beside the points, or the row labels of a heatmap | scatter, heatmap |
| **Ellipses** | the 95 % confidence ellipse of each class on a score plot | scatter |
| **Trees** | the dendrograms beside a heatmap | heatmap |
| **Values** | the numbers printed in the cells | heatmap, chain map |

An option applies to that chart and stays while the workspace is open; it is not written
anywhere. The options a chart does not have are greyed out.

## Export

**Export…**, or **Export as a picture…** in a chart's own menu, opens one dialog: the figure as it
will be written, beside the choices that make it.

![the export dialog](images/export-dialog.png)

| Choice | What it does |
| --- | --- |
| **Format** | **SVG**, a vector figure, or **PNG**, a picture |
| **Resolution** | PNG only: 2×, 3×, 4× or 6× the chart's size on screen |
| **Theme** | **Light**, **Dark**, or **As on screen** |
| **Background** | **Paper** (the theme's own surface), **White**, or **Transparent** |
| **Type size** | every piece of text on the figure together, 0.7 to 2.2 of what the screen shows |

**The theme is the figure's, not the window's.** A reviewer working in the dark theme still wants a
light figure for a paper, a slide or a printer, and should not have to switch the whole application
to get one — so **Light** is what the dialog opens with, however the application is set. **As on
screen** keeps the window's theme, which is what a figure for a dark slide deck wants. Everything
else about the chart — the palette, the labels, the grid, the legend, the ellipses — is exactly as
the chart options have it; only the ink, the rules and the paper change.

**Transparent** writes a PNG with nothing behind the figure, for dropping onto a coloured slide. An
SVG asked for the same way is written without its background rectangle.

The preview is the export: the same chart drawn through the same code with the same settings, so
what is in the dialog is what lands in the file. The line beside the buttons says how big the file
will be — in pixels for a PNG, in points for an SVG, which can then be used at any size. The
choices are remembered, in the settings, for the next figure.

**SVG** is the one to take to a figure. Every line is a line, every point a circle, every label a
text element in the chart's font, so it opens in Illustrator, Inkscape or a browser at any size
with nothing to redraw, and the labels can be edited in place. **PNG** is the same picture
rasterised: a chart 600 points wide on screen is 2400 pixels at 4×, which at 300 dpi is an
eight-inch figure. Both are drawn by the code that draws the screen, so a figure at 6× is the
figure on screen, only larger.

## From a script

The driving channel described in [[building-and-testing#The smoke test]] exports a chart without
either dialog: `exportChart` takes the `path`, a `page` and `index` for a chart of a statistics
page or a `control` for a named chart of any workspace, and the same `format`, `scale`, `theme`,
`background` and `fontScale` the dialog offers. `ChartExport.Save` is the one call behind all of
it, and the tests write figures both ways — light from a dark window, on white and on nothing — to
prove they come out as asked.
