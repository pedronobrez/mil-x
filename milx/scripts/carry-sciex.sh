#!/usr/bin/env bash
# Puts SCIEX's redistributable components into a built application, so it reads .wiff natively.
#
#   scripts/carry-sciex.sh <application folder>    # the folder holding MIL-X / MIL-X.exe
#
# Which files may travel is not a judgement call and not a list kept here. The SDK ships with its
# own licence — "END USER BETA SOFTWARE LICENSE AGREEMENT and REDISTRIBUTION LICENSE" — whose
# Appendix A names the redistributable components one by one. This reads that appendix. Anything
# else in vendor/sciex stays behind, and the licence travels beside the assemblies, which is what
# clause 4(e) asks of anyone passing them on.
#
# protobuf-net goes too: Clearcore2 needs it, it is not SCIEX's to license, and it comes under its
# own Apache 2.0 terms.
#
# The SDK is fetched by scripts/fetch-sciex-assemblies.sh (or .ps1) and is never in this
# repository. Without it this script says so and does nothing, which leaves an application that
# falls back to msconvert.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
SRC="${MILX_SCIEX_DIR:-$ROOT/vendor/sciex}"
LICENCE="$SRC/SCIEX_LICENSE.txt"

APP="${1:-}"
[ -n "$APP" ] || { echo "usage: $(basename "$0") <application folder>" >&2; exit 2; }
[ -d "$APP" ] || { echo "no such folder: $APP" >&2; exit 2; }

if [ ! -f "$LICENCE" ]; then
  echo "[sciex] no $LICENCE: nothing is redistributable without the appendix that grants it" >&2
  exit 1
fi

appendix () { sed -n '/APPENDIX A/,$p' "$LICENCE" | sed -n 's/^[[:space:]]*\([A-Za-z0-9_.]*\.dll\)[[:space:]]*$/\1/p'; }

DEST="$APP/plugins/sciex"
mkdir -p "$DEST"

carried=0
absent=""
while read -r dll; do
  [ -n "$dll" ] || continue
  if [ -f "$SRC/$dll" ]; then
    cp "$SRC/$dll" "$DEST/"
    carried=$((carried + 1))
  else
    absent="$absent $dll"
  fi
done < <(appendix)

[ -f "$SRC/protobuf-net.dll" ] && cp "$SRC/protobuf-net.dll" "$DEST/"
cp "$LICENCE" "$DEST/"

# and nothing of SCIEX's that the appendix does not name
listed="$(appendix)"
for f in "$DEST"/*.dll; do
  name="$(basename "$f")"
  case "$name" in
    MilX.*|System.*|Microsoft.*|protobuf-net.dll) continue ;;
  esac
  printf '%s\n' "$listed" | grep -qx "$name" || { echo "[sciex] leaving $name behind: Appendix A does not name it"; rm -f "$f"; }
done

echo "[sciex] $carried component(s) from Appendix A into $DEST, with the licence"
[ -n "$absent" ] && echo "[sciex] the appendix also names, and this SDK copy does not have:$absent"
exit 0
