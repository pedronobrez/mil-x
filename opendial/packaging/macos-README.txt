OpenDIAL @VERSION@ — macOS (Apple silicon)

Drag OpenDIAL.app onto the Applications folder beside it.

First launch
    The build is ad-hoc signed, not notarised, so double-clicking gives "unidentified developer".
    Right-click the app > Open, once. macOS remembers the answer.
    It will also ask for access to your Documents folder — say Allow, or the app cannot read your
    data where it lives.

Raw data
    .wiff is read natively once the SCIEX Clearcore2 SDK is in place; its licence forbids
    redistribution, so fetch it yourself with scripts/fetch-sciex-assemblies.sh from the source
    tree. mzML is read directly, and other vendor formats go through msconvert when it is on PATH.

Manual
    Inside the application, menu Help, in English and Portuguese.

Licence
    GPL-3.0 — see LICENSE.txt. What was changed in the MS-DIAL upstream is listed in NOTICE.txt.
