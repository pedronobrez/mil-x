---
title: MIL-Q (OpenQuant)
section: Data and files
order: 32
summary: What MIL-X shares with MIL-Q — today's OpenQuant — importing one of its batches, and exporting a component list for targeted quantitation.
---

# MIL-Q (OpenQuant)

**MIL-Q** is the targeted half of the family: the application that was OpenQuant until its 1.0,
now at 1.0.x under its own name. The buttons say MIL-Q; the file formats (`.oqproj`, the component
CSV) were defined under the OpenQuant name and have not changed, which is why the interop code and
some file names still carry it. MIL-Q is a targeted-quantitation application; MIL-X is untargeted
processing on the MS-DIAL engine. They are meant to feel like two workspaces of one suite — the
same palette, the same shell of tabs and bands, the same native `.wiff` reading, the same mass
calculator and the same signal-to-noise convention — and the workflow between them is *discover in
MIL-X, quantify in MIL-Q*. The two also share a [[command-line]] contract, so a script can ask
either for the same chromatogram and parse the same answer.

## Importing an OpenQuant batch

**File ▸ Import MIL-Q batch (.oqproj)…**, the **Import MIL-Q batch…** button in the
Samples workspace, or opening an `.oqproj` or `.opvproj` from Finder, adds the samples of the
OpenQuant project to the batch with what OpenQuant knew about them:

| OpenQuant | MIL-X |
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

**Export to MIL-Q…** on the Analytics toolbar, or **Process ▸ Export to MIL-Q…**, writes
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
`window`, `tolerance`, `unit`, `formula`, `adduct`, `response` (`area`), `provenance` (`MIL-X
alignment feature 17`, so MIL-Q's audit trail says where the component came from), and the
remaining MIL-Q columns (`is`, `internal_standard`, `concentration_unit`, `qualifier_of`,
`ion_ratio`, `ion_ratio_tolerance`, `regression`, `weighting`, `lm_id`, `min_response`) left
empty for MIL-Q to fill. The header is every column MIL-Q's component reader has at 1.0.6; a test
holds it to MIL-Q's own list, and loads an exported file through MIL-Q's reader when a checkout is
at hand (`MILQ_REPO`).

Filter first: the list is what the table shows, so a class filter or **Confirmed** in the review
state gives a component list of exactly the features you vouched for.

## What is deliberately not shared

Calibration curves, internal-standard ratios, ion-ratio qualifiers and acceptance criteria define
targeted quantitation and stay in OpenQuant. OpenQuant's LIPID MAPS index and formula finder stay
there too: MS-DIAL brings its own lipid annotation, and two answers would be worse than one.
