OpenDIAL @VERSION@ — Linux x86_64

    ./OpenDIAL

That is the whole installation: the .NET runtime is inside this folder, so nothing else has to be
installed. If the file lost its permission bit in transit, put it back with  chmod +x OpenDIAL .

What the distribution has to provide is the X11 client libraries and fontconfig, which any desktop
Linux already has. On a bare container or a server image, install them first:

    Debian/Ubuntu   apt-get install -y libx11-6 libice6 libsm6 libfontconfig1 libicu-dev
    Fedora/RHEL     dnf install -y libX11 libICE libSM fontconfig libicu

Native .wiff reading
    The reader is in this build; SCIEX's Clearcore2 SDK is not, because its licence forbids passing
    it on. Accept that licence, fetch the assemblies yourself, and put them in a plugins/sciex folder
    beside the application: .wiff is then read natively, with nothing else installed — no Analyst,
    no ProteoWizard. scripts/fetch-sciex-assemblies.sh (or .ps1 on Windows) in the source tree
    fetches them from the alpharaw package, which redistributes them under SCIEX's own
    redistribution licence.
    Without them the application says so and .wiff goes through msconvert, when that is on PATH.

Manual
    Inside the application, menu Help, in English and Portuguese.

Licence
    GPL-3.0 — see LICENSE.txt. What was changed in the MS-DIAL upstream is listed in NOTICE.txt.
