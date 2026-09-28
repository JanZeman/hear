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

  local xcode_project="$PWD/Builds/iOS/Unity-iPhone.xcodeproj"

  echo "==> Compiling Xcode project"
  xcodebuild -project "$xcode_project" \
    -scheme Unity-iPhone \
    -configuration Debug \
    -destination "id=$IOS_DEVICE_UDID" \
    -allowProvisioningUpdates \
    CODE_SIGN_STYLE=Automatic \
    DEVELOPMENT_TEAM=9VBQGD32YX \
    build

  # DerivedData is shared across every worktree on this Mac, keyed by a hash of the .xcodeproj's
  # own absolute path - a naive search through it (the previous approach here) could pick up a
  # DIFFERENT worktree's stale Hear.app instead of the one just built above, silently installing
  # the wrong worktree's build (human report 2026-09-28: "nerespektuje, ve kterem worktree to
  # bylo spusteno"). Asking Xcode for this exact project's own CONFIGURATION_BUILD_DIR instead
  # ties the install path deterministically to $xcode_project (itself derived from $ROOT_DIR,
  # i.e. this worktree), with no ambiguity and no DerivedData-internal path guessing.
  local build_dir
  build_dir="$(xcodebuild -project "$xcode_project" \
    -scheme Unity-iPhone \
    -configuration Debug \
    -destination "id=$IOS_DEVICE_UDID" \
    -showBuildSettings 2>/dev/null \
    | awk -F ' = ' '/CONFIGURATION_BUILD_DIR/ { print $2; exit }')"

  if [ -z "$build_dir" ]; then
    echo "Could not resolve CONFIGURATION_BUILD_DIR from xcodebuild -showBuildSettings." >&2
    exit 1
  fi

  local app_path="$build_dir/Hear.app"
  if [ ! -d "$app_path" ]; then
    echo "Expected built app not found at $app_path" >&2
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
