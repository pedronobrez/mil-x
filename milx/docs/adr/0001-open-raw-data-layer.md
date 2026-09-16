# ADR 0001: Replace the closed RawDataHandler with an open drop-in library

## Status
Accepted (2026-09-06)

## Context
MS-DIAL 5 reads raw data exclusively through the closed NuGet package `RawDataHandler`
(`CompMs.RawDataHandler.Core.RawDataAccess`). The repository ships only the
"vendor unsupported" build (mzML, imzML, NetCDF, ABF, IBF); the vendor-enabled build wraps
Windows-only SDKs. Both are opaque binaries, one of them depends on a native Windows DLL
(`RDAM_DLL.dll`), and the mzML reader silently drops the last peak of every spectrum.

## Decision
Create `MilX.RawData`, a netstandard2.0/net8.0 library that exposes the same public types
in the same namespaces (`RawDataAccess`, `SpectrumParser`, `MassBin`, `MassBinPool`,
`RawDataExtension`), so that every upstream project compiles unchanged. A single MSBuild
property (`UseOpenRawData=true`, patched into `MsdialCore.csproj`) switches the package
reference for the project reference. The library implements mzML natively, bridges vendor
formats through msconvert (ADR 0002), and can load the original closed dll by reflection for
formats it does not implement.

Numerical semantics that downstream algorithms depend on (ordering, positional indexing,
unit conversion, binning and rounding in accumulated spectra) are reproduced exactly and
guarded by a parity test against the closed reader; deliberate improvements are opt-out flags.

## Consequences
* MS-DIAL runs on macOS/Linux without any closed binary; results on mzML are identical
  downstream (verified on the synthetic data set) except that all peaks are now kept.
* ABF cannot be supported on macOS (Windows-only native dependency).
* Upstream updates require re-applying a 3-file patch (`upstream-patches/`).
