OpenDIAL @VERSION@ — Windows x64

    OpenDIAL.exe

That is the whole installation: the .NET runtime is inside this folder, so nothing else has to be
installed. There is no installer and nothing is written to the registry; move the folder wherever
you keep your tools and make a shortcut to OpenDIAL.exe.

SmartScreen
    The build is not signed with a Windows code-signing certificate, so the first launch shows
    "Windows protected your PC". More info > Run anyway. Unblock the zip before extracting
    (right-click the .zip > Properties > Unblock) and Windows will stop marking every file inside.

Raw data
    mzML and mzXML are read directly. Vendor formats go through msconvert (ProteoWizard) when it is
    on PATH — on Windows that is the normal ProteoWizard install.
    Native .wiff reading needs the SCIEX Clearcore2 SDK, which cannot be redistributed. Accept
    SCIEX's licence and fetch it yourself with scripts/fetch-sciex-assemblies.sh from the source
    tree; the files belong in plugins\sciex next to this README.

Note
    MS-DIAL 5 itself runs on Windows, and on Windows it does more than this port does — ion
    mobility and imaging among it. This build exists so a Windows machine can open and continue a
    review started on a Mac or on Linux, and so a mixed lab shares one set of files.

Manual
    Inside the application, menu Help, in English and Portuguese.

Licence
    GPL-3.0 — see LICENSE.txt. What was changed in the MS-DIAL upstream is listed in NOTICE.txt.
