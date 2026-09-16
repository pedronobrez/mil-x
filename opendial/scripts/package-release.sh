#!/usr/bin/env bash
# Builds the downloadable archives for a release, one per platform.
#
#   scripts/package-release.sh [--only linux|linux-arm|windows|macos|macos-intel] [--skip-build]
#       -> dist/release/OpenDIAL-<version>-linux-x86_64.tar.gz
#          dist/release/OpenDIAL-<version>-linux-arm64.tar.gz
#          dist/release/OpenDIAL-<version>-windows-x64.zip
#          dist/release/OpenDIAL-<version>-macos-arm64.dmg
#          dist/release/OpenDIAL-<version>-macos-x86_64.dmg
#          dist/release/SHA256SUMS
#
# Every archive is self-contained: the .NET runtime travels inside it, so nothing has to be
# installed first. What does not travel is the SCIEX Clearcore2 SDK — its licence forbids
# redistribution — so .wiff reading is set up on the user's machine by scripts/fetch-sciex-assemblies.sh.
#
# The archives carry SCIEX's redistributable components, so .wiff is read natively out of the box.
# Which files those are is not a judgement call: the SDK's own licence — "END USER BETA SOFTWARE
# LICENSE AGREEMENT and REDISTRIBUTION LICENSE" — ends in an Appendix A that names them one by one,
# and carry_sciex below reads that appendix rather than trusting a list kept here. Anything in
# vendor/sciex that the appendix does not name stays behind, and the licence itself travels beside
# the assemblies, which is what its clause 4(e) asks for.
#
# The application is built with OPENDIAL_SHIP_SCIEX=false and the components are added afterwards,
# so that the build step never decides this and the check at the end has one place to look.
#
# The Windows and Linux builds are cross-compiled; the macOS one can only be built on macOS,
# because the bundle needs codesign and iconutil.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
REPO="$(cd "$ROOT/.." && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

ONLY=""
SKIP_BUILD=0
while [ $# -gt 0 ]; do
  case "$1" in
    --only) ONLY="$2"; shift 2 ;;
    --skip-build) SKIP_BUILD=1; shift ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

VERSION="${OPENDIAL_VERSION:-$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$ROOT/Directory.Build.props" | head -1)}"
[ -n "$VERSION" ] || { echo "no <Version> in Directory.Build.props" >&2; exit 1; }
OUT="$ROOT/dist/release"
mkdir -p "$OUT"

wanted () { [ -z "$ONLY" ] || [ "$ONLY" = "$1" ]; }

# the one thing every platform's build has in common
export OPENDIAL_SHIP_SCIEX=false

# The licence and the notice of what was changed upstream travel with every binary — the GPL asks
# for the first and the LGPL for the second.
carry_paperwork () { # <folder>
  cp "$REPO/LICENSE" "$1/LICENSE.txt"
  cp "$REPO/NOTICE"  "$1/NOTICE.txt"
}

# SCIEX's redistributable components into a staged application; the appendix of their own licence
# decides which, and scripts/carry-sciex.sh is the one place that reads it, so the workflow that
# builds the Windows and Linux artifacts does exactly the same thing.
carry_sciex () { "$HERE/carry-sciex.sh" "$1"; }

# One text per platform, kept in packaging/ so the GitHub workflow that builds the Windows and
# Linux artifacts ships the same words as a local build.
readme () { # <platform> <destination>
  sed "s/@VERSION@/$VERSION/g" "$ROOT/packaging/$1-README.txt" > "$2"
}

# ---- Linux ------------------------------------------------------------------------------------
# x86_64 is what a desktop or a cluster node runs; arm64 is a Raspberry-class box, an Ampere VM, or
# an ARM laptop. Same application, and the .NET publish cross-compiles either from here.
linux_archive () { # <runtime identifier> <name in the archive>
  local rid="$1" arch="$2"
  echo "== $rid"
  [ "$SKIP_BUILD" = "1" ] || "$HERE/build-gui.sh" "$rid" >/dev/null
  local src="$ROOT/dist/opendial-desktop-$rid"
  local stage; stage="$(mktemp -d)/OpenDIAL-$VERSION-$arch"
  mkdir -p "$stage"
  cp -R "$src"/. "$stage/"
  chmod +x "$stage/OpenDIAL"
  carry_sciex "$stage"
  carry_paperwork "$stage"
  readme linux "$stage/README.txt"
  ( cd "$(dirname "$stage")" && tar -czf "$OUT/OpenDIAL-$VERSION-$arch.tar.gz" "$(basename "$stage")" )
  rm -rf "$(dirname "$stage")"
  echo "   $OUT/OpenDIAL-$VERSION-$arch.tar.gz"
}

if wanted linux;     then linux_archive linux-x64   linux-x86_64; fi
if wanted linux-arm; then linux_archive linux-arm64 linux-arm64;  fi

