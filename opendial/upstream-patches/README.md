# Upstream patches

OpenDIAL keeps the MS-DIAL sources in `../../MsdialWorkbench-MSDIAL-v5.5.260817/` almost
untouched so that a newer upstream release can be dropped in. Exactly four files are modified;
`opendial-upstream.patch` is the unified diff (`orig/` holds the pristine copies).

| File | Change | Why |
| --- | --- | --- |
| `src/MSDIAL5/MsdialCore/MsdialCore.csproj` | The four `RawDataHandler*` PackageReferences get the extra condition `'$(UseOpenRawData)' != 'true'`; a `ProjectReference` to `opendial/src/OpenDIAL.RawData` is added under `'$(UseOpenRawData)' == 'true'` (path overridable with `-p:OpenDialRawDataProject=`). | Lets every upstream project build with the open, cross-platform reader instead of the closed NuGet package. Default behaviour is unchanged. |
| `src/MSDIAL5/MsdialCore/DataObj/EadLipidSqliteDatabase.cs` (+ `MsdialCore.csproj`) | Under `UseOpenRawData=true` the file compiles against `Microsoft.Data.Sqlite` through three `using` aliases (`#if OPENDIAL_SQLITE`); the package reference swaps `System.Data.SQLite.Core` for `Microsoft.Data.Sqlite 8.0.10`. | `System.Data.SQLite` has no native library for Apple Silicon or ARM Linux; `Microsoft.Data.Sqlite` bundles `e_sqlite3` for every RID. The upstream `EadLipidDatabaseTests` pass on macOS arm64 with the change. |
| `tests/MSDIAL5/MsdialCoreTestApp/Program.cs` | Forces `CultureInfo.InvariantCulture` as the first statement of `Main`. | MS-DIAL parses numbers with the current culture in ~170 places; on a decimal-comma locale (this Mac is `en_BR`) the pipeline silently found zero peaks. |
| `tests/MSDIAL5/MsdialCoreTestApp/Parser/AnalysisFilesParser.cs` | `fileDir + "\\" + name` replaced by `Path.Combine` for the `.rtc` path in the CSV import branch. | Backslash is a valid file-name character on macOS/Linux; the old code created files named `dir\name.rtc`. |

Re-apply on a fresh upstream checkout:

```bash
cd MsdialWorkbench-MSDIAL-v5.5.260817
patch -p1 < ../opendial/upstream-patches/opendial-upstream.patch
```
