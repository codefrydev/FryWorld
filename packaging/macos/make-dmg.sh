#!/bin/bash
# Build the branded FryWorld disk image.
#
#   packaging/macos/make-dmg.sh --app FryWorld.app --output FryWorld-1.0.0-arm64.dmg
#
# Layout, background and volume icon all come from packaging/macos/dmg/. The
# heavy lifting is dmgbuild, which writes the Finder .DS_Store directly rather
# than driving Finder over AppleScript - Apple events on a headless GitHub
# runner fail intermittently (-1712 timed out, -1743 not authorized) and can
# hang outright.
#
# The script verifies the finished image and exits non-zero if anything is
# missing.
set -euo pipefail

DMGBUILD_VERSION="1.6.7"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SETTINGS="$SCRIPT_DIR/dmg/settings.py"

APP=""
OUTPUT=""
VOLNAME="FryWorld"

die() { echo "make-dmg: $*" >&2; exit 1; }

while [ $# -gt 0 ]; do
  case "$1" in
    --app)     APP="$2"; shift 2 ;;
    --output)  OUTPUT="$2"; shift 2 ;;
    --volname) VOLNAME="$2"; shift 2 ;;
    -h|--help) sed -n '2,13p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *)         die "unknown argument: $1" ;;
  esac
done

[ -n "$APP" ]    || die "--app is required"
[ -n "$OUTPUT" ] || die "--output is required"
[ -d "$APP" ]    || die "no such app bundle: $APP"
[ -f "$SETTINGS" ] || die "missing $SETTINGS"

APP="$(cd "$(dirname "$APP")" && pwd)/$(basename "$APP")"
APP_NAME="$(basename "$APP")"

for asset in "$SCRIPT_DIR/dmg/background.png" "$SCRIPT_DIR/dmg/background@2x.png" \
             "$SCRIPT_DIR/AppIcon.icns"; do
  [ -f "$asset" ] || die "missing $asset (run tools/generate_assets.py?)"
done

# A leftover /Volumes/FryWorld from a previously mounted release would make macOS
# mount ours as "FryWorld 1" and break the layout.
for mountpoint in "/Volumes/$VOLNAME" "/Volumes/$VOLNAME "*; do
  [ -d "$mountpoint" ] || continue
  echo "make-dmg: detaching stale mount $mountpoint"
  hdiutil detach "$mountpoint" -force >/dev/null 2>&1 || true
done

VENV="${FRYWORLD_DMG_VENV:-$REPO_ROOT/.venv-dmg}"
if [ ! -x "$VENV/bin/dmgbuild" ]; then
  echo "make-dmg: creating build venv at $VENV"
  python3 -m venv "$VENV"
  "$VENV/bin/pip" install --quiet --disable-pip-version-check "dmgbuild==$DMGBUILD_VERSION"
fi

rm -f "$OUTPUT"
"$VENV/bin/dmgbuild" -s "$SETTINGS" -D "app=$APP" -D "assets=$SCRIPT_DIR/dmg" "$VOLNAME" "$OUTPUT"
[ -f "$OUTPUT" ] || die "dmgbuild produced no output"

# ---------------------------------------------------------------- verification
MOUNT="$(mktemp -d /tmp/fryworld-dmg-verify.XXXXXX)"
cleanup() { hdiutil detach "$MOUNT" -force >/dev/null 2>&1 || true; rmdir "$MOUNT" 2>/dev/null || true; }
trap cleanup EXIT

hdiutil attach "$OUTPUT" -readonly -nobrowse -noautoopen -mountpoint "$MOUNT" >/dev/null

failures=0
check() {
  if eval "$2"; then
    printf '  ok    %s\n' "$1"
  else
    printf '  FAIL  %s\n' "$1"
    failures=$((failures + 1))
  fi
}

echo "make-dmg: verifying $OUTPUT"
check "$APP_NAME present"          "[ -d '$MOUNT/$APP_NAME' ]"
check "Applications symlink"       "[ -L '$MOUNT/Applications' ]"
check "background image"           "[ -s '$MOUNT/.background.tiff' ]"
check "custom volume icon"         "[ -s '$MOUNT/.VolumeIcon.icns' ]"
check "Finder layout (.DS_Store)"  "[ \"\$(stat -f%z '$MOUNT/.DS_Store' 2>/dev/null || echo 0)\" -ge 4096 ]"
check "code signature intact"      "codesign --verify --strict '$MOUNT/$APP_NAME' >/dev/null 2>&1"

if [ "$failures" -ne 0 ]; then
  die "$failures verification check(s) failed"
fi

echo "make-dmg: $OUTPUT ($(du -h "$OUTPUT" | cut -f1))"
