OpenDIAL @VERSION@ — macOS (Apple silicon)

Drag OpenDIAL.app onto the Applications folder beside it.

First launch
    The build is ad-hoc signed, not notarised, so double-clicking gives "unidentified developer".
    Right-click the app > Open, once. macOS remembers the answer.
    It will also ask for access to your Documents folder — say Allow, or the app cannot read your
    data where it lives.

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
