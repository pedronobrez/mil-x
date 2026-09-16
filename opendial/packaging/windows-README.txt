OpenDIAL @VERSION@ — Windows x64

    OpenDIAL.exe

That is the whole installation: the .NET runtime is inside this folder, so nothing else has to be
installed. There is no installer and nothing is written to the registry; move the folder wherever
you keep your tools and make a shortcut to OpenDIAL.exe.

SmartScreen
    The build is not signed with a Windows code-signing certificate, so the first launch shows
    "Windows protected your PC". More info > Run anyway. Unblock the zip before extracting
    (right-click the .zip > Properties > Unblock) and Windows will stop marking every file inside.

Native .wiff reading
    The reader is in this build; SCIEX's Clearcore2 SDK is not, because its licence forbids passing
    it on. Accept that licence, fetch the assemblies yourself, and put them in a plugins\\sciex folder
    beside the application: .wiff is then read natively, with nothing else installed — no Analyst,
    no ProteoWizard. scripts/fetch-sciex-assemblies.sh (or .ps1 on Windows) in the source tree
    fetches them from the alpharaw package, which redistributes them under SCIEX's own
    redistribution licence.
    Without them the application says so and .wiff goes through msconvert, when that is on PATH.

Note
    MS-DIAL 5 itself runs on Windows, and on Windows it does more than this port does — ion
    mobility and imaging among it. This build exists so a Windows machine can open and continue a
    review started on a Mac or on Linux, and so a mixed lab shares one set of files.

Manual
    Inside the application, menu Help, in English and Portuguese.

Licence
    GPL-3.0 — see LICENSE.txt. What was changed in the MS-DIAL upstream is listed in NOTICE.txt.
