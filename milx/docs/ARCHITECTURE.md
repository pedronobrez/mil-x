# MS-DIAL 5 reverse-engineering report and MIL-X architecture

This document records what was learned by taking the MS-DIAL 5.5.260817 source tree apart
(86 projects, ~800 k lines of C#), which parts are portable, which parts are Windows-only,
what the closed components do, and how MIL-X rebuilds the missing pieces so the software runs
natively on macOS (Apple Silicon and Intel) with Linux as the next target.

## 1. Repository map (upstream)

```
MsdialWorkbench.sln                     86 projects
src/Common/
  CommonStandard  (149 k LOC, netstandard2.0/2.1;net48;net472;net8.0)   all algorithms' shared foundation:
                  chromatography/peak picking, spectral scoring, adduct/isotope logic, lipidomics
                  nomenclature and in-silico MS/MS, formula generator, MSP/MGF/MassBank parsers,
                  MessagePack (de)serialisation, proteomics, molecular networking.   -> PORTABLE
  NCDK            (256 k LOC, netstandard2.0)  .NET port of the Chemistry Development Kit.        -> PORTABLE
                  (InChI generation needs the native libinchi; only lipidomics InChIKey generation
                  and MassBank export use it; the shipped binary is x64/libinchi.dll = Windows only)
  spectra-hash    SPLASH spectral hash.                                                            -> PORTABLE
  CommonMVVM      (5 k LOC, net472, WPF)  MVVM helpers, dialogs, validators.                       -> WINDOWS
  ChartDrawing    (26 k LOC, net472, WPF)  the whole charting stack (axes, chromatogram, spectrum,
                  heat map, table controls).                                                       -> WINDOWS
  NCDK.Display    (23 k LOC, net472, WPF)  2-D structure depiction.                                -> WINDOWS
  CommonSourceGenerator  Roslyn source generator used by the GUI.                                  -> PORTABLE
src/MSDIAL5/
  MsdialCore      (34 k LOC, netstandard2.0/2.1;net48;net8.0)  the engine: data providers,
                  peak spotting, MS2Dec deconvolution, annotation (MSP/text/LBM/EAD), alignment
                  (join, gap fill, refine), normalisation, project storage (MessagePack, zip),
                  exporters (txt/csv, msp/mgf/mat/sdf, mzTab-M, MassBank, GNPS).                   -> PORTABLE
  MsdialLcMsApi / MsdialGcMsApi / MsdialDimsCore / MsdialImmsCore / MsdialLcImMsApi /
  MsdialImmsImagingCore / MsdialIntegrate  (netstandard2.0)  per-ionisation pipelines.            -> PORTABLE
  MsdialGuiApp    (66 k LOC, 171 XAML, net48/net481/net472, WPF)  the desktop application.        -> WINDOWS
  SpectrumViewer  (WPF)                                                                            -> WINDOWS
src/MSDIAL4/      the previous generation (MSDIAL4 GUI + libraries); still used for the
                  MSDIAL4 console; not needed by MSDIAL5.                                         -> legacy
src/MSFINDER/     MS-FINDER (structure elucidation).  GUI WPF; core netstandard.                   -> partially portable
src/RawDataApp/   RawDataViewer / RawDataConverter (WPF).                                          -> WINDOWS
tests/MSDIAL5/MsdialCoreTestApp  (net472;net48;net8)  **the MS-DIAL console (assembly MSDIALCUI)**:
                  `lcms`, `gcms`, `dims`, `imms`, `lcimms`, `msn`, `eic` commands, method-file
                  parser, CSV import.  The upstream CI already publishes it for osx-x64.           -> PORTABLE
Assemblies/RawDataHandler-Vendor-UnSupported.1.3.9699.471.nupkg   closed reader (see §3)
```

Dependency graph of the portable core (arrows = ProjectReference):

```
NCDK <- CommonStandard <- MsdialCore <- {LcMs, GcMs, Dims, Imms, LcImMs, ImmsImaging} <- MsdialIntegrate <- MSDIALCUI
                            ^-- Splash                                                              ^-- MsdialGuiApp (WPF)
                            ^-- RawDataHandler (closed NuGet)  ==> replaced by MilX.RawData
```

NuGet packages used by the portable core: Accord/Accord.Math/Accord.Statistics 3.8 (managed),
MessagePack 1.7.3.7 (managed; old, has advisories), Newtonsoft.Json, MathNet.Numerics,
System.Data.SQLite.Core 1.0.112 (native SQLite.Interop shipped for win/linux-x64/osx-x64 only;
used solely by the EAD lipid SQLite database), System.CommandLine 2.0.8, zlib.net 1.0.3 (managed,
used by the closed reader).

## 2. Processing pipeline (what actually runs)

`LcmsProcess` (console) and `LcmsMethodModel` (GUI) orchestrate the same engine objects:

1. **Input** – `AnalysisFileBean` per raw file (path, class, type, acquisition type DDA/SWATH/AIF).
2. **Raw data** – `IDataProviderFactory<AnalysisFileBean>` → `StandardDataProvider` →
   `RawDataAccess(path, fileId, getProfile, isImaging, isGui, correctedRts).GetMeasurement()` →
   `RawMeasurement { SpectrumList, AccumulatedSpectrumList, ChromatogramList, CollisionEnergyTargets }`.
   The data model (`RawSpectrum`, `RawPrecursorIon`, ...) lives in **CommonStandard** (open); only the
   readers are closed.
3. **Peak spotting** – `PeakSpottingCore.Execute3DFeatureDetection`: m/z slices → EIC → smoothing →
   `PeakDetection` (CommonStandard) → isotope/adduct grouping → `ChromatogramPeakFeature` list.
4. **Deconvolution** – `Ms2Dec` (MS2Dec algorithm; per collision energy for AIF) → `MSDecResult`.
5. **Annotation** – `StandardAnnotationProcess` with `LcmsMspAnnotator` / `LcmsTextDBAnnotator` /
   LBM (lipid) annotators, `FacadeMatchResultEvaluator`.
6. **Persist** – `.pai2` (peak features, MessagePack), `.dcl` (deconvoluted spectra, custom binary),
   `.rtc` (RT correction), `.sfs`, tags xml.
7. **Alignment** – `LcmsAlignmentProcessFactory` → `PeakAligner` (join → gap fill → refine) →
   `AlignmentResultContainer` (`.arf2`, `.EIC.aef`, `.dcl`).
8. **Export** – `AnalysisCSVExporter` (`.mdpeak`), `AnalysisMspExporter` (`.mdmsp`),
   `AlignmentCSVExporter` (`.mdalign`), `AlignmentMspExporter`, `MztabFormatExporter` (`.mzTab`),
   optional project (`.mdproject` + `.mddata` zip, MessagePack).

Validation on macOS ARM64 (open reader, synthetic data from `tools/make_synthetic_mzml.py`):
LC-MS DDA — 3 samples, 35/32/40 peaks, 12/12 compounds annotated per sample, 18 aligned spots,
peak tables byte-identical to the closed reader; GC-MS EI — 2 samples, 12/12 deconvoluted
spectra annotated (scores 0.95–0.98), 12 aligned spots.

Method files are plain `Key: value` text parsed by `ConfigParser.ReadCommonParameter`
(case-insensitive keys such as `MS1 data type`, `Minimum peak height`, `MSP file path`,
`Together with alignment`); the full key list is in `tests/MSDIAL5/MsdialCoreTestApp/Parser/ConfigParser.cs`.

## 3. The closed component: RawDataHandler

Two builds exist. The repository ships only `RawDataHandler-Vendor-UnSupported` (netstandard2.0
+ net48 assemblies, 154 kB). Its public surface (decompiled with ILSpy for analysis; nothing was copied):

* `CompMs.RawDataHandler.Core.RawDataAccess` – constructor overloads
  `(path, fileID, getProfileData, isImagingMsData, isGuiProcess, [peakCutoff, mzTol, driftTol], correctedRts, bgWorker)`,
  `GetMeasurement()`, `ReadIonmobilityCalibrationInfo()`, `GetMaldiFrames()`, `GetMaldiFrameLaserInfo()`,
  imaging pixel queries, a set of vendor `*Dump` methods that all `throw NotSupportedException` in this build.
* `SpectrumParser` (static helpers used by MsdialCore/Dims/Imms/LcImMs: `GetRawMeasurementObj`,
  `LoadCollisionEnergyTargets`, `setSpectrumProperties` overloads, `GetAccumulatedMs1Spectrum`,
  `GetFrameRanges`, `AddToMassBinDictionary`), `MassBin`, `MassBinPool`, `RawDataExtension`.
* Readers: `Mzml.MzmlReader` (XmlTextReader, zlib via zlib.net, 32/64-bit floats, no numpress),
  `Imzml.ImzmlReader`, `NetCdf.NetCdfReader` (P/Invokes the native `netcdf` library),
  `Abf.ObjectConverter` (Reifycs `RDAM.dll`, which P/Invokes the native Windows `RDAM_DLL.dll`),
  `RawDataReader.ReadIABF` (MS-DIAL's own `.ibf` binary container; managed).
* The vendor-enabled build (`RawDataHandler`, not in the repo) additionally wraps Thermo, Agilent,
  Bruker, Sciex, Waters and Shimadzu SDKs – all Windows-only native/COM libraries.

Behaviour that downstream code relies on and that MIL-X reproduces exactly:

* spectra are re-sorted by (scan start time, drift time, MS level) and `Index = ScanNumber =
  OriginalIndex = position`;
* scan start time in seconds is converted to minutes (`Units.Minute`);
* `MsLevel == 0` is treated as 1 by `BaseDataProvider`;
* empty spectra get `LowestObservedMz/HighestObservedMz` from the scan window;
* accumulated (ion-mobility) spectra use 1 mDa bins keyed by `(int)(mz*1000)`, m/z rounded to 5
  decimals and intensities to integers;
* `CollisionEnergyTargets` = distinct rounded `RawSpectrum.CollisionEnergy` of MSn spectra.

Two defects were found in the closed mzML reader while building the parity test:

1. **The last peak of every spectrum is dropped** (both zlib and uncompressed arrays): a 195-peak
   spectrum comes back with 194 peaks and `HighestObservedMz` is wrong. MIL-X keeps all peaks;
   on the synthetic data set the downstream peak tables and alignment are nevertheless identical.
2. `BasePeakMz` is set to the highest m/z instead of the m/z of the most intense peak (unused downstream).

A third, more serious issue is in the open code: ~170 culture-sensitive `double.Parse` calls.
On a decimal-comma locale the closed reader parsed `scan start time 0.6 s` as 6000 s and every
threshold in the method file was misread, silently producing zero peaks. MIL-X forces the
invariant culture in every entry point.

## 4. MIL-X layout

```
milx/
  src/MilX.RawData/        open reader, drop-in for CompMs.RawDataHandler.Core (netstandard2.0; net8.0)
    Core/RawDataAccess.cs      same public API; dispatch per extension; RT-correction wrapper
    Core/SpectrumParser.cs, MassBin.cs, RawDataExtension.cs
    Mzml/MzmlReader.cs         streaming XmlReader (ReadSubtree per element), param groups, chromatograms,
                               ion mobility (MS:1002476, MS:1002815), progress
    Mzml/BinaryArrayDecoder.cs base64 + zlib (RFC1950) + MS-Numpress + 32/64-bit float/int
    Mzml/Numpress.cs           linear / pic / slof decoders
    Vendor/VendorConverter.cs  msconvert bridge (native or Docker), cache next to the file, env-var config
    Legacy/LegacyReaderPlugin.cs  reflection loader for the original closed dll (abf/ibf/cdf/imzML, and
                               vendor formats on Windows when the vendor-enabled build is present)
    Plugins/RawReaderPlugins.cs   IRawFileReaderPlugin discovery from ./plugins (e.g. a future Thermo
                               RawFileReader plugin, which needs a separately licensed library)
  src/MilX.Plugins.SciexWiff/  native .wiff reader on SCIEX's Clearcore2 SDK (ADR 0005), loaded from plugins/sciex
  src/MilX.Interop.OpenQuant/  OpenQuant component CSV export / .oqproj batch import
  src/MilX.Pipeline/       orchestration library (adapted from the console) – used by the GUI
                               (MilXProject .odproj, RawExplorer channels/TIC/BPC/XIC, per-sample aligned peaks)
  src/MilX.Desktop/        Avalonia 11 desktop application (macOS/Linux/Windows); OpenQuant's shell and palette:
                               Explorer (raw browsing), Analytics (per-sample EIC review grid of aligned spots,
                               statistics, metric plot), Method, Samples; New-project wizard
  tests/MilX.RawData.Tests 28 unit tests (encodings, numpress round trips, semantics, vendor bridge)
  tests/MilX.Pipeline.Tests end-to-end test on the synthetic data set
  tools/make_synthetic_mzml.py synthetic LC-MS/MS DDA generator (indexedmzML, 12 compounds, isotopes,
                               Na adducts, MS2, MSP library, method file)
  tools/RawDump/               diagnostic dumper used for closed-vs-open parity diffs
  scripts/                     setup-macos.sh, build-cli.sh, test.sh
  upstream-patches/            the 3 upstream modifications as a unified diff
  dist/                        published self-contained binaries
```

### 4.1 Raw data dispatch

```
RawDataAccess.GetMeasurement()
  ├─ any registered IRawFileReaderPlugin that CanRead(path)      (plugins/)
  ├─ .mzml                       -> MzmlReader
  ├─ .wiff (.wiff2)             -> MilX.Plugins.SciexWiff when present in plugins/sciex (native, ~1 s per file)
  ├─ .raw .d .wiff .wiff2 .lcd .qgd .lrp
  │     ├─ Windows + vendor-enabled legacy dll present -> legacy dll
  │     └─ otherwise VendorConverter.EnsureMzml(path)  -> <name>.mzML next to the file (or
  │        $MILX_MZML_CACHE), produced by msconvert (native exe on PATH / $MILX_MSCONVERT,
  │        or docker run --platform linux/amd64 proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses
  │        wine msconvert --mzML --64 --zlib --filter "peakPicking vendor msLevel=1-")
  │        then MzmlReader.  A pre-converted <name>.mzML newer than the source is used as-is.
  └─ .abf .ibf .iabf .cdf .imzml -> legacy closed dll if present (plugins/legacy or next to the app),
                                    else a clear message asking to convert to mzML.
```

### 4.2 Deliberate deviations from the closed reader (all switchable in `MzmlReaderOptions`)

| Deviation | Reason |
| --- | --- |
| Keep the last peak of each spectrum | closed reader bug |
| `BasePeakMz` = m/z of the most intense peak | closed reader bug |
| `RawSpectrum.CollisionEnergy` falls back to the precursor activation energy | enables the AIF/MSE per-CE deconvolution path and correct CE in exports for msconvert output, which stores CE only in `<activation>` |
| `SelectedIonMz` falls back to the isolation target | some converters omit `<selectedIon>` |
| TIC computed when `MS:1000285` is absent | |
| `referenceableParamGroup` fully resolved | closed reader only recognises two hard-coded group names |
| MS-Numpress, 32/64-bit integers, inverse reduced ion mobility (1/K0) supported | |

### 4.3 What still needs Windows

* ABF (Reifycs) – native `RDAM_DLL.dll`. Convert to mzML instead.
* NetCDF – needs `libnetcdf` (`brew install netcdf`) plus the legacy dll; low priority (GC-MS users can
  convert to mzML with msconvert/OpenMS).
* Vendor SDKs – handled through msconvert. A native Thermo path is possible on macOS with Thermo's
  .NET RawFileReader (separate license) through the plugin interface.
* The WPF GUI, ChartDrawing, CommonMVVM, NCDK.Display, R.NET (Notame), MS-FINDER GUI.
* `System.Data.SQLite` has no osx-arm64 native library; MIL-X builds compile the EAD lipid
  database against `Microsoft.Data.Sqlite` instead (4th upstream patch, verified by upstream tests).
* `libinchi` (InChIKey generation for lipid names, MassBank export) needs a macOS build of InChI;
  everything else in NCDK is managed.
