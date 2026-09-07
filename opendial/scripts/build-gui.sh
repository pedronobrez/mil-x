#!/usr/bin/env bash
# Publishes the OpenDIAL desktop app (Avalonia) as a self-contained folder for the current runtime.
#   scripts/build-gui.sh [osx-arm64|osx-x64|linux-x64|win-x64]   -> dist/opendial-desktop-<rid>/
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
RID="${1:-}"
if [ -z "$RID" ]; then
  case "$(uname -s)-$(uname -m)" in
    Darwin-arm64) RID=osx-arm64 ;; Darwin-x86_64) RID=osx-x64 ;; Linux-x86_64) RID=linux-x64 ;; Linux-aarch64) RID=linux-arm64 ;;
    *) echo "unknown platform, pass a runtime identifier"; exit 1 ;;
  esac
fi
OUT="$ROOT/dist/opendial-desktop-$RID"
dotnet publish "$ROOT/src/OpenDIAL.Desktop/OpenDIAL.Desktop.csproj" -c Release -r "$RID" --self-contained \
  -p:UseOpenRawData=true -p:SkipLibraryDownload=true -p:DebugType=None -p:DebugSymbols=false \
  -p:ErrorOnDuplicatePublishOutputFiles=false -o "$OUT"
# optional native SCIEX .wiff plugin (needs vendor/sciex from scripts/fetch-sciex-assemblies.sh)
if [ -f "$ROOT/vendor/sciex/Clearcore2.Data.AnalystDataProvider.dll" ]; then
  echo "[plugins] building the native SCIEX .wiff plugin"
  dotnet publish "$ROOT/src/OpenDIAL.Plugins.SciexWiff/OpenDIAL.Plugins.SciexWiff.csproj" -c Release -o "$OUT/plugins/sciex" -p:DebugType=None -p:DebugSymbols=false --nologo -v quiet
  # the plugin's own copies of Common/OpenDIAL.RawData must not shadow the application's
  rm -f "$OUT/plugins/sciex/Common.dll" "$OUT/plugins/sciex/OpenDIAL.RawData.dll" "$OUT/plugins/sciex/NCDK.dll" "$OUT"/plugins/sciex/MathNet.Numerics.dll "$OUT"/plugins/sciex/Accord*.dll "$OUT"/plugins/sciex/MessagePack*.dll "$OUT"/plugins/sciex/Newtonsoft.Json.dll "$OUT"/plugins/sciex/Splash.dll 2>/dev/null || true
else
  echo "[plugins] vendor/sciex not present: .wiff will go through msconvert (run scripts/fetch-sciex-assemblies.sh for native reading)"
fi
echo "[build-gui] done: $OUT/OpenDIAL"
