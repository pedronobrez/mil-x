# Upstream patches

OpenDIAL keeps the MS-DIAL sources in `../../MsdialWorkbench-MSDIAL-v5.5.260817/` almost
untouched so that a newer upstream release can be dropped in. Exactly six files are modified;
`opendial-upstream.patch` is the unified diff (`orig/` holds the pristine copies).

| File | Change | Why |
| --- | --- | --- |
| `src/Common/CommonStandard/MessagePack/LargeListMessagePack.cs` | `FillFromStream` reads until the requested count is in the buffer instead of issuing a single `Stream.Read`. | `Stream.Read` may return fewer bytes than asked for, and the `DeflateStream` inside a `ZipArchive` does so on a large entry. The partly-filled buffer went straight to the unsafe LZ4 decoder, which read past the valid data and killed the process with an `AccessViolationException` no catch block can stop: opening a processed `.mdproject` whose library is large crashed the application. |
| `src/MSDIAL5/MsdialCore/MsdialCore.csproj` | The four `RawDataHandler*` PackageReferences get the extra condition `'$(UseOpenRawData)' != 'true'`; a `ProjectReference` to `opendial/src/OpenDIAL.RawData` is added under `'$(UseOpenRawData)' == 'true'` (path overridable with `-p:OpenDialRawDataProject=`). | Lets every upstream project build with the open, cross-platform reader instead of the closed NuGet package. Default behaviour is unchanged. |
| `src/MSDIAL5/MsdialCore/DataObj/EadLipidSqliteDatabase.cs` (+ `MsdialCore.csproj`) | Under `UseOpenRawData=true` the file compiles against `Microsoft.Data.Sqlite` through three `using` aliases (`#if OPENDIAL_SQLITE`); the package reference swaps `System.Data.SQLite.Core` for `Microsoft.Data.Sqlite 8.0.10`. | `System.Data.SQLite` has no native library for Apple Silicon or ARM Linux; `Microsoft.Data.Sqlite` bundles `e_sqlite3` for every RID. The upstream `EadLipidDatabaseTests` pass on macOS arm64 with the change. |
| `tests/MSDIAL5/MsdialCoreTestApp/Program.cs` | Forces `CultureInfo.InvariantCulture` as the first statement of `Main`. | MS-DIAL parses numbers with the current culture in ~170 places; on a decimal-comma locale (this Mac is `en_BR`) the pipeline silently found zero peaks. |
| `tests/MSDIAL5/MsdialCoreTestApp/Process/CommonProcess.cs` | Every analysis file imported from a folder is given the project's acquisition type when it has none. | Only the file-list importer sets `AnalysisFileBean.AcquisitionType`; a folder import leaves it `None`, and the MS/MS of a peak is then collected under whichever rule `None` falls into. On a SCIEX IDA dataset this cost a quarter of the MS/MS assignments against the same data processed in the Windows GUI. |
| `tests/MSDIAL5/MsdialCoreTestApp/Parser/AnalysisFilesParser.cs` | `fileDir + "\\" + name` replaced by `Path.Combine` for the `.rtc` path in the CSV import branch. | Backslash is a valid file-name character on macOS/Linux; the old code created files named `dir\name.rtc`. |

Re-apply on a fresh upstream checkout:

```bash
cd MsdialWorkbench-MSDIAL-v5.5.260817
patch -p1 < ../opendial/upstream-patches/opendial-upstream.patch
```
