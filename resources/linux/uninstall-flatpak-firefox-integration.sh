#!/usr/bin/env bash

set -euo pipefail

HOST_NAME="com.nickvision.parabolic"
HOST_LAUNCHER="$HOME/.local/lib/parabolic-flatpak/native-host"
MANIFEST_PATH="$HOME/.mozilla/native-messaging-hosts/$HOST_NAME.json"

rm -f "$HOST_LAUNCHER" "$MANIFEST_PATH"
rmdir "$HOME/.local/lib/parabolic-flatpak" 2>/dev/null || true

echo "Parabolic Flatpak integration removed from Firefox."
