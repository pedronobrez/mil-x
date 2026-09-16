OpenDIAL @VERSION@ — Linux x86_64

    ./OpenDIAL

That is the whole installation: the .NET runtime is inside this folder, so nothing else has to be
installed. If the file lost its permission bit in transit, put it back with  chmod +x OpenDIAL .

What the distribution has to provide is the X11 client libraries and fontconfig, which any desktop
Linux already has. On a bare container or a server image, install them first:

    Debian/Ubuntu   apt-get install -y libx11-6 libice6 libsm6 libfontconfig1 libicu-dev
    Fedora/RHEL     dnf install -y libX11 libICE libSM fontconfig libicu

Raw data
    .wiff is read natively: SCIEX's redistributable Clearcore2 components travel in plugins/sciex,
    under SCIEX's own redistribution licence, which is in SCIEX_LICENSE.txt beside them. Nothing
    else has to be installed for it — no Analyst, no ProteoWizard. Those assemblies are SCIEX's
    software, for research use only, and are not covered by this program's GPL licence.
    mzML and mzXML are read directly. Other vendor formats go through ProteoWizard's msconvert when
    it is on PATH.

Manual
    Inside the application, menu Help, in English and Portuguese.

Licence
    GPL-3.0 — see LICENSE.txt. What was changed in the MS-DIAL upstream is listed in NOTICE.txt.
