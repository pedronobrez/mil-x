# mil-x

**MS-DIAL 5 on macOS, Windows and Linux**, with an interface built for the part of the work that is
actually slow: deciding what a few thousand aligned features are.

MS-DIAL is the standard tool for untargeted metabolomics and lipidomics, and it runs on Windows.
This is a port of MS-DIAL 5.5.260817 to a native desktop application — not a virtual machine, not a
compatibility layer — with an open raw-data layer underneath it and a review workspace on top.

**MIL-X** is *Multi-omics Identification Laboratory*, X for exploration. Until 1.0 the program was
called OpenDIAL; everything it wrote under that name still opens. Its targeted counterpart,
OpenQuant, becomes MIL-Q.

> **Not affiliated with the MS-DIAL authors.** This is an independent port. Bugs you find here are
> almost certainly ours — report them here, not to them.

## What it does that the original does not

- **Runs on macOS, Windows and Linux**, as a native desktop application with the system's own
  file dialogs, dark mode and shortcuts. Downloads for all three are on the
  [releases page](https://github.com/pedronobrez/mil-x/releases).
- **Reads SCIEX `.wiff` natively**, on every platform, out of the box: the releases carry the
  components SCIEX's licence names as redistributable. Everything else goes through msconvert.
- **Reconciles the two polarities of a batch.** A batch acquired both ways is one project: it runs
  twice, pairs the compounds on the neutral molecule, shows both product spectra side by side, and
  computes the statistics on compounds rather than on ions — a compound both runs saw counted once.
- **Says why so few features are named.** The chosen library is read and reported rather than
  assumed, and its retention times are calibrated to the gradient actually run, or left out of the
  score when they belong to another one.
- **Keeps a record.** The review is written to the same `_tags.xml` MS-DIAL reads, and a run report
  gathers the counts, the method, the library, the injections and the figures into one file.

## What it does not do

- **No proprietary vendor formats in the open build**, except SCIEX as described above. This is the
  upstream's own restriction: the closed readers are not redistributable. Convert to mzML, or
  install msconvert and let the port call it.
- **Not a replacement for MS-DIAL on Windows.** The upstream does more there — ion mobility,
  imaging. The Windows build exists so a mixed lab shares one set of files.
- **Not signed.** The first launch needs a right-click ▸ Open on macOS and "More info ▸ Run anyway"
  on Windows.

## Building it

Needs the .NET 8 SDK. `scripts/setup-macos.sh` installs one into `~/.dotnet` without sudo if you
have none. The port lives under `milx/` (it was `opendial/` until 1.0).

```bash
milx/scripts/setup-macos.sh      # .NET 8, and the local NuGet source for the upstream
milx/scripts/build-gui.sh        # the desktop application
milx/scripts/make-app-bundle.sh  # MIL-X.app, ad-hoc signed
milx/scripts/test.sh             # every suite, plus two end-to-end runs on synthetic data
```

The bundle is **ad-hoc signed**, so macOS asks for file access again on every install and Gatekeeper
will want a right-click ▸ Open the first time.

## The manual

`milx/docs/manual/` in English, `milx/docs/manual/pt/` in Portuguese. Both are embedded in
the application under **Help**, and both build to a PDF with `milx/scripts/build-manual.py`.

Start at `getting-started.md`; `concepts.md` explains the vocabulary if you are new to MS-DIAL.

## Licence

**GPL-3.0** for the port — see `LICENSE`. The vendored MS-DIAL tree stays **LGPL-3.0** under its own
terms. `NOTICE` explains which is which and lists every change made to the upstream; read it before
copying anything out of here.

## Credit

MS-DIAL is by Hiroshi Tsugawa and the Systems Omics Laboratory. If this port is useful to your work,
cite their papers, not this repository — the science is theirs. This is plumbing.
