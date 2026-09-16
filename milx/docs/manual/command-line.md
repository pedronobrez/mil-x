---
title: The command line and the tools
section: Reference
order: 44
summary: The milx-cli console for processing without the window, the smoke and drive scripts, and the diagnostic tools.
---

# The command line and the tools

## milx-cli

MS-DIAL's own console (`MSDIALCUI`), built with MIL-X's raw-data layer and published as a
self-contained binary by `scripts/build-cli.sh` into `dist/milx-cli-<rid>/`. It runs the same
engine the desktop application runs, without the window, which is what a server or a batch script
wants.

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
| `build-gui.sh`, `build-cli.sh` | publish the application and the console |
| `test.sh` | every test suite plus the synthetic end-to-end runs |
| `setup-macos.sh` | the .NET SDK, without sudo |
| `fetch-sciex-assemblies.sh` | the SCIEX SDK into `vendor/sciex`, for the native `.wiff` reader |

## Tools

Under `milx/tools/`, run with `dotnet run --project tools/<name> -- …`:

| Tool | What it does |
| --- | --- |
| `msdial_param_to_method.py <param.txt> --out method.txt [--msp lib.msp]` | turns an MS-DIAL parameter export from Windows into a method file, key by key, so a run can be reproduced |
| `make_synthetic_mzml.py --out DIR [--samples N] [--mode gcms]` | a synthetic LC-MS/MS DDA (or GC-MS EI) dataset with its library and method, for the end-to-end tests |
| `ResultCompare <reference> <test> [rtTol] [mzTol] [out.tsv]` | matches two results peak by peak and feature by feature and reports the ratios; `--check-library <project> <lib.msp>` asks whether a library could have produced a run's annotations; `--dump-params <project>` prints a project's parameters; `--name-detail` lists name disagreements |
| `RawDump <file> [--full] [--max N]` | prints what the raw-data layer reads from a file: spectra, precursors, timings — the open-versus-closed parity check |
| `WiffProbe <file.wiff> [cycles]` | prints how a `.wiff` acquisition is laid out: experiments, cycles, precursors per scan |
| `make_icon.py` | draws the application icon |
