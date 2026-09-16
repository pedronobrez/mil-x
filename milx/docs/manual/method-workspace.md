---
title: The Method workspace
section: Workspaces
order: 12
summary: The processing parameters as a form and as the method file behind it; loading, saving and resetting.
---

# The Method workspace

Every parameter the run uses, as a form on the left and — with **Advanced text** on — as the
plain-text method file the form is a view of. `⌘3` shows it. The two are one thing: an edit in the
form rewrites the text, and an edit in the text is parsed back into the form when the box loses
focus, with any error shown under it in red.

![the method form](images/method.png)

## The toolbar

| Control | What it does |
| --- | --- |
| **Load method file…** | replaces the method with a `.txt` or `.method` file; a file that says `Machine category: GCMS` switches the mode |
| **Save method file…** | writes the current method, console-compatible |
| **Reset to defaults** | the defaults for the current mode |
| **Mode** | LC-MS or GC-MS; changes which sections the file carries and which engine runs |
| **Advanced text** | shows the method file beside the form |

The status line under the form summarises it: `LC-MS · DDA · positive · library.msp · 3 parameters
changed from defaults`.

## The library

Under the identification group, a panel says what the chosen `.msp` actually holds — how many
records, which adducts, what range of mass, and whether it carries retention times and over what
window. It is read from the file itself, in the background, whenever the path changes; a library of
four hundred thousand records takes a second or two.

| Control | What it does |
| --- | --- |
| **Re-read** | scans the file again, after it has been replaced on disk |
| **Ignore its retention times** | leaves the library's own times out of the score and out of the filtering. This is the default for a new method |
| **Calibrate to this run…** | fits the library's times to the ones the current result measured for the compounds it named, writes `<library>-rt-calibrated.msp` beside the original, points the method at it and puts retention back into the score |

A warning appears in red when the library's retention window does not overlap the method's. That is
the usual case with a public library, and scoring against it costs names rather than buying
confidence — see [[annotation#Why so few features are named]]. The calibration needs a processed
result with at least twenty names shared with the library; it reports how many compounds it fitted
on and the R² of the fit.

## The form

The sections, in the order they appear. Every field is explained, with its default and what it
does to the result, in [[method-parameters]].

**Data type** — ion mode, MS1 and MS2 data type (centroid or profile), the default acquisition type
for new files, the target omics (metabolomics or lipidomics).

**Data collection** — the retention-time and mass ranges the run reads, and the centroid
tolerances.

**Peak detection** — smoothing, minimum peak width and height, mass slice width, maximum charge,
the adducts searched.

**Deconvolution** — the MS2Dec parameters: sigma window, MS/MS amplitude cutoff, keep isotope
range, and **Exclude fragments after precursor**.

**GC-MS** (GC-MS mode only) — retention type, retention-index compound type, alignment index type,
the RI dictionary table.

**Identification** — the MSP spectral library and an optional text library, the tolerances and
score cut-offs of the annotation, **Use RT for scoring**, **Use RT for filtering**, and **Only
report top hit**.

**Alignment** — whether to align after peak picking, the reference file, the tolerances and
their weights, the filters, gap filling.

**Export and process** — **Export QA height matrix (.qa.tsv)**, and the number of threads.

## The method file

The text is what MS-DIAL's console reads: `Key: value` lines, case-insensitive keys, grouped under
`#` comment headings. Keys the form does not model are kept under `#Other` and written back
unchanged, so a file from the console survives a round trip through the form. The same file is
written to the results folder as `milx_method.txt` at the start of every run, which is how a
result records how it was made.

A parameter export from MS-DIAL on Windows (`<project>_param_<stamp>.txt`) can be turned into a
method file with `tools/msdial_param_to_method.py`; see [[command-line]].

## Where the method lives

In the project. The wizard writes it, every change marks the project dirty, and **Save project**
writes it back. Opening a results folder with no project reads `milx_method.txt` from it
when there is one, and falls back to the defaults for the mode otherwise.
