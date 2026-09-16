MIL-X @VERSION@ — macOS (Apple silicon)

Drag MIL-X.app onto the Applications folder beside it.

First launch
    The build is ad-hoc signed, not notarised, so double-clicking gives "unidentified developer".
    Right-click the app > Open, once. macOS remembers the answer.
    It will also ask for access to your Documents folder — say Allow, or the app cannot read your
    data where it lives.

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
