#!/bin/bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_ROOT="$PROJECT_DIR/.build/universal-release"
ZIP_PATH="$PROJECT_DIR/dist/Codex Quota Universal.zip"
TEMP_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/codex-quota-universal.XXXXXX")"
APP_PATH="$TEMP_ROOT/Codex Quota.app"
CONTENTS_PATH="$APP_PATH/Contents"

cleanup() {
  rm -rf "$TEMP_ROOT"
}
trap cleanup EXIT

build_architecture() {
  local architecture="$1"
  local triple="${architecture}-apple-macosx14.0"
  local scratch_path="$BUILD_ROOT/$architecture"

  swift build \
    --package-path "$PROJECT_DIR" \
    --configuration release \
    --triple "$triple" \
    --scratch-path "$scratch_path"
}

rm -rf "$BUILD_ROOT"
build_architecture arm64
build_architecture x86_64

ARM64_BINARY="$BUILD_ROOT/arm64/arm64-apple-macosx/release/CodexQuota"
X86_64_BINARY="$BUILD_ROOT/x86_64/x86_64-apple-macosx/release/CodexQuota"

mkdir -p "$CONTENTS_PATH/MacOS" "$CONTENTS_PATH/Resources" "$PROJECT_DIR/dist"
lipo -create "$ARM64_BINARY" "$X86_64_BINARY" -output "$CONTENTS_PATH/MacOS/CodexQuota"
cp "$PROJECT_DIR/AppResources/Info.plist" "$CONTENTS_PATH/Info.plist"
chmod +x "$CONTENTS_PATH/MacOS/CodexQuota"

lipo "$CONTENTS_PATH/MacOS/CodexQuota" -verify_arch arm64 x86_64
xattr -cr "$APP_PATH"
codesign --force --deep --sign - "$APP_PATH"
codesign --verify --deep --strict "$APP_PATH"

rm -f "$ZIP_PATH"
find "$APP_PATH" -exec touch -h -t 200101010000 {} +
(
  cd "$TEMP_ROOT"
  COPYFILE_DISABLE=1 LC_ALL=C /usr/bin/zip -q -r -X "$ZIP_PATH" "Codex Quota.app"
)

echo "Packed universal app: $ZIP_PATH"
