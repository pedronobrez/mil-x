---
title: OpenDIAL manual
section: Start
order: 1
summary: What OpenDIAL is, what this manual covers, and where to start reading.
---

# OpenDIAL manual

OpenDIAL is an open, cross-platform port of the **MS-DIAL 5** processing engine for untargeted
metabolomics and lipidomics — peak picking, MS/MS deconvolution, spectral-library annotation and
alignment — with a desktop application of its own for macOS, Linux and Windows. The engine is
MS-DIAL's, used unchanged. What OpenDIAL adds around it is an open raw-data layer that reads mzML
and SCIEX `.wiff` natively and bridges the other vendor formats, a review workspace built around
MS-DIAL's own curation loop, and a statistics workspace for seeing a dataset whole.

This manual is the complete reference for the application: every workspace, every control, every
file it reads and writes, every setting and environment variable, and what to do when something
goes wrong. It is the same text in the application's **Help** window and in the PDF, built from
one set of pages, and it is updated with every version — the [[versions]] page says what changed.

## The two languages

The manual exists in English and in Portuguese, page for page. The **English** / **Português**
button at the top of the Help window switches from one to the other on the same page, and the
choice is kept for next time. The names of the controls — buttons, tabs, menus — stay in English
in both, because that is how they appear on screen. Each language has its own PDF.

## How to read it

Pages link to one another in double brackets, the way an Obsidian vault does: [[concepts]] is a
link, and clicking it opens that page. At the bottom of every page, **Referenced by** lists the
pages that point at it, so the manual can be read in either direction. The search box at the top
of the Help window finds pages by any word in them, best match first, and marks the words on the
page it opens. `⌘F` focuses it; `Esc` clears it; `⌘[` and `⌘]` go back and forward.

Pressing **F1** in the application opens the manual at the page for the workspace that is showing.

## Where to start

- New to OpenDIAL: [[getting-started]], then [[concepts]].
- Coming from MS-DIAL on Windows: [[concepts#What is the same as MS-DIAL]], then [[review-tags]] and [[projects-and-files]] — your projects and your curation carry over.
- Setting up a batch: [[new-project-wizard]], [[samples-workspace]], [[method-workspace]], [[processing]].
- Reviewing a result: [[analytics-workspace]] is the overview; [[ion-table]], [[review-tags]], [[evidence-panels]], [[reintegration]] and [[annotation]] go one level down.
- Looking at the dataset as a whole: [[statistics-workspace]], then [[one-factor-analysis]], [[internal-standards]], [[pathways]] and, for a design with two factors, [[two-factor-analysis]]; the charts leave as SVG or PNG, see [[chart-export]].
- Getting data out: [[exports]] and [[openquant]].
- Something is wrong: [[troubleshooting]].

## The pages

**Start** — [[getting-started]] · [[concepts]] · [[shell]]

**Workspaces** — [[new-project-wizard]] · [[samples-workspace]] · [[method-workspace]] · [[processing]] · [[explorer-workspace]] · [[analytics-workspace]] · [[statistics-workspace]] · [[one-factor-analysis]] · [[internal-standards]] · [[pathways]] · [[two-factor-analysis]]

**Reviewing** — [[ion-table]] · [[review-tags]] · [[evidence-panels]] · [[reintegration]] · [[annotation]] · [[exports]] · [[chart-export]]

**Data and files** — [[projects-and-files]] · [[raw-data-formats]] · [[openquant]] · [[caches-and-storage]]

**Reference** — [[settings]] · [[keyboard-shortcuts]] · [[method-parameters]] · [[environment-variables]] · [[command-line]] · [[glossary]]

**Under the hood** — [[algorithms]] · [[architecture]] · [[building-and-testing]] · [[biopan-plan]]

**Help** — [[troubleshooting]] · [[versions]] · [[about]]
