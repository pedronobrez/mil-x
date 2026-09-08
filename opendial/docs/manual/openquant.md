---
title: OpenQuant
section: Data and files
order: 32
summary: What OpenDIAL shares with OpenQuant, importing an OpenQuant batch, and exporting a component list for targeted quantitation.
---

# OpenQuant

OpenQuant is a targeted-quantitation application; OpenDIAL is untargeted processing on the
MS-DIAL engine. They are meant to feel like two workspaces of one suite — the same palette, the
same shell of tabs and bands, the same native `.wiff` reading — and the workflow between them is
*discover in OpenDIAL, quantify in OpenQuant*.

## Importing an OpenQuant batch

**File ▸ Import OpenQuant batch (.oqproj)…**, the **Import OpenQuant batch…** button in the
Samples workspace, or opening an `.oqproj` or `.opvproj` from Finder, adds the samples of the
OpenQuant project to the batch with what OpenQuant knew about them:

| OpenQuant | OpenDIAL |
| --- | --- |
| path, sample index | the raw file and, for a multi-sample `.wiff`, the right sample |
| name | the sample name |
| sample type `Quality Control` | type **QC** |
| `Blank`, `Double Blank`, `Solvent` | type **Blank** |
| `Standard` | type **Standard** |
| anything else | type **Sample** |
| sample group | the **class**; the sample type when there is no group |
| dilution factor, comment | the same columns |

Paths in the project are resolved relative to it. Rows whose files do not exist are added anyway
and listed in a warning, so the batch can be fixed rather than retyped. The project takes the
`.oqproj`'s name when it has none yet.

## Exporting a component list

**Export to OpenQuant…** on the Analytics toolbar, or **Process ▸ Export to OpenQuant…**, writes
the features the ion table is showing as an OpenQuant component CSV, with the headers OpenQuant's
own component table writes and reads. The file opens in OpenQuant through **Method ▸ Import
components**.

The options:

| Option | Default | Meaning |
| --- | --- | --- |
| **Annotated spots only** | on | skip the unknowns |
| **Fill the fragment column from the representative MS/MS** | on | the most intense product ion at least 2 Da below the precursor becomes the `fragment` — an MRM-HR style component; without it the component is precursor-only |
| **RT half-window (min)** | 0.3 | the `window` column: half the retention window OpenQuant integrates in |
| **Mass tolerance** | 0.02 Da | the `tolerance` and `unit` columns, Da or ppm |

Each feature becomes one row: `name` (the annotation, or `m/z 760.5851 @ 11.40 min` for an
unknown), `group` (the ontology, `annotated`, or `unknown`), `precursor`, `fragment`, `rt`,
`window`, `tolerance`, `unit`, `formula`, `adduct`, `response` (`area`), and the remaining
OpenQuant columns (`is`, `internal_standard`, `concentration_unit`, `qualifier_of`, `ion_ratio`,
`ion_ratio_tolerance`, `regression`, `weighting`, `lm_id`) left empty for OpenQuant to fill.

Filter first: the list is what the table shows, so a class filter or **Confirmed** in the review
state gives a component list of exactly the features you vouched for.

## What is deliberately not shared

Calibration curves, internal-standard ratios, ion-ratio qualifiers and acceptance criteria define
targeted quantitation and stay in OpenQuant. OpenQuant's LIPID MAPS index and formula finder stay
there too: MS-DIAL brings its own lipid annotation, and two answers would be worse than one.
