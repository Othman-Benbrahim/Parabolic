#!/usr/bin/env bash

set -euo pipefail

APP_ID="org.nickvision.tubeconverter"
HOST_NAME="com.nickvision.parabolic"
EXTENSION_ID="parabolic-media-detector@othmanbenbrahim.dev"
INSTALL_ROOT="$HOME/.local/lib/parabolic-flatpak"
HOST_LAUNCHER="$INSTALL_ROOT/native-host"
FIREFOX_HOST_DIR="$HOME/.mozilla/native-messaging-hosts"
MANIFEST_PATH="$FIREFOX_HOST_DIR/$HOST_NAME.json"

command -v flatpak >/dev/null 2>&1 || {
    echo "Flatpak is not installed." >&2
    exit 1
}
flatpak info "$APP_ID" >/dev/null 2>&1 || {
    echo "Install the Parabolic Flatpak before its Firefox integration." >&2
    exit 1
}

mkdir -p "$INSTALL_ROOT" "$FIREFOX_HOST_DIR"
cat > "$HOST_LAUNCHER" <<'EOF'
#!/bin/sh
exec flatpak run --command=org.nickvision.tubeconverter.NativeHost org.nickvision.tubeconverter "$@"
EOF
chmod 0755 "$HOST_LAUNCHER"

cat > "$MANIFEST_PATH" <<EOF
{
  "name": "$HOST_NAME",
  "description": "Parabolic Flatpak Firefox Native Messaging bridge",
  "path": "$HOST_LAUNCHER",
  "type": "stdio",
  "allowed_extensions": ["$EXTENSION_ID"]
}
EOF
chmod 0644 "$MANIFEST_PATH"

echo "Parabolic Flatpak integration installed for Firefox."
echo "Restart Firefox if it was already open."
