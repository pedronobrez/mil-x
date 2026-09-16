---
title: Settings
section: Reference
order: 40
summary: The Settings window — theme, the survey-scan cache, the msconvert bridge — and the settings file behind it.
---

# Settings

**File ▸ Settings…** opens a small window with three sections. **Save** writes them and applies the
theme; **Cancel** leaves everything as it was. The path of the settings file is shown at the bottom.

## Appearance

**Theme**: **Follow the system**, **Light** or **Dark**. The whole interface follows, charts
included. `MILX_THEME=dark` or `light` forces one for a session without touching the setting.

## Survey scan cache

Reading a vendor file takes about as long every time it is opened, so the spectra of each file are
kept here after the first read and reopening a project draws its chromatograms straight away.
The line shows how many files are cached and how much space they take, with the folder; **Empty
it** deletes them. Nothing else is stored, and emptying it only costs one slow read per file. See
[[caches-and-storage]].

## Vendor format conversion

Thermo `.raw`, Agilent and Bruker `.d`, Shimadzu `.lcd` and SCIEX `.wiff2` — and `.wiff` when the
native reader is not present — are converted to mzML with ProteoWizard's msconvert before
processing.

| Setting | Meaning |
| --- | --- |
| **msconvert path** | a native msconvert executable; empty means "look on the PATH" |
| **Use Docker instead of a native msconvert** | run the ProteoWizard Docker image |
| **Docker image** | which image; the default bundles the vendor readers under Wine |
| **Conversion cache** | where the converted mzML files go; empty means beside the source file |

See [[raw-data-formats#The msconvert bridge]] for what each route needs.

## The settings file

`settings.json` under `~/.config/MIL-X` on macOS and Linux, `%APPDATA%\MIL-X` on Windows,
or the folder `MILX_SETTINGS_DIR` names. Besides the three sections above it remembers what the
application learns from use:

| Field | What it remembers |
| --- | --- |
| `RecentProjects` | the last ten projects, for the File menu |
| `LastInputFolder`, `LastOutputFolder`, `LastProjectFolder` | where the file dialogs open |
| `LastMspFile`, `LastMethodFile` | the library and method to offer the next project |
| `ShowLog` | whether the log panel was open |
| `IonTableColumnOrder` | the order the reviewer arranged the ion table's columns in |
| `IonTableDetached` | whether the ion table was torn off into its own window |
| `HelpLanguage` | the language the manual was last read in, `en` or `pt` |

A corrupt file is ignored and the defaults used. Deleting it resets everything above.