# ---- Windows ----------------------------------------------------------------------------------
if wanted windows; then
  echo "== win-x64"
  [ "$SKIP_BUILD" = "1" ] || "$HERE/build-gui.sh" win-x64 >/dev/null
  SRC="$ROOT/dist/opendial-desktop-win-x64"
  STAGE="$(mktemp -d)/OpenDIAL-$VERSION-windows-x64"
  mkdir -p "$STAGE"
  cp -R "$SRC"/. "$STAGE/"
  carry_sciex "$STAGE"
  carry_paperwork "$STAGE"

  readme windows "$STAGE/README.txt"

  ( cd "$(dirname "$STAGE")" && zip -q -r "$OUT/OpenDIAL-$VERSION-windows-x64.zip" "$(basename "$STAGE")" )
  rm -rf "$(dirname "$STAGE")"
  echo "   $OUT/OpenDIAL-$VERSION-windows-x64.zip"
fi

# ---- macOS ------------------------------------------------------------------------------------
# Apple silicon is what this is developed on; the Intel build is for the Macs still on a desk. Both
# are built here because a bundle needs codesign and iconutil, which only exist on macOS.
macos_dmg () { # <runtime identifier> <name in the archive>
  local rid="$1" arch="$2"
  echo "== $rid"
  if [ "$SKIP_BUILD" = "1" ]; then
    "$HERE/make-app-bundle.sh" "$rid" --skip-publish >/dev/null
  else
    "$HERE/make-app-bundle.sh" "$rid" >/dev/null
  fi
  local stage; stage="$(mktemp -d)/OpenDIAL $VERSION"
  mkdir -p "$stage"
  ditto "$ROOT/dist/OpenDIAL.app" "$stage/OpenDIAL.app"
  # changing what is inside a signed bundle invalidates the signature, so it is signed again after
  carry_sciex "$stage/OpenDIAL.app/Contents/MacOS"
  codesign --force --deep --sign - --timestamp=none "$stage/OpenDIAL.app" 2>/dev/null
  codesign --verify --deep "$stage/OpenDIAL.app" || { echo "the bundle lost its signature" >&2; exit 1; }
  ln -s /Applications "$stage/Applications"
  carry_paperwork "$stage"
  readme macos "$stage/README.txt"
  local dmg="$OUT/OpenDIAL-$VERSION-$arch.dmg"
  rm -f "$dmg"
  hdiutil create -quiet -volname "OpenDIAL $VERSION" -srcfolder "$stage" -ov -format UDZO "$dmg"
  rm -rf "$(dirname "$stage")"
  echo "   $dmg"
}

if [ "$(uname -s)" != "Darwin" ]; then
  if wanted macos || wanted macos-intel; then echo "== macos: skipped (bundles can only be built on macOS)"; fi
else
  if wanted macos;       then macos_dmg osx-arm64 macos-arm64; fi
  if wanted macos-intel; then macos_dmg osx-x64   macos-x86_64; fi
fi

# ---- only what the appendix allows, and the licence with it -----------------------------------
# Reads the archives back rather than trusting the staging folders: what ships is what is inside
# these files, and a file of SCIEX's that their appendix does not name is a licence breach that
# cannot be taken back once it is public.
echo "== checking the archives"
LISTED="$(sed -n '/APPENDIX A/,$p' "$ROOT/vendor/sciex/SCIEX_LICENSE.txt" | sed -n 's/^[[:space:]]*\([A-Za-z0-9_.]*\.dll\)[[:space:]]*$/\1/p')"
for f in "$OUT"/OpenDIAL-"$VERSION"-*.tar.gz "$OUT"/OpenDIAL-"$VERSION"-*.zip "$OUT"/OpenDIAL-"$VERSION"-*.dmg; do
  [ -e "$f" ] || continue
  case "$f" in
    *.tar.gz) LIST=$(tar -tzf "$f") ;;
    *.zip)    LIST=$(unzip -Z1 "$f") ;;
    *.dmg)    LIST=$(hdiutil imageinfo "$f" >/dev/null 2>&1 && { MP=$(mktemp -d); hdiutil attach -quiet -nobrowse -readonly -mountpoint "$MP" "$f"; find "$MP" -print; hdiutil detach -quiet "$MP"; rmdir "$MP" 2>/dev/null; }) ;;
  esac
  # every SCIEX file inside has to be one the appendix names
  BAD=""
  while read -r entry; do
    name="$(basename "$entry")"
    case "$name" in
      Clearcore2.*|Sciex.*|SciexToolKit.dll|Wiff*.dll) ;;
      *) continue ;;
    esac
    printf '%s\n' "$LISTED" | grep -qx "$name" || BAD="$BAD $name"
  done < <(printf '%s\n' "$LIST")
  if [ -n "$BAD" ]; then
    echo "REFUSING: $(basename "$f") carries SCIEX files Appendix A does not name:$BAD" >&2
    rm -f "$f"
    exit 1
  fi
  # and the licence has to be beside them
  if printf '%s' "$LIST" | grep -q -i "clearcore2" && ! printf '%s' "$LIST" | grep -q "SCIEX_LICENSE.txt"; then
    echo "REFUSING: $(basename "$f") carries the components without the licence they are granted by" >&2
    rm -f "$f"
    exit 1
  fi
  n=$(printf '%s\n' "$LIST" | grep -c -i "clearcore2" || true)
  echo "   $(basename "$f"): $n SCIEX components, all named in Appendix A, licence included"
done

# ---- checksums --------------------------------------------------------------------------------
( cd "$OUT" && shasum -a 256 OpenDIAL-"$VERSION"-* > SHA256SUMS && cat SHA256SUMS )
echo "[package-release] done: $OUT"
