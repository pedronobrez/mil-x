# ADR 0003: Force the invariant culture in every OpenDIAL entry point

## Status
Accepted (2026-09-06)

## Context
MS-DIAL parses and formats numbers with the thread's current culture in about 170 places
(method files, MSP/text libraries, mzML cvParam values, exports). On the development Mac
(locale `en_BR`, decimal comma) the closed reader turned a scan time of `0.6` s into `6000` s and
the console produced zero peaks without any error. Windows users in decimal-comma locales are
exposed to the same defect.

## Decision
Every executable (console, desktop app, tests) sets `CultureInfo.DefaultThreadCurrentCulture`,
`DefaultThreadCurrentUICulture`, `CurrentCulture` and `CurrentUICulture` to
`CultureInfo.InvariantCulture` before anything else, and the published launcher also sets
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`. `OpenDIAL.RawData` itself always parses with
`CultureInfo.InvariantCulture` regardless of the host culture.

## Consequences
* Deterministic results on every OS and locale; file formats stay `.`-decimal as upstream expects.
* UI number formatting in the desktop app is culture-invariant (acceptable for a scientific tool).
