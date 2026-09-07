#!/usr/bin/env bash
# One-time developer setup for OpenDIAL on macOS (Apple Silicon or Intel) and Linux.
# - installs the .NET 8 SDK into ~/.dotnet without sudo (skipped when `dotnet` 8+ is present)
# - registers the local NuGet source that holds the upstream closed reader package
#   (only needed to build the "vendor unsupported" upstream configuration for parity tests)
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
UPSTREAM="$(cd "$ROOT/.." && pwd)/MsdialWorkbench-MSDIAL-v5.5.260817"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks 2>/dev/null | grep -qE '^(8|9|10)\.'; then
  echo "[setup] installing .NET 8 SDK into $HOME/.dotnet"
  TMP="$(mktemp -d)"
  curl -sSL https://dot.net/v1/dotnet-install.sh -o "$TMP/dotnet-install.sh"
  bash "$TMP/dotnet-install.sh" --channel 8.0 --install-dir "$HOME/.dotnet"
  rm -rf "$TMP"
  echo "[setup] add to your shell profile:  export DOTNET_ROOT=\"\$HOME/.dotnet\"; export PATH=\"\$HOME/.dotnet:\$HOME/.dotnet/tools:\$PATH\""
fi
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
dotnet --version

if [ -d "$UPSTREAM/Assemblies" ]; then
  if ! dotnet nuget list source | grep -q "msdial-local"; then
    dotnet nuget add source "$UPSTREAM/Assemblies" --name msdial-local
  fi
fi

echo "[setup] optional: install ProteoWizard msconvert (Docker) for vendor formats:"
echo "        docker pull proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses"
echo "[setup] done"
