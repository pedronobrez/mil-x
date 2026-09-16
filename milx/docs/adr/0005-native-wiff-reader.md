# ADR 0005: Native SCIEX .wiff reading through the Clearcore2 SDK (OpenQuant's approach)

## Status
Accepted (2026-09-07)

## Context
ADR 0002 routes every vendor format through msconvert. For SCIEX `.wiff` that is unnecessary:
OpenQuant (the user's targeted-quantitation application) already reads `.wiff` natively on
macOS by loading SCIEX's managed Clearcore2 assemblies on .NET 8 — the same assemblies that the
open-source `alpharaw` package redistributes under SCIEX's "WIFF Reader Distributable SDK"
license. MIL-X is itself a .NET application, so it can link to them directly instead of
going through Python.

## Decision
`MilX.Plugins.SciexWiff` implements `IRawFileReaderPlugin` on top of Clearcore2 and is
loaded from `<app>/plugins/sciex`. It follows OpenQuant's recipe exactly:

* `AnalystWiffDataProvider(OpenFileMode.ReadOnlyShared)` so Analyst / OpenQuant can keep the
  file open; `AnalystDataProviderFactory.CreateBatch` → samples → experiments → cycles;
* the thread culture is pinned to invariant (Clearcore2 parses numbers with the current culture);
* on non-Windows the private `StgStorage.sWindows` flag is switched off so the managed
  structured-storage implementation is used instead of the Windows COM API;
* the .NET Framework compatibility packages the SDK expects are referenced from NuGet.

Mapping to MS-DIAL's model: one `RawSpectrum` per (experiment, cycle) with the experiment's own
retention time, `ExperimentID` = experiment index (SWATH/DIA grouping), precursor from the
experiment's fixed mass (product-ion / SWATH) or the IDA parent m/z, isolation window from the
SDK, untriggered IDA scans skipped, and profile spectra centroided with MS-DIAL's own
local-maximum method (the one MsdialCore applies to profile data). Multi-sample batches are
exposed as one symbolic link per sample (`WiffSampleLinks`), since MS-DIAL identifies files by path.

The SDK assemblies are not committed: `scripts/fetch-sciex-assemblies.sh` copies them from an
installed `alpharaw` (or downloads the wheel) into `vendor/sciex`, and the plugin builds as a
stub without them. Without the plugin, `.wiff` still works through msconvert.

## Consequences
* `.wiff` opens natively on macOS in about a second per file; verified on TripleTOF 5600
  MRM-HR/IDA acquisitions and end to end through the MS-DIAL LC-MS pipeline.
* `.wiff2` is declared but untested (no file available); the SDK ships `Clearcore2.Data.Wiff2`.
* Users must accept SCIEX's redistribution license terms when shipping the plugin folder.
