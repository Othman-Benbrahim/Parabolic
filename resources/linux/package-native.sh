#!/usr/bin/env bash

set -euo pipefail

if [[ $# -ne 2 ]]; then
    echo "Usage: $0 <linux-x64|linux-arm64> <x64|arm64>" >&2
    exit 2
fi

RUNTIME="$1"
ARCH="$2"
case "$RUNTIME:$ARCH" in
    linux-x64:x64|linux-arm64:arm64) ;;
    *) echo "Unsupported runtime/architecture pair: $RUNTIME/$ARCH" >&2; exit 2 ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
VERSION="2026.8.6"
PACKAGE_NAME="Parabolic-${VERSION}-linux-${ARCH}"
BUILD_ROOT="$REPO_ROOT/dist/linux-$ARCH"
PACKAGE_ROOT="$BUILD_ROOT/$PACKAGE_NAME"
APP_DIR="$PACKAGE_ROOT/app"
BRIDGE_DIR="$PACKAGE_ROOT/bridge"

rm -rf "$BUILD_ROOT"
mkdir -p "$APP_DIR" "$BRIDGE_DIR" "$PACKAGE_ROOT/share/icons"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet publish "$REPO_ROOT/Nickvision.Parabolic.GNOME/Nickvision.Parabolic.GNOME.csproj" \
    -c Release --runtime "$RUNTIME" --self-contained true \
    -p:PublishReadyToRun=true -o "$APP_DIR"
dotnet publish "$REPO_ROOT/Nickvision.Parabolic.NativeHost/Nickvision.Parabolic.NativeHost.csproj" \
    -c Release --runtime "$RUNTIME" --self-contained true -o "$BRIDGE_DIR"
dotnet publish "$REPO_ROOT/Nickvision.Parabolic.DownloadService/Nickvision.Parabolic.DownloadService.csproj" \
    -c Release --runtime "$RUNTIME" --self-contained true -o "$BRIDGE_DIR"

for tool in yt-dlp N_m3u8DL-RE deno; do
    if [[ ! -x "$SCRIPT_DIR/tools/$tool" ]]; then
        echo "Missing bundled tool: $SCRIPT_DIR/tools/$tool" >&2
        exit 1
    fi
    install -m 0755 "$SCRIPT_DIR/tools/$tool" "$APP_DIR/$tool"
    install -m 0755 "$SCRIPT_DIR/tools/$tool" "$BRIDGE_DIR/$tool"
done

install -m 0644 "$REPO_ROOT/resources/licenses/N_m3u8DL-RE-LICENSE.txt" \
    "$APP_DIR/N_m3u8DL-RE-LICENSE.txt"
install -m 0644 "$REPO_ROOT/resources/licenses/N_m3u8DL-RE-LICENSE.txt" \
    "$BRIDGE_DIR/N_m3u8DL-RE-LICENSE.txt"
install -m 0644 "$SCRIPT_DIR/org.nickvision.tubeconverter.desktop.in" \
    "$PACKAGE_ROOT/share/org.nickvision.tubeconverter.desktop.in"
install -m 0644 "$SCRIPT_DIR/org.nickvision.tubeconverter.service.in" \
    "$PACKAGE_ROOT/share/org.nickvision.tubeconverter.service.in"
install -m 0644 "$SCRIPT_DIR/org.nickvision.tubeconverter.metainfo.xml" \
    "$PACKAGE_ROOT/share/org.nickvision.tubeconverter.metainfo.xml"
install -m 0644 "$REPO_ROOT/resources/org.nickvision.tubeconverter.svg" \
    "$PACKAGE_ROOT/share/icons/org.nickvision.tubeconverter.svg"
install -m 0644 "$REPO_ROOT/resources/org.nickvision.tubeconverter-symbolic.svg" \
    "$PACKAGE_ROOT/share/icons/org.nickvision.tubeconverter-symbolic.svg"
install -m 0755 "$SCRIPT_DIR/install-native.sh" "$PACKAGE_ROOT/install.sh"
install -m 0755 "$SCRIPT_DIR/uninstall-native.sh" "$PACKAGE_ROOT/uninstall.sh"

tar -C "$BUILD_ROOT" -czf "$REPO_ROOT/dist/$PACKAGE_NAME.tar.gz" "$PACKAGE_NAME"
echo "$REPO_ROOT/dist/$PACKAGE_NAME.tar.gz"
