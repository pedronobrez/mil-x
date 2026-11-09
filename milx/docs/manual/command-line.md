---
title: The command line and the tools
section: Reference
order: 44
summary: milx, the command line MIL-X shares with MIL-Q — summaries first, JSON out, a file-queue server for an agent — plus the MS-DIAL console, the scripts and the diagnostic tools.
---

# The command line and the tools

## milx

MIL-X and MIL-Q implement one command-line contract (`docs/CLI-CONTRACT.md`, version `mil-cli/1`,
byte-identical in both repositories): the same subcommands, flags and JSON schemas, so a script or
an agent can call `milq` or `milx` and parse the answer the same way. The binary is published by
`scripts/build-cli.sh` into `dist/milx-cli-<rid>/milx`, self-contained, with the SCIEX plugin
beside it; `milx-cli lcms …` in the same folder is MS-DIAL's own console, unchanged.

The contract was written for a reader that pays for every line it reads, and three rules follow
from that. **Summaries first**: every subcommand that could print thousands of rows has a
`--summary` form, the default, that answers the usual question in a few lines — apex, height,
area, points across the peak, co-elution score — and the full trace comes only on `--trace`.
**Machine-readable only**: JSON on stdout (`--format json`) or TSV (`--format tsv`), nothing else
on stdout, diagnostics on stderr, and with `--quiet` nothing on stderr but errors. **Deterministic**:
the same input gives the same bytes, with a period as the decimal mark whatever the locale, m/z to
four decimals (six for theoretical values), retention times in minutes to four, intensities as the
reader reports them. Every document starts with `"schema": "mil-cli/1"`, `"tool": "milx"`, the
version, the command and the input (absolute path, sample index). An error is JSON on stderr —
`{"schema":"mil-cli/1","error":"unreadable","message":"…"}` — with exit 2 for bad arguments, 3 for a
file that cannot be read (a `.wiff` without its `.wiff.scan`), 4 for nothing found (only with
`--require`; an empty result is not otherwise an error), 5 for an internal failure.

| Subcommand | Answers | Key flags |
| --- | --- | --- |
| `selftest [FILES…]` | what works on this machine: platform, runtime, whether the SCIEX SDK loaded, whether msconvert is found, and for each file whether it reads, with how many spectra and in how long | |
| `info FILE` | the samples of the file and its channels: index, kind (`ms1`, `ida-slot`, `dda-events`, `swath`, `product`), label, scan count, mass range, collision energy, polarity | `--sample N\|NAME` |
| `scans FILE` | the scan table of a source: scan, cycle, RT, MS level, `parent_mz` — the precursor this scan actually isolated —, isolation window, CE, TIC, points | `--source`, `--rt A:B`, `--parent MZ[:TOL]`, `--ms-level N`, `--count` |
| `xic FILE` | one summary per m/z: apex RT, apex height, area, FWHM, points across the peak, S/N; against a `--reference MZ@SOURCE` ion, Pearson's r over the reference peak, apex ΔRT and height ratio | `--mz LIST` (commas or spaces), `--tol 0.01`, `--unit Da\|ppm`, `--source`, `--rt A:B`, `--smooth N`, `--trace` |
| `spectrum FILE` | one spectrum as a peak list — a scan, or the average over a time range — with backgrounds subtracted, the scans used and their precursor | `--scan N` or `--rt A:B`, `--subtract A:B` (repeatable), `--centroid vendor\|local\|none`, `--top N`, `--min-rel 0.01`, `--msp PATH` |
| `isotopes FILE` | the measured envelope — M, M+1, M+2… — against theory and the spacing check | `--mz`, `--charge`, `--rt A:B`, `--formula` |
| `batch-xic PATHS…` | the `xic` summary over many files, one row per file, sample and m/z, with the folder check's findings as skips | as `xic`, plus `--rt-window C±W` |
| `convert FILE` | mzML that keeps every scan's own precursor | `--out`, `--centroid` |
| `export --project P` | the review exports without the window: `--reviewed OUT` (the tagged table), `--matrix OUT` (heights and areas), `--milq OUT` (the component list) | `--areas`, `--annotated-only` |
| `serve` | keep files open and answer requests: `--stdio` for JSON lines, `--inbox DIR` for the folder below | `--allow-root DIR` (repeatable), `--max-jobs 1`, `--idle-exit MIN` |
| `lcms`, `gcms`, … | MS-DIAL's own workflows, run in-process (see below) | `--acquisition-type`, `--library`, `--summary` |

