---
title: The New project wizard
section: Workspaces
order: 10
summary: The four steps that create a project — name and folder, samples, method, summary — and what each writes.
---

# The New project wizard

**File ▸ New project…** (`⌘N`) opens a four-step wizard. It writes the project file when it
finishes, not before, and every later change is saved back into that file. The steps are
**Project**, **Samples**, **Method** and **Summary**; **Back** and **Next** move between them,
**Cancel** leaves nothing behind, and the last step's button reads **Create project**.

## Step 1 — Project

A **Name** and a **Folder**. The project is created as `<Folder>/<Name>/<Name>.odproj`, with a
`results` folder beside it for everything the run writes; characters a file name cannot hold are
replaced by underscores. **Browse…** picks the folder; the preview underneath shows the exact path.
Raw files stay where they are — the project records their paths, relative when they are nearby.

Next is enabled once there is a name and the folder exists.

## Step 2 — Samples

**Add data files…** picks raw files; **Add folder…** takes every supported raw file directly inside
a folder. A `.wiff` batch that holds several samples becomes one row per sample, named after the
sample inside the batch. **Remove** drops the selected row. The table shows the **File**, an
editable **Sample** name, the **Type** (Sample, Blank, QC, Standard) and the **Class**, and a
**Format** badge — `mzML`, `wiff · native`, `raw · msconvert` — that says how the file will be
read; a file that needs msconvert is counted in the summary line so you know before the run.
Everything here can be changed later in the [[samples-workspace]].

Next needs at least one file.

## Step 3 — Method

Where the processing method comes from:

- **Defaults for LC-MS (DDA, positive, centroid)** — MS-DIAL's LC-MS defaults; see [[method-parameters]].
- **Defaults for GC-MS (EI, RT alignment)** — the GC-MS branch with retention-time alignment.
- **From a method file** — a console-compatible `.txt`, for instance one saved from an earlier project or produced from an MS-DIAL parameter export by `tools/msdial_param_to_method.py`.

**Library** is the MSP spectral library the annotation searches; it is optional, and without one
every feature stays unknown. The library path is remembered for the next project. The method can be
edited afterwards in the [[method-workspace]].

## Step 4 — Summary

The path, the sample count and class count, the method source and the library, in one block.
**Process the batch right away** (ticked by default) starts the run as soon as the project is
written; otherwise the project opens on the Samples workspace and `⌘R` starts it later. See
[[processing]].

## What it writes

The `.odproj` file described in [[projects-and-files]]: the name, the mode (LC-MS or GC-MS), the
results folder, the method text, and the samples with their paths, names, types, classes,
acquisition types and order. The wizard also remembers its folder, the last method file and the
last library in the application's settings, so the next project starts where this one did.
