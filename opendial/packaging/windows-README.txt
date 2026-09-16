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
    .wiff is read natively: SCIEX's redistributable Clearcore2 components travel in plugins/sciex,
    under SCIEX's own redistribution licence, which is in SCIEX_LICENSE.txt beside them. Nothing
    else has to be installed for it — no Analyst, no ProteoWizard. Those assemblies are SCIEX's
    software, for research use only, and are not covered by this program's GPL licence.
    mzML and mzXML are read directly. Other vendor formats go through ProteoWizard's msconvert when
    it is on PATH.

Note
    MS-DIAL 5 itself runs on Windows, and on Windows it does more than this port does — ion
    mobility and imaging among it. This build exists so a Windows machine can open and continue a
    review started on a Mac or on Linux, and so a mixed lab shares one set of files.

Manual
    Inside the application, menu Help, in English and Portuguese.

Licence
    GPL-3.0 — see LICENSE.txt. What was changed in the MS-DIAL upstream is listed in NOTICE.txt.
