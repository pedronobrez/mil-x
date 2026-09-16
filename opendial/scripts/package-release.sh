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
# The builds are made with OPENDIAL_SHIP_SCIEX=false, which compiles the native .wiff reader and
# leaves SCIEX's assemblies out of the output. So the archives carry a reader that works the moment
# somebody who has accepted SCIEX's licence puts the SDK in plugins/sciex on their own machine, and
# they carry none of SCIEX's bytes. Without the SDK the reader says so and .wiff goes through
# msconvert, which is what a machine without it did before.
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

# The SCIEX Clearcore2 SDK is built into the local application whenever vendor/sciex is present,
# and its licence forbids passing it on. A release archive is exactly the act of passing it on, so
# the plugin folder is stripped here and the archives are searched for it again at the end.
drop_sciex () { # <folder>
  rm -rf "$1/plugins/sciex"
  rmdir "$1/plugins" 2>/dev/null || true
}

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
  drop_sciex "$stage"
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
  drop_sciex "$STAGE"
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
  # taking files out of a signed bundle invalidates the signature, so it is signed again after
  drop_sciex "$stage/OpenDIAL.app/Contents/MacOS"
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

# ---- nothing redistributable slipped in -------------------------------------------------------
# Reads the archives back rather than trusting the staging folders: what ships is what is inside
# these files, and a mistake here is a licence breach that cannot be taken back once it is public.
echo "== checking the archives"
for f in "$OUT"/OpenDIAL-"$VERSION"-*.tar.gz "$OUT"/OpenDIAL-"$VERSION"-*.zip "$OUT"/OpenDIAL-"$VERSION"-*.dmg; do
  [ -e "$f" ] || continue
  case "$f" in
    *.tar.gz) LIST=$(tar -tzf "$f") ;;
    *.zip)    LIST=$(unzip -Z1 "$f") ;;
    *.dmg)    LIST=$(hdiutil imageinfo "$f" >/dev/null 2>&1 && { MP=$(mktemp -d); hdiutil attach -quiet -nobrowse -readonly -mountpoint "$MP" "$f"; find "$MP" -print; hdiutil detach -quiet "$MP"; rmdir "$MP" 2>/dev/null; }) ;;
  esac
  if printf '%s' "$LIST" | grep -q -i -E "clearcore2|plugins/sciex"; then
    echo "REFUSING: $(basename "$f") carries the SCIEX SDK, which must not be redistributed" >&2
    rm -f "$f"
    exit 1
  fi
  echo "   $(basename "$f"): clean"
done

# ---- checksums --------------------------------------------------------------------------------
( cd "$OUT" && shasum -a 256 OpenDIAL-"$VERSION"-* > SHA256SUMS && cat SHA256SUMS )
echo "[package-release] done: $OUT"
