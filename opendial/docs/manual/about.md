---
title: About OpenDIAL
section: Help
order: 62
summary: License, attribution, citation, and what OpenDIAL is not.
---

# About OpenDIAL

OpenDIAL is an open, community-maintained port of the MS-DIAL 5 processing engine — peak picking,
MS/MS deconvolution, spectral-library annotation and alignment — to macOS, Linux and Windows,
with a cross-platform user interface of its own.

## License

OpenDIAL is built on the MS-DIAL source code released under the GNU Lesser General Public License
v3.0, and is itself distributed under LGPL-3.0. The MS-DIAL sources are used almost unchanged;
the six patches are listed in [[architecture#The six upstream patches]] and shipped as a diff.

The native `.wiff` reader links against SCIEX's Clearcore2 assemblies, redistributed under SCIEX's
WIFF Reader Distributable SDK license, which is copied beside them in `plugins/sciex`. They are not
part of the OpenDIAL repository. The msconvert bridge uses ProteoWizard, whose Docker image carries
the vendor libraries under their own licenses. The interface uses the Inter and JetBrains Mono NL
typefaces under the SIL Open Font License.

## Attribution

OpenDIAL is not affiliated with, endorsed by, or supported by RIKEN, UC Davis or the MS-DIAL
development team. If you use OpenDIAL in a publication, please cite the original MS-DIAL papers:

- Tsugawa, H. et al. *MS-DIAL: data-independent MS/MS deconvolution for comprehensive metabolome analysis.* Nature Methods 12, 523–526 (2015).
- Tsugawa, H. et al. *A lipidome atlas in MS-DIAL 4.* Nature Biotechnology 38, 1159–1163 (2020).

The orthogonal model follows Trygg, J. and Wold, S., *Orthogonal projections to latent structures
(O-PLS)*, Journal of Chemometrics 16, 119–128 (2002). The drift correction follows the
QC-RLSC procedure of Dunn, W. B. et al., Nature Protocols 6, 1060–1083 (2011).

## What OpenDIAL is not

It is not a re-implementation of MS-DIAL's algorithms: the numbers a run produces are MS-DIAL's,
and are validated against MS-DIAL on Windows on the same data (see
[[processing#Numbers to expect]]). It is not a replacement for MS-DIAL's Windows interface either: imaging, ion mobility
browsing, MS-FINDER integration and the Notame statistics are not ported. And it is not a
targeted-quantitation tool; that is OpenQuant's job, and [[openquant]] is the bridge.
