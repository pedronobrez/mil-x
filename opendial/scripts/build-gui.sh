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
echo "[build-gui] done: $OUT/OpenDIAL"
