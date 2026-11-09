# MIL suite — shared command-line contract, version 1 (`mil-cli/1`)

This file is meant to live, byte-identical, at `docs/CLI-CONTRACT.md` in **both** `pedronobrez/mil-q`
and `pedronobrez/mil-x`. Both applications implement the same subcommands, flags and output schemas,
so a script (or an agent) can call either one and parse the answer the same way. When the two differ,
the contract wins and the difference is a bug in one of them.

Binary names: `milq` (MIL-Q, Python entry point) and `milx` (MIL-X, the self-contained console that
already ships as `milx-cli`; `milx-cli lcms …` keeps working unchanged).

## Why it exists

The CLI is used by an agent that pays per token for everything it reads. So:

- **Summaries first.** Every subcommand that can produce thousands of rows has a `--summary` form
  that answers the usual question in a few lines (apex, height, area, points across the peak,
  co-elution score). Full traces only when asked (`--trace`).
- **Machine-readable only.** JSON on stdout by default (`--format json`), or TSV (`--format tsv`).
  No progress bars, colours, banners or log lines on stdout. Diagnostics go to stderr; with
  `--quiet`, nothing goes to stderr except errors.
- **Deterministic.** Same input, same bytes out. Numbers with a period as the decimal mark,
  regardless of locale. m/z to 4 decimals (6 for theoretical values), RT in **minutes** to 4
  decimals, intensities as the reader reports them.
- **Every JSON document** starts with `"schema": "mil-cli/1"`, `"tool": "milq"|"milx"`,
  `"version"`, `"command"` and `"input"` (absolute paths, sample index).
- **Errors** are JSON on stderr: `{"schema":"mil-cli/1","error":"<code>","message":"…"}` with a
  non-zero exit code: 2 bad arguments, 3 file unreadable/missing companion (`.wiff.scan`),
  4 nothing found (empty result is *not* an error unless `--require` is passed), 5 internal.
- **Headless.** No Qt/Avalonia import on any code path of these subcommands. Must run over SSH and
  inside a small Linux VM (see "Execution targets").
- **Cheap to call repeatedly.** A per-file sidecar index/cache (`--cache DIR`, default the app's
  cache folder) so the second call on the same file does not re-read headers, and a `serve` mode
  (below) that keeps files open across calls.

## Execution targets (must be verified, not assumed)

