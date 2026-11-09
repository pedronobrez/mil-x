#!/usr/bin/env bash
# Builds the MIL-X command line: milx (the shared mil-cli/1 contract, docs/CLI-CONTRACT.md, with the
# MS-DIAL workflows run in-process) and the MS-DIAL 5 console (MSDIALCUI, as milx-cli) with the open
# MilX.RawData reader, published self-contained for the current (or given) runtime.
#   scripts/build-cli.sh              -> dist/milx-cli-<rid>/milx and dist/milx-cli-<rid>/milx-cli
#   scripts/build-cli.sh osx-arm64    -> explicit runtime identifier (osx-arm64, osx-x64, linux-x64, linux-arm64, win-x64)
#   linux-arm64 is the Ubuntu 22.04 aarch64 target of the contract: a VM with ~3 GB RAM and no .NET; the
#   publish is self-contained so nothing has to be installed, and milx selftest says whether .wiff reads there.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
UPSTREAM="$(cd "$ROOT/.." && pwd)/MsdialWorkbench-MSDIAL-v5.5.260817"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

RID="${1:-}"
if [ -z "$RID" ]; then
  case "$(uname -s)-$(uname -m)" in
    Darwin-arm64) RID=osx-arm64 ;;
    Darwin-x86_64) RID=osx-x64 ;;
    Linux-x86_64) RID=linux-x64 ;;
    Linux-aarch64) RID=linux-arm64 ;;
    *) echo "unknown platform, pass a runtime identifier"; exit 1 ;;
  esac
fi
CONFIG="${CONFIG:-Release}"
OUT="$ROOT/dist/milx-cli-$RID"
echo "[build-cli] publishing milx ($CONFIG, $RID) -> $OUT"
dotnet publish "$ROOT/src/MilX.Cli/MilX.Cli.csproj" \
  --configuration "$CONFIG" --runtime "$RID" --self-contained \
  -p:UseOpenRawData=true -p:SkipLibraryDownload=true -p:DebugType=None -p:DebugSymbols=false \
  -p:ErrorOnDuplicatePublishOutputFiles=false -o "$OUT"
echo "[build-cli] publishing MSDIALCUI ($CONFIG, $RID) with the open reader -> $OUT"
dotnet publish "$UPSTREAM/tests/MSDIAL5/MsdialCoreTestApp/MsdialCoreTestApp.csproj" \
  --configuration "$CONFIG" --framework net8 --runtime "$RID" --self-contained \
  -p:UseOpenRawData=true -p:SkipLibraryDownload=true -p:DebugType=None -p:DebugSymbols=false \
  -p:ErrorOnDuplicatePublishOutputFiles=false -o "$OUT"
# optional native SCIEX .wiff plugin (needs vendor/sciex from scripts/fetch-sciex-assemblies.sh)
if [ -f "$ROOT/vendor/sciex/Clearcore2.Data.AnalystDataProvider.dll" ]; then
  echo "[plugins] building the native SCIEX .wiff plugin"
  dotnet publish "$ROOT/src/MilX.Plugins.SciexWiff/MilX.Plugins.SciexWiff.csproj" -c Release -o "$OUT/plugins/sciex" -p:DebugType=None -p:DebugSymbols=false --nologo -v quiet
  # the plugin's own copies of Common/MilX.RawData must not shadow the application's
  rm -f "$OUT/plugins/sciex/Common.dll" "$OUT/plugins/sciex/MilX.RawData.dll" "$OUT/plugins/sciex/NCDK.dll" "$OUT"/plugins/sciex/MathNet.Numerics.dll "$OUT"/plugins/sciex/Accord*.dll "$OUT"/plugins/sciex/MessagePack*.dll "$OUT"/plugins/sciex/Newtonsoft.Json.dll "$OUT"/plugins/sciex/Splash.dll 2>/dev/null || true
else
  echo "[plugins] vendor/sciex not present: .wiff will go through msconvert (run scripts/fetch-sciex-assemblies.sh for native reading)"
fi
cp "$UPSTREAM/LGPL.txt" "$UPSTREAM/THIRD-PARTY-LICENSE-README.md" "$OUT/" 2>/dev/null || true
cat > "$OUT/milx-cli" <<'EOF'
#!/usr/bin/env bash
# Thin launcher: forces invariant globalization (MS-DIAL parses numbers with the current culture).
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
exec "$HERE/MSDIALCUI" "$@"
EOF
chmod +x "$OUT/milx-cli" "$OUT/MSDIALCUI" "$OUT/milx" 2>/dev/null || true
echo "[build-cli] done: $OUT/milx --version · $OUT/milx-cli --version"
# a cross-published binary cannot run here; say so instead of failing the build
case "$RID" in
  osx-*) [ "$(uname -s)" = Darwin ] && "$OUT/milx" --version || echo "[build-cli] built for $RID; run milx selftest on that machine" ;;
  linux-*) [ "$(uname -s)" = Linux ] && "$OUT/milx" --version || echo "[build-cli] built for $RID; run milx selftest on that machine" ;;
  *) echo "[build-cli] built for $RID" ;;
esac
