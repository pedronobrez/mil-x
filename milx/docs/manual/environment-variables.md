---
title: Environment variables
section: Reference
order: 43
summary: Every MILX_* variable — what it changes, who it is for — plus the ones the runtime reads.
---

# Environment variables

None of these is needed in ordinary use; the Settings window covers what a person changes. They
exist for scripts, diagnostics and the smoke test.

Until 1.0 every one of them was an `OPENDIAL_*` variable. Those names still work in 1.0: at
start-up each is copied to its `MILX_*` name when that one is unset, so a script written for
OpenDIAL runs unchanged. The new name wins when both are set. The old names go away in a later
version, so rename them when you next touch the script. On macOS an application launched from Finder
does not see a shell's variables; launch it with `open -a /Applications/MIL-X.app --env
NAME=value` or from a terminal.

## Storage and appearance

| Variable | Effect |
| --- | --- |
| `MILX_SETTINGS_DIR` | the folder for `settings.json` instead of `~/.config/MIL-X` |
| `MILX_CACHE` | the root of the survey-scan cache instead of `~/Library/Caches/MIL-X` (the cache lives in `spectra` under it) |
| `MILX_THEME` | `dark` or `light` for this session, ignoring the setting |

## Raw data

| Variable | Effect |
| --- | --- |
| `MILX_WIFF_CENTROID` | how the native `.wiff` reader centroids profile spectra: `sciex` (default, the vendor's peak finder), `msdial` (local maximum), `0` (keep profile) |
| `MILX_WIFF_SAMPLE` | which sample of a multi-sample `.wiff` to read when nothing else says: an index (0-based) or a sample name |
| `MILX_WIFF_MIN_INTENSITY` | drop centroided peaks below this intensity |
| `MILX_PLUGINS` | an extra folder to load raw-file reader plugins from, besides `<app>/plugins` |
| `MILX_LEGACY_RAWDATA_DLL` | the path of MS-DIAL's original reader dll, for `.ibf`, `.cdf` and `.imzML`, instead of `<app>/plugins/legacy` |

## The msconvert bridge

| Variable | Effect |
| --- | --- |
| `MILX_MSCONVERT` | the msconvert executable; `docker` forces the Docker route |
| `MILX_DOCKER` | the docker executable (default `docker`) |
| `MILX_PWIZ_IMAGE` | the ProteoWizard image (default `proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses:latest`) |
| `MILX_DOCKER_PLATFORM` | the `--platform` flag (default `linux/amd64`, which Apple Silicon needs) |
| `MILX_MZML_CACHE` | where converted mzML files go instead of beside the source |
| `MILX_MSCONVERT_ARGS` | extra msconvert arguments, appended after the defaults |
| `MILX_MSCONVERT_PROFILE` | `1` keeps profile data (no vendor peak picking); set the method to Profile |
| `MILX_MSCONVERT_FORCE` | `1` reconverts even when a cached mzML exists |

The Settings window's values take precedence over these in the desktop application.

## Scripting the application

These make the application do something at start-up, for scripts and the tests; see
[[building-and-testing#The smoke test]].

| Variable | Effect |
| --- | --- |
| `MILX_OPEN` | open this `.milx`, `.mdproject` or results folder |
| `MILX_OPEN_RAW` | add this raw file, or every raw file in this folder, and open the first in the Explorer |
| `MILX_IMPORT_OPENQUANT` | import this `.oqproj` batch |
| `MILX_EXPORT_OPENQUANT` | after `MILX_OPEN`, write the annotated features as OpenQuant components to this path |
| `MILX_AUTORUN` | a folder of raw files (with an optional `library.msp` and `method*.txt`): process it into `milx_output` at start-up |
| `MILX_SNAPSHOT` | render the window to this PNG after `MILX_SNAPSHOT_DELAY` seconds (default 10), on workspace `MILX_SNAPSHOT_TAB` (0–4), after `MILX_SNAPSHOT_ACTION` (`explorer-peak`, `explorer-channel`, `analytics-spectrum`, `analytics-metric`, `analytics-magnify`, `stats-cluster`, `stats-network`, `search-demo`, `reintegrate-demo`, `review-peaks`, `review-map`, `review-candidates`, `wizard`, `about`) |
| `MILX_UI_PROBE` | write what the window is showing to this JSON file whenever it changes, and watch `<file>.commands` for commands; the smoke test's channel |
| `MILX_TRACE` | `1` prints Avalonia's binding and layout warnings to the console |

## Tests

| Variable | Effect |
| --- | --- |
| `MILX_UPDATE_BASELINES` | `1` rewrites the visual-regression baselines from the current render |
| `MILX_TEST_ALIGNMENT` | a real alignment result for the real-data statistics tests |
| `MILX_TEST_WIFF` | a real `.wiff` for the SCIEX plugin tests |

## The runtime

| Variable | Effect |
| --- | --- |
| `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` | `1` — set by the launcher of the command-line build; the desktop application forces the invariant culture itself. See [[troubleshooting#Processing]] |
| `DOTNET_ROOT` | where the .NET SDK is, for building (`~/.dotnet` after `setup-macos.sh`) |
