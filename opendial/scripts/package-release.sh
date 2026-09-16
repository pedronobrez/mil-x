#!/usr/bin/env bash
# Builds the downloadable archives for a release, one per platform.
#
#   scripts/package-release.sh [--only linux|windows|macos] [--skip-build]
#       -> dist/release/OpenDIAL-<version>-linux-x86_64.tar.gz
#          dist/release/OpenDIAL-<version>-windows-x64.zip
#          dist/release/OpenDIAL-<version>-macos-arm64.dmg
#          dist/release/SHA256SUMS
#
# Every archive is self-contained: the .NET runtime travels inside it, so nothing has to be
# installed first. What does not travel is the SCIEX Clearcore2 SDK — its licence forbids
# redistribution — so .wiff reading is set up on the user's machine by scripts/fetch-sciex-assemblies.sh.
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

# ---- Linux ------------------------------------------------------------------------------------
if wanted linux; then
  echo "== linux-x64"
  [ "$SKIP_BUILD" = "1" ] || "$HERE/build-gui.sh" linux-x64 >/dev/null
  SRC="$ROOT/dist/opendial-desktop-linux-x64"
  STAGE="$(mktemp -d)/OpenDIAL-$VERSION-linux-x86_64"
  mkdir -p "$STAGE"
  cp -R "$SRC"/. "$STAGE/"
  chmod +x "$STAGE/OpenDIAL"
  drop_sciex "$STAGE"
  carry_paperwork "$STAGE"

  cat > "$STAGE/README.txt" <<EOF
OpenDIAL $VERSION — Linux x86_64

    ./OpenDIAL

That is the whole installation: the .NET runtime is inside this folder, so nothing else has to be
installed. If the file lost its permission bit in transit, put it back with  chmod +x OpenDIAL .

What the distribution has to provide is the X11 client libraries and fontconfig, which any desktop
Linux already has. On a bare container or a server image, install them first:

    Debian/Ubuntu   apt-get install -y libx11-6 libice6 libsm6 libfontconfig1 libicu-dev
    Fedora/RHEL     dnf install -y libX11 libICE libSM fontconfig libicu

Raw data
    mzML and mzXML are read directly. Vendor formats go through msconvert (ProteoWizard), which
    OpenDIAL calls when it finds it on PATH — on Linux that usually means the ProteoWizard docker
    image or a wine install.
    Native .wiff reading needs the SCIEX Clearcore2 SDK, which cannot be redistributed. Accept
    SCIEX's licence and fetch it yourself with scripts/fetch-sciex-assemblies.sh from the source
    tree; the files land in plugins/sciex next to this README.

Manual
    Inside the application, menu Help, in English and Portuguese.

Licence
    GPL-3.0 — see LICENSE.txt. What was changed in the MS-DIAL upstream is listed in NOTICE.txt.
EOF

  ( cd "$(dirname "$STAGE")" && tar -czf "$OUT/OpenDIAL-$VERSION-linux-x86_64.tar.gz" "$(basename "$STAGE")" )
  rm -rf "$(dirname "$STAGE")"
  echo "   $OUT/OpenDIAL-$VERSION-linux-x86_64.tar.gz"
fi

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

  cat > "$STAGE/README.txt" <<EOF
OpenDIAL $VERSION — Windows x64

    OpenDIAL.exe

That is the whole installation: the .NET runtime is inside this folder, so nothing else has to be
installed. There is no installer and nothing is written to the registry; move the folder wherever
you keep your tools and make a shortcut to OpenDIAL.exe.

SmartScreen
    The build is not signed with a Windows code-signing certificate, so the first launch shows
    "Windows protected your PC". More info > Run anyway. Unblock the zip before extracting
    (right-click the .zip > Properties > Unblock) and Windows will stop marking every file inside.

Raw data
    mzML and mzXML are read directly. Vendor formats go through msconvert (ProteoWizard) when it is
    on PATH — on Windows that is the normal ProteoWizard install.
    Native .wiff reading needs the SCIEX Clearcore2 SDK, which cannot be redistributed. Accept
    SCIEX's licence and fetch it yourself with scripts/fetch-sciex-assemblies.sh from the source
    tree; the files belong in plugins\\sciex next to this README.

Note
    MS-DIAL 5 itself runs on Windows, and on Windows it does more than this port does — ion
    mobility and imaging among it. This build exists so a Windows machine can open and continue a
    review started on a Mac or on Linux, and so a mixed lab shares one set of files.

Manual
    Inside the application, menu Help, in English and Portuguese.

Licence
    GPL-3.0 — see LICENSE.txt. What was changed in the MS-DIAL upstream is listed in NOTICE.txt.
EOF

  ( cd "$(dirname "$STAGE")" && zip -q -r "$OUT/OpenDIAL-$VERSION-windows-x64.zip" "$(basename "$STAGE")" )
  rm -rf "$(dirname "$STAGE")"
  echo "   $OUT/OpenDIAL-$VERSION-windows-x64.zip"
fi

# ---- macOS ------------------------------------------------------------------------------------
if wanted macos; then
  if [ "$(uname -s)" != "Darwin" ]; then
    echo "== macos: skipped (bundles can only be built on macOS)"
  else
    echo "== osx-arm64"
    if [ "$SKIP_BUILD" = "1" ]; then
      "$HERE/make-app-bundle.sh" osx-arm64 --skip-publish >/dev/null
    else
      "$HERE/make-app-bundle.sh" osx-arm64 >/dev/null
    fi
    STAGE="$(mktemp -d)/OpenDIAL $VERSION"
    mkdir -p "$STAGE"
    ditto "$ROOT/dist/OpenDIAL.app" "$STAGE/OpenDIAL.app"
    # taking files out of a signed bundle invalidates the signature, so it is signed again after
    drop_sciex "$STAGE/OpenDIAL.app/Contents/MacOS"
    codesign --force --deep --sign - --timestamp=none "$STAGE/OpenDIAL.app" 2>/dev/null
    codesign --verify --deep "$STAGE/OpenDIAL.app" || { echo "the bundle lost its signature" >&2; exit 1; }
    ln -s /Applications "$STAGE/Applications"
    carry_paperwork "$STAGE"
    cat > "$STAGE/README.txt" <<EOF
OpenDIAL $VERSION — macOS (Apple silicon)

Drag OpenDIAL.app onto the Applications folder beside it.

First launch
    The build is ad-hoc signed, not notarised, so double-clicking gives "unidentified developer".
    Right-click the app > Open, once. macOS remembers the answer.
    It will also ask for access to your Documents folder — say Allow, or the app cannot read your
    data where it lives.

Raw data
    .wiff is read natively once the SCIEX Clearcore2 SDK is in place; its licence forbids
    redistribution, so fetch it yourself with scripts/fetch-sciex-assemblies.sh from the source
    tree. mzML is read directly, and other vendor formats go through msconvert when it is on PATH.

Manual
    Inside the application, menu Help, in English and Portuguese.

Licence
    GPL-3.0 — see LICENSE.txt. What was changed in the MS-DIAL upstream is listed in NOTICE.txt.
EOF
    DMG="$OUT/OpenDIAL-$VERSION-macos-arm64.dmg"
    rm -f "$DMG"
    hdiutil create -quiet -volname "OpenDIAL $VERSION" -srcfolder "$STAGE" -ov -format UDZO "$DMG"
    rm -rf "$(dirname "$STAGE")"
    echo "   $DMG"
  fi
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
