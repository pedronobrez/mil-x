---
title: Environment variables
section: Reference
order: 43
summary: Every OPENDIAL_* variable — what it changes, who it is for — plus the ones the runtime reads.
---

# Environment variables

None of these is needed in ordinary use; the Settings window covers what a person changes. They
exist for scripts, diagnostics and the smoke test. On macOS an application launched from Finder
does not see a shell's variables; launch it with `open -a /Applications/OpenDIAL.app --env
NAME=value` or from a terminal.

## Storage and appearance

| Variable | Effect |
| --- | --- |
| `OPENDIAL_SETTINGS_DIR` | the folder for `settings.json` instead of `~/.config/OpenDIAL` |
| `OPENDIAL_CACHE` | the root of the survey-scan cache instead of `~/Library/Caches/OpenDIAL` (the cache lives in `spectra` under it) |
| `OPENDIAL_THEME` | `dark` or `light` for this session, ignoring the setting |

## Raw data

| Variable | Effect |
| --- | --- |
| `OPENDIAL_WIFF_CENTROID` | how the native `.wiff` reader centroids profile spectra: `sciex` (default, the vendor's peak finder), `msdial` (local maximum), `0` (keep profile) |
| `OPENDIAL_WIFF_SAMPLE` | which sample of a multi-sample `.wiff` to read when nothing else says: an index (0-based) or a sample name |
| `OPENDIAL_WIFF_MIN_INTENSITY` | drop centroided peaks below this intensity |
| `OPENDIAL_PLUGINS` | an extra folder to load raw-file reader plugins from, besides `<app>/plugins` |
| `OPENDIAL_LEGACY_RAWDATA_DLL` | the path of MS-DIAL's original reader dll, for `.ibf`, `.cdf` and `.imzML`, instead of `<app>/plugins/legacy` |

## The msconvert bridge

| Variable | Effect |
| --- | --- |
| `OPENDIAL_MSCONVERT` | the msconvert executable; `docker` forces the Docker route |
| `OPENDIAL_DOCKER` | the docker executable (default `docker`) |
| `OPENDIAL_PWIZ_IMAGE` | the ProteoWizard image (default `proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses:latest`) |
| `OPENDIAL_DOCKER_PLATFORM` | the `--platform` flag (default `linux/amd64`, which Apple Silicon needs) |
| `OPENDIAL_MZML_CACHE` | where converted mzML files go instead of beside the source |
| `OPENDIAL_MSCONVERT_ARGS` | extra msconvert arguments, appended after the defaults |
| `OPENDIAL_MSCONVERT_PROFILE` | `1` keeps profile data (no vendor peak picking); set the method to Profile |
| `OPENDIAL_MSCONVERT_FORCE` | `1` reconverts even when a cached mzML exists |

The Settings window's values take precedence over these in the desktop application.

## Scripting the application

These make the application do something at start-up, for scripts and the tests; see
[[building-and-testing#The smoke test]].

| Variable | Effect |
| --- | --- |
| `OPENDIAL_OPEN` | open this `.odproj`, `.mdproject` or results folder |
| `OPENDIAL_OPEN_RAW` | add this raw file, or every raw file in this folder, and open the first in the Explorer |
| `OPENDIAL_IMPORT_OPENQUANT` | import this `.oqproj` batch |
| `OPENDIAL_EXPORT_OPENQUANT` | after `OPENDIAL_OPEN`, write the annotated features as OpenQuant components to this path |
| `OPENDIAL_AUTORUN` | a folder of raw files (with an optional `library.msp` and `method*.txt`): process it into `opendial_output` at start-up |
| `OPENDIAL_SNAPSHOT` | render the window to this PNG after `OPENDIAL_SNAPSHOT_DELAY` seconds (default 10), on workspace `OPENDIAL_SNAPSHOT_TAB` (0–4), after `OPENDIAL_SNAPSHOT_ACTION` (`explorer-peak`, `explorer-channel`, `analytics-spectrum`, `analytics-metric`, `analytics-magnify`, `stats-cluster`, `stats-network`, `search-demo`, `reintegrate-demo`, `review-peaks`, `review-map`, `review-candidates`, `wizard`, `about`) |
| `OPENDIAL_UI_PROBE` | write what the window is showing to this JSON file whenever it changes, and watch `<file>.commands` for commands; the smoke test's channel |
| `OPENDIAL_TRACE` | `1` prints Avalonia's binding and layout warnings to the console |

## Tests

| Variable | Effect |
| --- | --- |
| `OPENDIAL_UPDATE_BASELINES` | `1` rewrites the visual-regression baselines from the current render |
| `OPENDIAL_TEST_ALIGNMENT` | a real alignment result for the real-data statistics tests |
| `OPENDIAL_TEST_WIFF` | a real `.wiff` for the SCIEX plugin tests |

## The runtime

| Variable | Effect |
| --- | --- |
| `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` | `1` — set by the launcher of the command-line build; the desktop application forces the invariant culture itself. See [[troubleshooting#Processing]] |
| `DOTNET_ROOT` | where the .NET SDK is, for building (`~/.dotnet` after `setup-macos.sh`) |
