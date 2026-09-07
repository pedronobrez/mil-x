#!/usr/bin/env bash
# Builds the MS-DIAL 5 console (MSDIALCUI) with the open OpenDIAL.RawData reader and publishes
# a self-contained binary for the current (or given) runtime.
#   scripts/build-cli.sh              -> dist/opendial-cli-<rid>/
#   scripts/build-cli.sh osx-arm64    -> explicit runtime identifier (osx-arm64, osx-x64, linux-x64, linux-arm64, win-x64)
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
OUT="$ROOT/dist/opendial-cli-$RID"
echo "[build-cli] publishing MSDIALCUI ($CONFIG, $RID) with the open reader -> $OUT"
dotnet publish "$UPSTREAM/tests/MSDIAL5/MsdialCoreTestApp/MsdialCoreTestApp.csproj" \
  --configuration "$CONFIG" --framework net8 --runtime "$RID" --self-contained \
  -p:UseOpenRawData=true -p:SkipLibraryDownload=true -p:DebugType=None -p:DebugSymbols=false \
  -p:ErrorOnDuplicatePublishOutputFiles=false -o "$OUT"
cp "$UPSTREAM/LGPL.txt" "$UPSTREAM/THIRD-PARTY-LICENSE-README.md" "$OUT/" 2>/dev/null || true
cat > "$OUT/opendial-cli" <<'EOF'
#!/usr/bin/env bash
# Thin launcher: forces invariant globalization (MS-DIAL parses numbers with the current culture).
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
exec "$HERE/MSDIALCUI" "$@"
EOF
chmod +x "$OUT/opendial-cli" "$OUT/MSDIALCUI" 2>/dev/null || true
echo "[build-cli] done: $OUT/opendial-cli --version"
"$OUT/opendial-cli" --version
