---
title: The window
section: Start
order: 4
summary: The menus, the workspace tabs, the status bar, the log, the progress band, and how the window handles unsaved work.
---

# The window

One window holds one session: one project, one batch, one result. The workspaces are tabs along
the top; the menu bar, the log and the status bar belong to the window and stay the same whichever
workspace is showing.

## Workspace tabs

**Explorer**, **Analytics**, **Method**, **Samples**, **Statistics**, in that order, selected with
a click or with `⌘1` to `⌘5` (`Ctrl+1` to `Ctrl+5` on a keyboard without a command key). To the
right of the tabs the project name is shown, with a **results loaded** badge once there is a
result. Each workspace has its own page: [[explorer-workspace]], [[analytics-workspace]],
[[method-workspace]], [[samples-workspace]], [[statistics-workspace]].

Opening a processed project lands on Analytics; an unprocessed one on Samples; a raw file on
the Explorer; starting a run switches to Analytics.

## The title and the dirty marker

The title reads `OpenDIAL — <project name>`, with a `•` after it while the project has changes that
are not saved: an edited batch, an edited method, a run whose project has not been written. Closing
the window, opening another project or starting a new one while the marker is up asks **Save this
project first?** with **Cancel**, **Discard** and **Save**.

## Menus

### File

| Item | Shortcut | What it does |
| --- | --- | --- |
| New project… | `⌘N` | the [[new-project-wizard]] |
| Open project… | `⌘O` | an `.odproj` or an `.mdproject` |
| Open results folder… | | a folder written by a run, the console or MS-DIAL, with no project file; the batch is rebuilt from the result files |
| Recent projects | | the last ten, newest first; a path that no longer exists is dropped from the list when chosen |
| Save project | `⌘S` | writes the `.odproj`; asks for a location the first time |
| Save project as… | | writes it somewhere else; the results folder defaults to `results` beside it |
| Add data files… | | raw files into the batch, one row per injection |
| Add folder… | | every supported raw file directly inside a folder; Agilent and Bruker `.d` folders can only be added this way, since Finder cannot hand a folder to an application |
| Import OpenQuant batch (.oqproj)… | | the samples of an OpenQuant project with their types, groups, dilutions and comments; see [[openquant]] |
| Close project | | empties the session |
| Settings… | | theme, cache, msconvert; see [[settings]] |
| Quit | `⌘Q` | |

### View

| Item | Shortcut | What it does |
| --- | --- | --- |
| Log | `⌘L` | shows or hides the log panel under the workspace |
| Progress band | | hides the band once a run is over |
| Explorer: uncheck all channels | | clears every ticked channel in the Explorer tree |
| Explorer: show TIC of every sample | | expands every file and ticks its TIC |
| Explorer: collapse tree | | |

### Process

| Item | Shortcut | What it does |
| --- | --- | --- |
| Process batch | `⌘R` | runs the pipeline on the batch; see [[processing]] |
| Cancel run | | stops it at the next checkpoint |
| Re-export alignment matrix… | | a height and area matrix of every feature against every injection, as TSV; see [[exports]] |
| Export to OpenQuant… | | the listed features as an OpenQuant component list; see [[openquant]] |
| Open output folder | | the results folder in Finder |
| Choose output folder… | | where the next run writes |
| Load method file… | | replaces the method with a file's contents; see [[method-workspace]] |
| Save method file… | | writes the current method as a console-compatible file |

### Workspace

The five workspaces with their shortcuts.

### Help

| Item | Shortcut | What it does |
| --- | --- | --- |
| OpenDIAL manual | `F1` | this manual, at its first page |
| Help for this workspace | `⌘⇧?` | the page for the workspace that is showing |
| Keyboard shortcuts | | [[keyboard-shortcuts]] |
| Troubleshooting | | [[troubleshooting]] |
| About OpenDIAL | | the version, the MS-DIAL version it tracks, the runtime, the license |

`F1` also opens the page for the current workspace when the manual is already open.

## The progress band

While a run is going a band appears under the tabs: the stage (Setup, Converting, Processing,
Export, Alignment, Project, Done), the file being worked on, the last message, the overall
percentage, a **Cancel** button. Nothing is modal: the workspaces stay usable while the engine
works. The band stays after the run so the summary can be read; the `✕` or **View ▸ Progress band**
hides it.

## The log

`⌘L` opens a panel under the workspace with every line the engine printed, time-stamped, in a
monospace face: which library loaded and how many records, which files were read natively and
which were converted, the per-file progress, the alignment count, the exported files, and any error
in full. It holds the last five thousand lines. When a run fails the log opens by itself. Lines
can be selected and copied.

## The status bar

The left side is the last thing the application did or is doing — `Opened 8 file(s) from …`,
`Processing…`, `Review saved to …`, `Export failed: …`. The right side is the results folder.
Most workspaces also have a status line of their own under their content: the Samples workspace
counts its injections by type, the Method workspace summarises the method.

## Documents from Finder

The bundle registers `.odproj`, `.mdproject`, `.oqproj`/`.opvproj` and the raw formats (`.mzML`,
`.wiff`, `.wiff2`, `.raw`, `.abf`, `.ibf`, `.cdf`, `.lcd`, `.qgd`). Opening one from Finder, on a
cold start or while the application is running, does what the extension says: a project opens as a
project, an OpenQuant batch is imported, a raw file goes to the Explorer.

## Themes

Light, dark, or following the system, from [[settings]]. The whole interface — charts included —
follows the choice.
