#!/usr/bin/env bash
# Fetches the SCIEX WIFF Reader Distributable SDK assemblies (Clearcore2.*) that the native
# .wiff plugin links against. They are redistributed by the open-source `alpharaw` package
# (MannLabs) under SCIEX's "WIFF Reader Distributable Beta SDK" redistribution license, which
# is copied next to them (SCIEX_LICENSE.txt). They are NOT committed to this repository.
#
#   scripts/fetch-sciex-assemblies.sh            -> milx/vendor/sciex/
#   scripts/fetch-sciex-assemblies.sh /path/dir  -> copy from an existing alpharaw/ext/sciex dir
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
DEST="$ROOT/vendor/sciex"
mkdir -p "$DEST"

if [ -n "${1:-}" ] && [ -f "$1/Clearcore2.Data.dll" ]; then
  cp "$1"/*.dll "$1"/*.txt "$DEST/" 2>/dev/null || true
  echo "[sciex] copied from $1"
else
  LOCAL="$(python3 -c "import alpharaw, os; print(os.path.join(os.path.dirname(alpharaw.__file__), 'ext', 'sciex'))" 2>/dev/null || true)"
  if [ -n "$LOCAL" ] && [ -f "$LOCAL/Clearcore2.Data.dll" ]; then
    cp "$LOCAL"/*.dll "$LOCAL"/*.txt "$DEST/" 2>/dev/null || true
    echo "[sciex] copied from installed alpharaw: $LOCAL"
  else
    TMP="$(mktemp -d)"
    echo "[sciex] downloading the alpharaw wheel (no dependencies) ..."
    python3 -m pip download alpharaw --no-deps --only-binary=:all: -d "$TMP" >/dev/null
    WHEEL="$(ls "$TMP"/alpharaw-*.whl | head -1)"
    ( cd "$TMP" && unzip -q -o "$WHEEL" 'alpharaw/ext/sciex/*' )
    cp "$TMP"/alpharaw/ext/sciex/*.dll "$TMP"/alpharaw/ext/sciex/*.txt "$DEST/" 2>/dev/null || true
    rm -rf "$TMP"
    echo "[sciex] extracted from $WHEEL"
  fi
fi
ls "$DEST" | wc -l | xargs -I{} echo "[sciex] {} files in $DEST"
echo "[sciex] read the license before redistributing: $DEST/SCIEX_LICENSE.txt"
