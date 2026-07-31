#!/bin/bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
APP_PATH="$PROJECT_DIR/dist/Codex Quota.app"
ZIP_PATH="$PROJECT_DIR/dist/Codex Quota.zip"
CONTENTS_PATH="$APP_PATH/Contents"

swift build --package-path "$PROJECT_DIR" -c release
BIN_PATH="$(swift build --package-path "$PROJECT_DIR" -c release --show-bin-path)"

rm -rf "$APP_PATH"
mkdir -p "$CONTENTS_PATH/MacOS" "$CONTENTS_PATH/Resources"

cp "$BIN_PATH/CodexQuota" "$CONTENTS_PATH/MacOS/CodexQuota"
cp "$PROJECT_DIR/AppResources/Info.plist" "$CONTENTS_PATH/Info.plist"
chmod +x "$CONTENTS_PATH/MacOS/CodexQuota"

xattr -cr "$APP_PATH"
codesign --force --deep --sign - "$APP_PATH"
rm -f "$ZIP_PATH"
ditto -c -k --sequesterRsrc --keepParent "$APP_PATH" "$ZIP_PATH"
xattr -cr "$APP_PATH"

echo "Built: $APP_PATH"
echo "Packed: $ZIP_PATH"
