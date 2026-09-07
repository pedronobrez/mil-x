#!/usr/bin/env bash
# Runs the OpenDIAL test suites and the end-to-end synthetic LC-MS/MS pipeline check.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
UPSTREAM="$(cd "$ROOT/.." && pwd)/MsdialWorkbench-MSDIAL-v5.5.260817"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

echo "[test] OpenDIAL.RawData unit tests"
dotnet test "$ROOT/tests/OpenDIAL.RawData.Tests/OpenDIAL.RawData.Tests.csproj" -c Release --nologo -v minimal

echo "[test] OpenQuant interop + SCIEX plugin tests"
dotnet test "$ROOT/tests/OpenDIAL.Interop.OpenQuant.Tests/OpenDIAL.Interop.OpenQuant.Tests.csproj" -c Release --nologo -v minimal -p:UseOpenRawData=true
dotnet test "$ROOT/tests/OpenDIAL.Plugins.SciexWiff.Tests/OpenDIAL.Plugins.SciexWiff.Tests.csproj" -c Release --nologo -v minimal
echo "[test] headless interface tests (the real views, no display)"
dotnet test "$ROOT/tests/OpenDIAL.Desktop.Tests/OpenDIAL.Desktop.Tests.csproj" -c Release --nologo -v minimal -p:UseOpenRawData=true

if [ -f "$ROOT/tests/OpenDIAL.Pipeline.Tests/OpenDIAL.Pipeline.Tests.csproj" ]; then
  echo "[test] OpenDIAL.Pipeline tests"
  dotnet test "$ROOT/tests/OpenDIAL.Pipeline.Tests/OpenDIAL.Pipeline.Tests.csproj" -c "Debug vendor unsupported" --nologo -v minimal -p:UseOpenRawData=true
fi

echo "[test] end-to-end: synthetic LC-MS/MS DDA through MSDIALCUI (open reader)"
WORK="$(mktemp -d)"
python3 "$ROOT/tools/make_synthetic_mzml.py" --out "$WORK" --samples 3 >/dev/null
OUT="$ROOT/build/cli-open"
dotnet build "$UPSTREAM/tests/MSDIAL5/MsdialCoreTestApp/MsdialCoreTestApp.csproj" --configuration Release --framework net8 \
  -p:UseOpenRawData=true -p:SkipLibraryDownload=true -o "$OUT" --nologo -v quiet
mkdir -p "$WORK/out"
dotnet "$OUT/MSDIALCUI.dll" lcms -i "$WORK" -o "$WORK/out" -m "$WORK/method_lcms_dda.txt" -p > "$WORK/out/run.log" 2>&1
for f in "$WORK"/out/*.mdpeak; do
  n=$(( $(wc -l < "$f") - 1 ))
  a=$(awk -F'\t' 'NR>1 && $2!="" && $2!="Unknown"{print $2}' "$f" | sort -u | wc -l | tr -d ' ')
  echo "  $(basename "$f"): $n peaks, $a annotated compounds"
  [ "$n" -ge 25 ] || { echo "FAIL: too few peaks"; exit 1; }
  [ "$a" -ge 10 ] || { echo "FAIL: too few annotations"; exit 1; }
done
rm -rf "$WORK"

echo "[test] end-to-end: synthetic GC-MS (EI) through MSDIALCUI gcms"
WORK="$(mktemp -d)"
python3 "$ROOT/tools/make_synthetic_mzml.py" --out "$WORK" --samples 2 --mode gcms >/dev/null
mkdir -p "$WORK/out"
dotnet "$OUT/MSDIALCUI.dll" gcms -i "$WORK" -o "$WORK/out" -m "$WORK/method_gcms_ei.txt" > "$WORK/out/run.log" 2>&1
for f in "$WORK"/out/*.mdscan; do
  n=$(( $(wc -l < "$f") - 1 ))
  echo "  $(basename "$f"): $n deconvoluted EI spectra"
  [ "$n" -ge 10 ] || { echo "FAIL: too few GC-MS spectra"; exit 1; }
done
rm -rf "$WORK"
echo "[test] all good"
