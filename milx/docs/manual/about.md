---
title: About MIL-X
section: Help
order: 62
summary: License, attribution, citation, and what MIL-X is not.
---

# About MIL-X

MIL-X is an open, community-maintained port of the MS-DIAL 5 processing engine — peak picking,
MS/MS deconvolution, spectral-library annotation and alignment — to macOS, Linux and Windows,
with a cross-platform user interface of its own.

## The name

**MIL** is *Multi-omics Identification Laboratory*; **X** is exploration, the untargeted half of
the work, where a batch goes in and a few thousand features come out and the job is to decide what
they are. **MIL-Q** is quantification, the targeted half — today's OpenQuant, renamed when its own
1.0 comes. *Explore, then quantify* is what the two do together, and **Export to OpenQuant…** is
the hand-off.

Until 1.0 this program was **OpenDIAL**. The old name is still understood wherever it was written
down: `.odproj` projects open, `OPENDIAL_*` variables work, the old settings folder is read once.
[[versions#1.0.0 — September 2026]] lists every one of them.

The mark is a cow's head, face on. MIL-X is said like *milks*; MS-DIAL's own icon is a rotary
telephone, so the pun is a homage, not a joke at the parent's expense. The name is set in a
monospace face because it is a designation, not a word.

## License

MIL-X is built on the MS-DIAL source code released under the GNU Lesser General Public License
v3.0, and is itself distributed under LGPL-3.0. The MS-DIAL sources are used almost unchanged;
the six patches are listed in [[architecture#The six upstream patches]] and shipped as a diff.

The native `.wiff` reader links against SCIEX's Clearcore2 assemblies, redistributed under SCIEX's
WIFF Reader Distributable SDK license, which is copied beside them in `plugins/sciex`. They are not
part of the MIL-X repository. The msconvert bridge uses ProteoWizard, whose Docker image carries
the vendor libraries under their own licenses. The interface uses the Inter and JetBrains Mono NL
typefaces under the SIL Open Font License.

## Attribution

MIL-X is not affiliated with, endorsed by, or supported by RIKEN, UC Davis or the MS-DIAL
development team. If you use MIL-X in a publication, please cite the original MS-DIAL papers:

- Tsugawa, H. et al. *MS-DIAL: data-independent MS/MS deconvolution for comprehensive metabolome analysis.* Nature Methods 12, 523–526 (2015).
- Tsugawa, H. et al. *A lipidome atlas in MS-DIAL 4.* Nature Biotechnology 38, 1159–1163 (2020).

The orthogonal model follows Trygg, J. and Wold, S., *Orthogonal projections to latent structures
(O-PLS)*, Journal of Chemometrics 16, 119–128 (2002). The drift correction follows the
QC-RLSC procedure of Dunn, W. B. et al., Nature Protocols 6, 1060–1083 (2011).

## What MIL-X is not

It is not a re-implementation of MS-DIAL's algorithms: the numbers a run produces are MS-DIAL's,
and are validated against MS-DIAL on Windows on the same data (see
[[processing#Numbers to expect]]). It is not a replacement for MS-DIAL's Windows interface either: imaging, ion mobility
browsing, MS-FINDER integration and the Notame statistics are not ported. And it is not a
targeted-quantitation tool; that is OpenQuant's job, and [[openquant]] is the bridge.
