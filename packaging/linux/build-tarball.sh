#!/bin/bash
# Package FryWorld for Linux (tarball + desktop entry + icon)
# Usage:
#   packaging/linux/build-tarball.sh --version 1.0.0 --publish-dir publish/linux-x64 --output-dir packaging/linux
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

VERSION="1.0.0"
PUBLISH_DIR="$REPO_ROOT/publish/linux-x64"
OUTPUT_DIR="$SCRIPT_DIR"

while [ $# -gt 0 ]; do
  case "$1" in
    --version)     VERSION="$2"; shift 2 ;;
    --publish-dir) PUBLISH_DIR="$2"; shift 2 ;;
    --output-dir)  OUTPUT_DIR="$2"; shift 2 ;;
    *)             echo "Unknown arg: $1"; exit 1 ;;
  esac
done

STAGE_DIR="$(mktemp -d /tmp/fryworld-linux-XXXXXX)"
cleanup() { rm -rf "$STAGE_DIR"; }
trap cleanup EXIT

echo "Packaging Linux tarball for FryWorld v$VERSION..."

# Assemble package directory structure
PKG_DIR="$STAGE_DIR/FryWorld-$VERSION-linux-x64"
mkdir -p "$PKG_DIR/bin" "$PKG_DIR/share/applications" "$PKG_DIR/share/icons/hicolor/256x256/apps"

cp -R "$PUBLISH_DIR/." "$PKG_DIR/bin/"
chmod +x "$PKG_DIR/bin/FryWorld"

cp "$SCRIPT_DIR/fryworld.desktop" "$PKG_DIR/share/applications/"
cp "$SCRIPT_DIR/fryworld-256x256.png" "$PKG_DIR/share/icons/hicolor/256x256/apps/fryworld.png"

# Add convenient install.sh script
cat << 'EOF' > "$PKG_DIR/install.sh"
#!/bin/bash
set -eu
INSTALL_PREFIX="${PREFIX:-$HOME/.local}"
echo "Installing FryWorld to $INSTALL_PREFIX..."

mkdir -p "$INSTALL_PREFIX/bin" "$INSTALL_PREFIX/share/applications" "$INSTALL_PREFIX/share/icons/hicolor/256x256/apps"
cp -R bin/* "$INSTALL_PREFIX/bin/"
cp share/applications/fryworld.desktop "$INSTALL_PREFIX/share/applications/"
cp share/icons/hicolor/256x256/apps/fryworld.png "$INSTALL_PREFIX/share/icons/hicolor/256x256/apps/"

# Update desktop database if available
update-desktop-database "$INSTALL_PREFIX/share/applications" 2>/dev/null || true
echo "FryWorld installed successfully! Run with: fryworld"
EOF
chmod +x "$PKG_DIR/install.sh"

# Create .tar.gz archive
mkdir -p "$OUTPUT_DIR"
ARCHIVE_PATH="$OUTPUT_DIR/FryWorld-$VERSION-linux-x64.tar.gz"
tar -czf "$ARCHIVE_PATH" -C "$STAGE_DIR" "FryWorld-$VERSION-linux-x64"

echo "✓ Linux tarball created: $ARCHIVE_PATH ($(du -h "$ARCHIVE_PATH" | cut -f1))"
