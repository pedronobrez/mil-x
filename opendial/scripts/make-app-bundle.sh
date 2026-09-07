#!/usr/bin/env bash
# Packages the published desktop app as a double-clickable macOS application bundle.
#
#   scripts/make-app-bundle.sh [osx-arm64|osx-x64] [--skip-publish]
#       -> dist/OpenDIAL.app
#
# The bundle is self-contained (its own .NET runtime, the SCIEX plugin when vendor/sciex is
# present) and ad-hoc signed, which is what macOS requires to launch an arm64 binary. It is not
# notarised, so the first launch still needs Finder's right-click > Open.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"

RID=""
SKIP_PUBLISH=0
for arg in "$@"; do
  case "$arg" in
    --skip-publish) SKIP_PUBLISH=1 ;;
    *) RID="$arg" ;;
  esac
done
if [ -z "$RID" ]; then
  case "$(uname -m)" in
    arm64) RID=osx-arm64 ;; x86_64) RID=osx-x64 ;;
    *) echo "unsupported architecture $(uname -m)"; exit 1 ;;
  esac
fi
[ "$(uname -s)" = "Darwin" ] || { echo "application bundles can only be built on macOS"; exit 1; }

VERSION="${OPENDIAL_VERSION:-0.1.0}"
PUBLISH="$ROOT/dist/opendial-desktop-$RID"
APP="$ROOT/dist/OpenDIAL.app"

# 1. the published, self-contained application ------------------------------------------------
if [ "$SKIP_PUBLISH" = "0" ]; then
  "$HERE/build-gui.sh" "$RID"
fi
[ -x "$PUBLISH/OpenDIAL" ] || { echo "no published app at $PUBLISH (drop --skip-publish)"; exit 1; }

# 2. the icon ----------------------------------------------------------------------------------
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
ICONSET="$WORK/OpenDIAL.iconset"
mkdir -p "$ICONSET"
python3 "$ROOT/tools/make_icon.py" --out "$WORK/icon-1024.png" --size 1024 >/dev/null
for spec in "16 16x16" "32 16x16@2x" "32 32x32" "64 32x32@2x" "128 128x128" "256 128x128@2x" "256 256x256" "512 256x256@2x" "512 512x512" "1024 512x512@2x"; do
  set -- $spec
  sips -z "$1" "$1" "$WORK/icon-1024.png" --out "$ICONSET/icon_$2.png" >/dev/null 2>&1
done
iconutil --convert icns "$ICONSET" --output "$WORK/OpenDIAL.icns"

# 3. the bundle --------------------------------------------------------------------------------
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
# a self-contained .NET application resolves its runtime next to the executable, so the whole
# publish folder (including plugins/) lives in MacOS/
ditto "$PUBLISH" "$APP/Contents/MacOS"
cp "$WORK/OpenDIAL.icns" "$APP/Contents/Resources/OpenDIAL.icns"

# Windows-only compat assemblies shipped by the SCIEX plugin: dead weight on macOS, and codesign
# refuses to walk runtimes/win/lib/net8.0 ("bundle format unrecognized").
rm -rf "$APP"/Contents/MacOS/plugins/*/runtimes/win
printf 'APPL????' > "$APP/Contents/PkgInfo"

document_type() { # <name> <extensions…> ; role and icon are the same for all of them
  local name="$1"; shift
  printf '    <dict>\n'
  printf '      <key>CFBundleTypeName</key><string>%s</string>\n' "$name"
  printf '      <key>CFBundleTypeRole</key><string>%s</string>\n' "$ROLE"
  printf '      <key>CFBundleTypeIconFile</key><string>OpenDIAL</string>\n'
  printf '      <key>LSHandlerRank</key><string>%s</string>\n' "$RANK"
  printf '      <key>CFBundleTypeExtensions</key>\n      <array>\n'
  for e in "$@"; do printf '        <string>%s</string>\n' "$e"; done
  printf '      </array>\n    </dict>\n'
}

{
  cat <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>OpenDIAL</string>
  <key>CFBundleDisplayName</key><string>OpenDIAL</string>
  <key>CFBundleExecutable</key><string>OpenDIAL</string>
  <key>CFBundleIdentifier</key><string>org.opendial.desktop</string>
  <key>CFBundleIconFile</key><string>OpenDIAL</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleVersion</key><string>$VERSION</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>LSApplicationCategoryType</key><string>public.app-category.productivity</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSRequiresAquaSystemAppearance</key><false/>
  <key>NSHumanReadableCopyright</key><string>OpenDIAL — an open, cross-platform port of MS-DIAL. MS-DIAL is © RIKEN and UC Davis.</string>
  <key>CFBundleDocumentTypes</key>
  <array>
PLIST
  ROLE=Editor RANK=Owner   document_type "OpenDIAL project" odproj
  ROLE=Viewer RANK=Alternate document_type "MS-DIAL project" mdproject
  ROLE=Viewer RANK=Alternate document_type "OpenQuant batch" oqproj opvproj
  ROLE=Viewer RANK=Alternate document_type "Mass spectrometry raw data" mzML mzml wiff wiff2 raw abf ibf cdf lcd qgd
  cat <<'PLIST'
  </array>
</dict>
</plist>
PLIST
} > "$APP/Contents/Info.plist"

plutil -lint "$APP/Contents/Info.plist" >/dev/null

# 4. signature ---------------------------------------------------------------------------------
# arm64 refuses to run unsigned code; ad-hoc is enough for a locally built application.
xattr -cr "$APP"
codesign --force --deep --sign - --timestamp=none "$APP" 2>/dev/null
codesign --verify --deep "$APP" && echo "[bundle] signature ok (ad-hoc)"

# Let Finder pick up the new icon and document bindings straight away — but only while there is no
# installed copy. Two registered bundles both claim .odproj and Finder then picks either one.
LSREGISTER=/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister
if [ -d /Applications/OpenDIAL.app ]; then
  "$LSREGISTER" -u "$APP" 2>/dev/null || true
  echo "[bundle] /Applications/OpenDIAL.app owns the document types; copy this build over it to update"
else
  "$LSREGISTER" -f "$APP" 2>/dev/null || true
fi
touch "$APP"

echo "[bundle] done: $APP"
echo "         install with:  cp -R \"$APP\" /Applications/"
