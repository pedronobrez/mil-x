OpenDIAL @VERSION@ — Linux x86_64

    ./OpenDIAL

That is the whole installation: the .NET runtime is inside this folder, so nothing else has to be
installed. If the file lost its permission bit in transit, put it back with  chmod +x OpenDIAL .

What the distribution has to provide is the X11 client libraries and fontconfig, which any desktop
Linux already has. On a bare container or a server image, install them first:

    Debian/Ubuntu   apt-get install -y libx11-6 libice6 libsm6 libfontconfig1 libicu-dev
    Fedora/RHEL     dnf install -y libX11 libICE libSM fontconfig libicu

Raw data
    mzML and mzXML are read directly. Vendor formats go through msconvert (ProteoWizard), which
    OpenDIAL calls when it finds it on PATH — on Linux that usually means the ProteoWizard docker
    image or a wine install.
    Native .wiff reading needs the SCIEX Clearcore2 SDK, which cannot be redistributed. Accept
    SCIEX's licence and fetch it yourself with scripts/fetch-sciex-assemblies.sh from the source
    tree; the files land in plugins/sciex next to this README.

Manual
    Inside the application, menu Help, in English and Portuguese.

Licence
    GPL-3.0 — see LICENSE.txt. What was changed in the MS-DIAL upstream is listed in NOTICE.txt.
