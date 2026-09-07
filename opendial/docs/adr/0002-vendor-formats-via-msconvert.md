# ADR 0002: Vendor formats are converted to mzML with ProteoWizard msconvert

## Status
Accepted (2026-09-06)

## Context
The requested inputs are Thermo `.raw`, Agilent/Bruker `.d`, Sciex `.wiff`/`.wiff2` and mzML.
Every vendor SDK is a Windows-only native or COM library; Bruker's TDF SDK has a Linux build
but no macOS build; Thermo's RawFileReader is a .NET library that runs on macOS but is
separately licensed. Reverse-engineering the proprietary container formats is neither feasible
nor legally comfortable.

## Decision
`OpenDIAL.RawData` treats vendor files as "mzML that has not been produced yet": on first
access it runs msconvert (native executable when available, otherwise the official
`proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses` Docker image, which bundles the
vendor readers under Wine and runs on Apple Silicon through x86-64 emulation), writes
`<name>.mzML` next to the source (or into a configurable cache), and reads that. Conversion
applies vendor centroiding (`peakPicking vendor msLevel=1-`), matching MS-DIAL's default
"Centroid" data type. Pre-converted mzML files are used as-is. A plugin interface allows a
native Thermo reader later without touching the core.

## Consequences
* One code path (mzML) for all formats; MS-DIAL's algorithms are unaffected.
* Requires Docker Desktop or msconvert; the first read of a file is slow under emulation.
* Profile-mode workflows must set `OPENDIAL_MSCONVERT_PROFILE=1` (and MS-DIAL's data type).
* Ion-mobility vendor data (Bruker TIMS, Agilent IM-QTOF, Waters) converts to mzML with
  drift-time scans; the LC-IM-MS pipeline consumes them through the same reader.
