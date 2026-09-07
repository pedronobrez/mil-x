# Porting status and user guide (macOS first, Linux next)

## TL;DR

| Component | macOS (arm64/x64) | Linux | Notes |
| --- | --- | --- | --- |
| MS-DIAL 5 engine (peak picking, MS2Dec, annotation, alignment, exports, project files) | **works** | works (CI) | unchanged upstream code, built with `-p:UseOpenRawData=true` |
| Console `MSDIALCUI` (`lcms`, `gcms`, `dims`, `imms`, `lcimms`, `msn`, `eic`) | **works, self-contained binary in `dist/`** | works (CI) | invariant culture forced; LC-MS DDA and GC-MS EI validated end to end on synthetic data (12/12 compounds each) |
| mzML / indexedmzML input | **works (open reader, tested)** | works | zlib, numpress, 32/64-bit, ion mobility |
| SCIEX `.wiff` | **works natively** (Clearcore2 plugin, ~1 s per file) | untested | `scripts/fetch-sciex-assemblies.sh`; see §2a |
| Thermo `.raw`, Agilent/Bruker `.d`, Sciex `.wiff2`, Shimadzu `.lcd` | **works through msconvert** (native or Docker) | same | conversion is automatic and cached; see §2 |
| `.abf` (Reifycs) | no | no | Windows-only native library; convert to mzML |
| `.ibf` / `.cdf` / `.imzML` | with the legacy dll | with the legacy dll (+ libnetcdf) | drop `RawDataHandler-Vendor-UnSupported.dll` into `plugins/legacy` |
| Desktop GUI | Avalonia app in `src/OpenDIAL.Desktop` (Explorer / Analytics / Method / Samples, OpenQuant's look) | same | the WPF GUI is not portable |
| EIEIO lipid SQLite DB (EAD lipidomics) | **works** (Microsoft.Data.Sqlite) | works | upstream `EadLipidDatabaseTests` pass on Apple Silicon |
| InChIKey generation (NCDK, libinchi) | needs `libinchi.dylib` next to the app | needs `libinchi.so` | `brew install inchi` |

## 1. Build and run the command line

```bash
bash opendial/scripts/setup-macos.sh          # .NET 8 SDK into ~/.dotnet (no sudo), NuGet source
bash opendial/scripts/build-cli.sh            # -> opendial/dist/opendial-cli-osx-arm64/
opendial/dist/opendial-cli-osx-arm64/opendial-cli lcms -i /data/run1 -o /data/run1/out -m method.txt -p
```

`-i` is a folder (all `.mzML`/vendor files inside), a single file, or a CSV listing files with
class/type columns. `-m` is an MS-DIAL method file (see `testdata/synthetic_dda/method_lcms_dda.txt`
for a complete LC-MS DDA example; keys are the same as the Windows console). `-p` also writes an
`.mdproject` that the Windows GUI (and the OpenDIAL desktop app) can open.

Development loop (no publish):

```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH
dotnet build MsdialWorkbench-MSDIAL-v5.5.260817/tests/MSDIAL5/MsdialCoreTestApp/MsdialCoreTestApp.csproj \
  -c Release -f net8 -p:UseOpenRawData=true -p:SkipLibraryDownload=true -o opendial/build/cli-open
dotnet opendial/build/cli-open/MSDIALCUI.dll lcms --help
bash opendial/scripts/test.sh                 # unit tests + synthetic end-to-end run
```

## 2. Vendor formats on macOS/Linux

The vendor SDKs (Thermo, Agilent MHDAC, Bruker Baf2Sql/TDF, Sciex WiffReader, Waters, Shimadzu)
are Windows-only binaries, so OpenDIAL converts vendor files to mzML with ProteoWizard's
`msconvert` the first time they are read and then reuses `<name>.mzML` next to the raw file
(or in `$OPENDIAL_MZML_CACHE`). Nothing changes in your workflow: point `-i` at the folder with
the `.raw`/`.d`/`.wiff` files.

Choose one converter:

1. **Docker (recommended on macOS).** Install Docker Desktop (enable Rosetta/x86-64 emulation on
   Apple Silicon) and `docker pull proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses`.
   OpenDIAL runs `docker run --platform linux/amd64 ... wine msconvert <file> --mzML --64 --zlib
   --filter "peakPicking vendor msLevel=1-"`. Conversion is slow under emulation (minutes per
   file) but exact, and covers all four requested formats plus Waters and Shimadzu.
2. **Native msconvert.** If you have a Windows box or a Linux box with the pwiz Docker image,
   convert there and copy the `.mzML` files; or set `OPENDIAL_MSCONVERT=/path/to/msconvert`.
3. **Thermo only:** Thermo's .NET `RawFileReader` runs natively on macOS/Linux but is distributed
   under Thermo's license; it can be wired in as an `IRawFileReaderPlugin` (see `Plugins/`).

Environment variables: `OPENDIAL_MSCONVERT`, `OPENDIAL_DOCKER`, `OPENDIAL_PWIZ_IMAGE`,
`OPENDIAL_DOCKER_PLATFORM`, `OPENDIAL_MZML_CACHE`, `OPENDIAL_MSCONVERT_ARGS`,
`OPENDIAL_MSCONVERT_PROFILE=1` (keep profile data), `OPENDIAL_MSCONVERT_FORCE=1` (ignore cache).

Keep the MS-DIAL setting "MS1/MS2 data type: Centroid" (the default); vendor peak picking is applied
during conversion.

## 2a. SCIEX .wiff natively

Run `bash opendial/scripts/fetch-sciex-assemblies.sh` once (it copies the Clearcore2 SDK from
the `alpharaw` Python package into `opendial/vendor/sciex`, together with SCIEX's redistribution
license) and rebuild with `scripts/build-cli.sh` / `scripts/build-gui.sh`: the plugin lands in
`<app>/plugins/sciex` and `.wiff` files (with their `.wiff.scan`) are read directly, the way
OpenQuant reads them. Options: `OPENDIAL_WIFF_SAMPLE` (index or name inside a multi-sample
batch), `OPENDIAL_WIFF_CENTROID=0` (keep profile data; then set "MS1/MS2 data type: Profile"),
`OPENDIAL_WIFF_MIN_INTENSITY`. Verified on TripleTOF 5600 acquisitions through the whole
LC-MS pipeline (peak picking, MS2 deconvolution, alignment).

## 3. Legacy formats (ibf, cdf, imzML, abf)

Copy `RawDataHandler-Vendor-UnSupported.dll` (extracted from
`MsdialWorkbench-MSDIAL-v5.5.260817/Assemblies/*.nupkg`, `lib/netstandard2.0/`) into
`<app>/plugins/legacy/`. OpenDIAL loads it by reflection for those extensions only. `.ibf` is pure
managed and works; `.cdf` needs `brew install netcdf` and the library visible to the process;
`.abf` needs the Windows-only Reifycs native library and cannot work on macOS.

## 4. Known limitations / next steps

* Alignment and annotation run in memory as upstream; very large studies should use
  `Alignment light mode: True` in the method file (upstream ADR-0002).
* GC-MS retention-index workflows need the RI dictionary files exactly as on Windows.
* Upstream unit tests on macOS arm64 (open reader): MsdialCoreTests 290/295, LcMsApi 66/66,
  GcMsApi 6/6, DimsCore 33/33, ImmsCore 56/56, LcImMsApi 52/52. The 5 failures are test
  infrastructure, not the port: two declare `[DeploymentItem(@"Resources\Export\...")]` with
  Windows path separators and three compare exported text against CRLF line endings.
* The desktop application is a new MVP; the Windows GUI's advanced views (statistics, molecular
  networking browser, MS-FINDER integration, imaging) are not ported.
* Linux is covered by CI (ubuntu-22.04) for the CLI; Docker-based conversion works the same way
  (no emulation needed on x86-64).


## The survey scan cache

A vendor file costs the same read every time a project is opened — about 25 seconds for one of the
ZenoTOF acquisitions here, and a review pass over eight of them started with three minutes of
waiting. After the first read the survey scans of a file are written to a cache directory, and every
chromatogram after that comes from there:

| | |
| --- | --- |
| vendor library, one 60 MB `.wiff` | 24 882 ms |
| the same file from the cache | 87 ms |
| what is stored | 484 scans, 4 295 640 centroids, 32 MB |

Entries are keyed by the file's own identity — path, size and last write time — so an edited or
replaced file never serves a stale one. Masses are kept as tenths of a millidalton in a 32-bit
integer, two orders finer than any tolerance a chromatogram is extracted with, which halves both the
file and the time to read it. The store is capped and evicted least-recently-used.

It lives in `~/Library/Caches/OpenDIAL/ms1` on macOS, `$XDG_CACHE_HOME/OpenDIAL/ms1` on Linux and
under `LocalAppData` on Windows; `OPENDIAL_CACHE` moves it. Preferences shows its size and empties
it. Deleting it by hand is safe: it costs one slow read.