A **source** says where a chromatogram or a spectrum is read from: `ms1`, the survey channel;
`channel:N`, one channel by its index in `info`; `ms2-all`, every MS2 scan of every channel merged
by retention time; and `gated:MZ[:TOL]` — only the MS2 scans whose own precursor is within TOL
(default 0.05 Da) of MZ, from any IDA slot or DDA event, merged by RT. The gate is the same
mechanism as the Explorer's *Gated by precursor* source ([[explorer-workspace#The panels]]), and it
is what a product ion's chromatogram needs on a data-dependent file, where one precursor's spectra
land in whichever slot was free each cycle.

The `xic` summary states its definitions in the document, because MIL-Q computes the same ones:
the apex is the tallest point in the window; the bounds walk outward from it to the first point at
or below 5 % of the apex, or to a rise of a tenth of the apex after a valley; the area is the
trapezoid between the bounds in intensity × minutes; the FWHM interpolates the two half-height
crossings; S/N is the apex over MIL-Q's automatic noise estimate — the median absolute deviation of
the point-to-point differences scaled to a standard deviation, the lower-half standard deviation
when more than half the differences are zero — and is null when nothing can be measured, as on a
trace of exact zeros ([[evidence-panels#Signal-to-noise over a noise region]] says why).

```bash
milx info run.wiff --sample 2
milx scans run.wiff --source ms2-all --count
milx xic run.wiff --mz "760.5851, 184.0733" --tol 0.01
milx xic run.wiff --mz 184.0733 --source gated:760.5851 --reference 760.5851@ms1
milx spectrum run.wiff --source gated:760.5851 --rt 11.3:11.5 --subtract 10:10.5 --top 20 --msp pc.msp
milx isotopes run.wiff --mz 760.5851 --rt 11.3:11.5 --formula C42H82NO8P
milx batch-xic ~/data/batch --mz 760.5851 --rt-window "11.4±0.3" --format tsv
milx convert run.wiff --out run.mzML
milx export --project results/ --reviewed reviewed.tsv --matrix matrix.tsv --milq components.csv
```

### What it measured

On a ZenoTOF 7600 IDA acquisition (a 64 MB `.wiff.scan`, 7 906 spectra in 25 experiments, 490
cycles), read natively on an Apple Silicon Mac:

| Call | Time | Peak memory |
| --- | --- | --- |
| `info`, cold | 27.1 s — the vendor read with SCIEX's own centroiding | 343 MB |
| `info`, warm (`--cache`) | 0.16 s | 208 MB |
| `xic --mz 760.5851`, cold | 29.3 s | 314 MB |
| `scans --source ms2-all --count` | 7 416 MS2 scans, 7 301 distinct precursors | |

The read is the cost, and it is paid once in `serve` mode, where files stay open across requests:
the same `xic` through the inbox, file already open, answered in 0.26 s. The `--cache DIR` sidecar
(the desktop application's own spectra cache) answers `info` and `scans` without the vendor library
on the second call; intensities for `xic` and `spectrum` always come from the reader, so a
document is the same bytes cold and warm.

On the synthetic DDA study (`tools/make_synthetic_mzml.py`, 563 spectra): `info` in 0.17 s;
`convert` writes 2.9 MB of mzML, and `scans` on the result gives the same `parent_mz` for all 62
MS2 scans and the same `xic` apex and area to the last digit; `lcms --summary` on the three samples
takes 2.6 s and reports 35, 40 and 32 peaks, 12 annotated in each, 18 aligned features. `export` on
the eight-injection liver project writes 2 235 features three ways.

### The MS-DIAL workflows, with a summary

`milx lcms -i IN -o OUT -m METHOD [-p]` runs MS-DIAL's console in-process, exactly as `milx-cli
lcms` does, plus three things an agent needs. `--acquisition-type DDA|SWATH|AIF` and `--library
X.msp` override the method file — the MS-DIAL 5.5 projects of this user's IDA data were marked
SWATH, see [[raw-data-formats#SCIEX .wiff]] — and the method actually used is written beside the
results as `milx_method.txt`. `--summary` replaces the console's log (kept in `milx-run.log`) with
one JSON: per file, peaks, MS/MS assigned and annotated; the alignment's features, MS/MS assigned
and annotated; and the paths of every export. `gcms`, `dims`, `imms`, `lcimms`, `msn` and `eic`
pass through unchanged.

### serve --inbox: the file queue

An agent working in a Linux virtual machine sees the Mac's folders but not its programs, and
cannot type into a terminal. So the Mac runs, once, in a terminal:

```bash
milx serve --inbox ~/Documents/mil-jobs --allow-root ~/Documents/MIL-data
```

and the agent writes request files. The inbox folder gets `inbox/`, `running/`, `outbox/`,
`failed/`, `log.jsonl`, `STATUS.json` and, when it is time to stop, a `STOP` file. A request is one
file `inbox/<id>.json`, written atomically (write `<id>.json.tmp`, then rename):

```json
{"schema": "mil-cli/1", "id": "0042", "command": "xic",
 "args": {"file": "/Users/me/Documents/MIL-data/run.wiff", "mz": "760.5851, 184.0733", "tol": 0.01},
 "format": "json", "timeout_s": 600}
```

The args are the command's flags without dashes; a `"batch": [ … ]` runs several requests in order
in one file. The server picks requests up in name order — a file-system watcher, with a poll every
second behind it — moves the file to `running/`, executes it in-process on the same code the
command line runs (no shell, no subprocess, no eval), writes `outbox/<id>.json` atomically and
keeps the request beside it as `outbox/<id>.request.json`, or puts both in `failed/` with the error
document. The answer is byte for byte what `milx xic …` would have printed; a test checks it. A
trace, an MSP or a TSV goes to a file under `outbox/<id>/`, and the answer lists it under `files`.

It is an allow-list, not a shell: only the contract's subcommands and the MS-DIAL workflow are
accepted, anything else fails with `not_allowed`. Every path in a request — inputs and outputs —
must resolve, symbolic links followed, inside an `--allow-root` or inside the inbox folder, or the
request fails with `path_outside_roots`; nothing outside `outbox/` is ever written over. One job
runs at a time by default (`--max-jobs 1`), so a job sized for a small machine cannot starve this
one; `timeout_s` (default 1800) ends a runaway job with `timeout`. `STATUS.json` is rewritten on
every change — version, pid, started, the current job and its elapsed time, the queue length, the
last error — and `log.jsonl` gets one line per job: id, command, args, start, end, exit, the
output paths and the peak resident memory; it never holds file contents. Creating `STOP` (or
Ctrl-C, or `--idle-exit MIN` with nothing to do) finishes the current job and exits, removing
`STOP`; a server that finds files in `running/` at start moves them to `failed/` as `interrupted`.

To have the server start with the session, a `launchd` agent does it; save this as
`~/Library/LaunchAgents/io.github.pedronobrez.milx.serve.plist` and `launchctl load` it:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>Label</key><string>io.github.pedronobrez.milx.serve</string>
  <key>ProgramArguments</key><array>
    <string>/Applications/MIL-X.app/Contents/MacOS/milx</string>
    <string>serve</string><string>--inbox</string><string>/Users/me/Documents/mil-jobs</string>
    <string>--allow-root</string><string>/Users/me/Documents/MIL-data</string><string>--quiet</string>
  </array>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
  <key>StandardErrorPath</key><string>/Users/me/Documents/mil-jobs/serve.err</string>
</dict></plist>
```

`serve --stdio` is the same server over a pipe: one request per line on stdin, one document per
line on stdout, for a program that spawns `milx` itself.

### The Linux target

The contract's second machine is an Ubuntu 22.04 aarch64 virtual machine with about 3 GB of
memory and no .NET: `scripts/build-cli.sh linux-arm64` publishes a self-contained folder for it,
nothing to install. Whether `.wiff` reads there is decided by `milx selftest run.wiff` on that
machine — the SCIEX plugin runs on .NET 8 on linux-arm64 in principle, and it has not been verified
on that machine from here; mzML reads everywhere, and `convert` on the Mac is the route that makes
a `.wiff` travel. With the inbox server on the Mac the Linux machine needs neither.

### The contract test

`tests/MilX.Cli.Tests` runs every subcommand on the synthetic study and on an IDA-like variant of
it (`make_synthetic_mzml.py --ida 4`: four dependent slots a cycle, filled by rank, no exclusion,
so one precursor moves between slots), drives the inbox server with the contract's four requests —
one valid, one with a path outside the roots, one with a command the allow-list refuses, one that
times out — and compares `milx` against `milq` on `info`, `scans`, `xic` (ms1 and gated) and
`spectrum` when `MILQ_CLI` names MIL-Q's executable (RT exact, m/z 1e-4, intensity relative 1e-6);
`MILX_TEST_WIFF` adds a real acquisition.

## milx-cli

MS-DIAL's own console (`MSDIALCUI`), built with MIL-X's raw-data layer and published beside `milx`.
It runs the same engine the desktop application runs, without the window.

```bash
milx-cli lcms -i /data/run1 -o /data/run1/out -m method.txt -p
```

| Option | Meaning |
| --- | --- |
| `lcms`, `gcms`, `dims`, `imms`, `lcimms`, `msn`, `eic` | the workflow; the desktop application covers `lcms` and `gcms` |
| `-i` | a folder (every raw file in it), one file, or a CSV listing files with their class and type |
| `-o` | the output folder |
| `-m` | the method file; see [[method-parameters]] |
| `-p` | also write an `.mdproject`, which the desktop application and MS-DIAL on Windows open |
| `--version` | the MS-DIAL version |

The `milx-cli` launcher forces the invariant culture before starting; use it rather than
`MSDIALCUI` directly. Vendor files are converted and `.wiff` files are read natively exactly as in
the application, with the plugin in `plugins/sciex` beside the binary.

## Scripts

All under `milx/scripts/`; see [[building-and-testing]] for the build ones.

| Script | What it does |
| --- | --- |
| `smoke-ui.py` | drives the installed application and checks it works: `--project` opens a processed project and checks the shell; `--process FOLDER --library X.msp` builds a project from the raw files, processes it, re-integrates a peak, saves, exports, opens the manual |
| `ui-drive.sh` | the same primitives one at a time — `front`, `click X Y`, `key CODE cmd`, `shot PATH`, `where` — for a screenshot or a look |
| `build-manual.py` | builds the PDF and HTML of this manual from `docs/manual`; `--lang pt` builds the Portuguese edition from `docs/manual/pt` |
| `make-app-bundle.sh` | publishes and packages `dist/MIL-X.app` |
| `build-gui.sh`, `build-cli.sh` | publish the application, and `milx` with the console (`build-cli.sh linux-arm64` for the Linux machine) |
| `test.sh` | every test suite plus the synthetic end-to-end runs |
| `setup-macos.sh` | the .NET SDK, without sudo |
| `fetch-sciex-assemblies.sh` | the SCIEX SDK into `vendor/sciex`, for the native `.wiff` reader |

## Tools

Under `milx/tools/`, run with `dotnet run --project tools/<name> -- …`:

| Tool | What it does |
| --- | --- |
| `msdial_param_to_method.py <param.txt> --out method.txt [--msp lib.msp]` | turns an MS-DIAL parameter export from Windows into a method file, key by key, so a run can be reproduced |
| `make_synthetic_mzml.py --out DIR [--samples N] [--mode gcms] [--ida SLOTS]` | a synthetic LC-MS/MS DDA (or GC-MS EI) dataset with its library and method, for the end-to-end tests; `--ida` lays the MS2 scans out as IDA slots, several precursors sharing a slot |
| `ResultCompare <reference> <test> [rtTol] [mzTol] [out.tsv]` | matches two results peak by peak and feature by feature and reports the ratios; `--check-library <project> <lib.msp>` asks whether a library could have produced a run's annotations; `--dump-params <project>` prints a project's parameters; `--name-detail` lists name disagreements |
| `RawDump <file> [--full] [--max N]` | prints what the raw-data layer reads from a file: spectra, precursors, timings — the open-versus-closed parity check |
| `WiffProbe <file.wiff> [cycles]` | prints how a `.wiff` acquisition is laid out: experiments, cycles, precursors per scan |
| `make_icon.py` | draws the application icon |
