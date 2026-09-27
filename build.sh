#!/usr/bin/env bash
# Build and deploy the Hear app.
#
# Usage: ./build.sh <ios|mac|android>
#
# ios     - builds via Unity, compiles the Xcode project, installs and launches
#           on the configured physical iPhone (xcrun devicectl).
# mac     - builds a macOS standalone player via Unity and launches it.
# android - builds an APK via Unity, installs and launches it via adb.

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$ROOT_DIR/Hear"
BUNDLE_ID="com.janzeman.hear"

# Jan's "Jan GN iPhone Dev" device. Override with IOS_DEVICE_UDID if it changes.
IOS_DEVICE_UDID="${IOS_DEVICE_UDID:-00008101-001251CA1E31003A}"

usage() {
  echo "Usage: $0 <ios|mac|android>" >&2
  exit 1
}

[ $# -eq 1 ] || usage
TARGET_RAW="$1"

case "$TARGET_RAW" in
  ios) TARGET="ios" ;;
  mac|macos|osx) TARGET="mac" ;;
  android|oid|droid) TARGET="android" ;;
  *) usage ;;
esac

cd "$PROJECT_DIR"

build_ios() {
  echo "==> Building iOS player via Unity"
  unity build --target iOS \
    --execute-method Hear.Editor.HearDevelopmentBuild.BuildIos \
    --output-path Builds/iOS

  echo "==> Compiling Xcode project"
  xcodebuild -project Builds/iOS/Unity-iPhone.xcodeproj \
    -scheme Unity-iPhone \
    -configuration Debug \
    -destination "id=$IOS_DEVICE_UDID" \
    -allowProvisioningUpdates \
    CODE_SIGN_STYLE=Automatic \
    DEVELOPMENT_TEAM=9VBQGD32YX \
    build

  local app_path
  app_path="$(find "$HOME/Library/Developer/Xcode/DerivedData" \
    -maxdepth 4 -path "*Unity-iPhone-*/Build/Products/Debug-iphoneos/Hear.app" \
    -print -quit)"

  if [ -z "$app_path" ]; then
    echo "Could not locate the built Hear.app under DerivedData." >&2
    exit 1
  fi

  echo "==> Installing on device ($IOS_DEVICE_UDID)"
  xcrun devicectl device install app --device "$IOS_DEVICE_UDID" "$app_path"

  echo "==> Launching on device"
  xcrun devicectl device process launch --device "$IOS_DEVICE_UDID" "$BUNDLE_ID"
}

build_mac() {
  echo "==> Building macOS player via Unity"
  unity build --target StandaloneOSX \
    --execute-method Hear.Editor.HearDevelopmentBuild.BuildMacOs \
    --output-path Builds/macOS/Hear.app

  echo "==> Launching"
  open Builds/macOS/Hear.app
}

build_android() {
  echo "==> Building Android APK via Unity"
  unity build --target Android \
    --execute-method Hear.Editor.HearDevelopmentBuild.BuildAndroid \
    --output-path Builds/Android/Hear.apk

  echo "==> Installing on device via adb"
  adb install -r Builds/Android/Hear.apk

  echo "==> Launching on device"
  adb shell monkey -p "$BUNDLE_ID" -c android.intent.category.LAUNCHER 1 >/dev/null
}

case "$TARGET" in
  ios) build_ios ;;
  mac) build_mac ;;
  android) build_android ;;
esac

echo "==> Done ($TARGET)"