1. **macOS on Apple Silicon (the user's machine) — the primary target.** The agent cannot type into
   a macOS terminal; it reaches the Mac through `serve --inbox` (below): it writes request files into
   a shared folder and the Mac executes them natively, `.wiff` included.
2. Ubuntu 22.04 aarch64 VM, Python 3.10, ~3 GB RAM, 4 cores, no .NET preinstalled** — this is
   where the agent runs commands. Requirements: an install path that works there without a GUI
   stack; `.wiff` reading through Clearcore2 on .NET 8 linux-arm64 if it can be made to work
   (bootstrap installs the runtime into the user's home, no sudo); if `.wiff` cannot be read on
   Linux, say so in `selftest` output and support the mzML route fully (see `convert`). This target
   is a fallback: with `serve --inbox` running on the Mac it is not required for the workflow.
3. Memory: stream spectra; never hold a whole file's profile data. Peak RSS for `xic` on a
   ~1 GB `.wiff` must stay under ~1.5 GB.

## Data model the CLI exposes

- **sample** — one injection (multi-sample `.wiff` → `--sample N|NAME`, default 0).
- **channel** — one experiment of the method. `kind` is one of `ms1`, `ida-slot` (one SCIEX IDA
  dependent experiment), `dda-events` (all data-dependent MS2 of an mzML, one channel),
  `swath` (one isolation window), `mrm`, `product` (fixed-precursor product-ion scan), `other`.
- **scan** — one spectrum: `scan` (index within the channel), `cycle`, `rt`, `ms_level`,
  `parent_mz` (**the precursor actually isolated for this scan**; for SCIEX IDA this is
  `MassSpectrumInfo.ParentMZ`, never the experiment's `FixedMasses` placeholder), `isolation`
  (low/high when known), `ce`, `tic`, `points`.
- **source** (where an XIC or a spectrum is read from):
  - `ms1` — the survey channel;
  - `channel:N` — one channel by index;
  - `ms2-all` — every MS2 scan of every channel, merged by RT;
  - `gated:MZ[:TOL]` — **only the MS2 scans whose `parent_mz` is within TOL of MZ** (default
    TOL 0.05 Da), from any channel/slot, merged by RT. This is the core new feature.

## Subcommands

| Subcommand | Purpose | Key flags | Output |
|---|---|---|---|
| `selftest` | what works on this machine | `[FILES…]` | JSON: platform, runtime, vendor SDK status, per-file read check |
| `info FILE` | samples and channels | `--sample` | JSON: samples; channels with index, kind, label, scan count, mass range, CE, polarity |
| `scans FILE` | the scan table | `--source`, `--rt A:B`, `--parent MZ[:TOL]`, `--ms-level` | TSV/JSON rows as in the data model; `--count` gives counts only |
| `xic FILE` | extracted ion chromatograms | `--mz LIST` (comma or space separated), `--tol 0.01`, `--unit Da\|ppm`, `--source`, `--rt A:B`, `--smooth N` (Gaussian, scans), `--summary` (default) or `--trace`, `--reference MZ@SOURCE` | summary per m/z: apex RT, apex height, area (RT window), FWHM, points across peak, S/N (noise convention of the app, stated), and vs reference: Pearson r over the reference peak, apex ΔRT, height ratio |
| `spectrum FILE` | one spectrum as a peak list | `--source`, `--scan N` or `--rt A:B` (average), `--subtract A:B` (background average subtracted, can repeat), `--centroid vendor\|local\|none`, `--top N`, `--min-rel 0.01`, `--msp PATH` (also write MSP) | JSON/TSV peaks (m/z, intensity, relative), plus the scans used and their parent_mz |
| `isotopes FILE` | measured envelope vs theory | `--mz`, `--charge`, `--rt A:B`, `--formula` (optional) | M, M+1, M+2… measured vs expected, spacing check |
| `batch-xic PATHS…` | the same XIC summary over many files | as `xic`, plus `--rt-window C±W` | one row per (file, sample, m/z): apex RT, height, area, found flag; this is how blanks and distribution are checked |
| `convert FILE` | write mzML that keeps per-scan precursors | `--out`, `--centroid` | the mzML path; must round-trip through `scans` with identical parent_mz |
| `serve` | keep files open and answer requests: JSON lines on stdin (`--stdio`), or a watched folder (`--inbox DIR`, see below) | `--stdio` or `--inbox DIR`, `--allow-root DIR` (repeatable), `--max-jobs`, `--idle-exit MIN` | same documents as above, one per request |

## `serve --inbox`: the file-queue mode (how the agent runs commands on the Mac)

The agent works in a Linux VM that sees the user's connected folders but not the Mac's programs, and
it cannot type into a terminal. The user starts, once, in a macOS terminal:

```
milq serve --inbox ~/Documents/mil-jobs --allow-root ~/Documents/MIL-data
milx serve --inbox ~/Documents/mil-jobs-x --allow-root ~/Documents/MIL-data
```

Layout created under the inbox folder: `inbox/`, `running/`, `outbox/`, `failed/`, `log.jsonl`,
`STATUS.json`, and a `STOP` sentinel.

- **A request** is one file `inbox/<id>.json`, written atomically by the agent (write `<id>.json.tmp`,
  then rename): `{"schema":"mil-cli/1","id":"<id>","command":"xic","args":{…same names as the CLI
  flags, without dashes…},"format":"json"}`. Optional `"batch": [ …several requests… ]` runs them in
  order in one file.
- **The server** picks requests up in name order (poll ≤ 1 s, or file-system events), moves the file to
  `running/`, executes it **in-process** (no shell, no subprocess, no `eval`), writes the answer to
  `outbox/<id>.json` atomically (tmp + rename) and moves the request to `outbox/<id>.request.json`, or
  both to `failed/` with the error document. Large outputs (`--trace`, MSP, TSV) go to files under
  `outbox/<id>/` and the answer lists their paths.
- **Allow-list, not a shell.** Only the contract's subcommands (and MIL-Q's `peptide …`) are accepted;
  anything else is rejected with `error: "not_allowed"`. Every path in a request — inputs and outputs —
  must resolve (symlinks followed) inside an `--allow-root` or inside the inbox folder; otherwise
  `error: "path_outside_roots"`. No request can delete or overwrite a file outside `outbox/`.
- **One job at a time by default** (`--max-jobs 1`), so a 3 GB-RAM-sized job cannot starve the machine;
  a per-job timeout (`"timeout_s"`, default 1800) ends a runaway job with `error: "timeout"`.
- **Status and log.** `STATUS.json` is rewritten on every change: server version, tool, pid, started,
  current job (id, command, elapsed), queue length, last error. `log.jsonl` gets one line per job: id,
  command, args, start, end, exit, output paths, peak RSS. The log never contains file contents.
- **Stopping.** Creating `STOP` (or Ctrl-C, or `--idle-exit MIN` with nothing to do) finishes the current
  job and exits cleanly, removing `STOP`. A server that finds stale files in `running/` at start moves
  them to `failed/` with `error: "interrupted"`.
- **Same numbers.** A request through the inbox returns byte-for-byte the document the same CLI call
  prints; a test checks it.

Peptide subcommands are defined in MIL-Q only (see its prompt) and their outputs follow the same
conventions. MIL-X reaches peptides through MSP libraries written by MIL-Q.

## Contract test

Both repositories carry the same test: on a synthetic DDA mzML (MIL-X's
`tools/make_synthetic_mzml.py`, extended to emit an IDA-like pattern where several precursors share a
slot), `info`, `scans`, `xic --summary` (ms1 and gated) and `spectrum` must give the same numbers from
`milq` and `milx` within stated tolerances (RT exact, m/z 1e-4, intensity relative 1e-6 for the same
centroiding mode). A real `.wiff` version of the test runs when `MILQ_TEST_WIFF` / `MILX_TEST_WIFF`
point at one. A second test drives `serve --inbox` with a few requests (one valid, one with a path
outside the roots, one unknown command, one that times out) and checks the outbox, `failed/`, the
log and `STATUS.json`.
