#!/usr/bin/env bash

set -euo pipefail

APP_PATH="/Applications/Parabolic.app"
HOST_DIR="$HOME/Library/Application Support/Mozilla/NativeMessagingHosts"
LAUNCH_AGENT_DIR="$HOME/Library/LaunchAgents"
HOST_PATH="$APP_PATH/Contents/Library/ParabolicBridge/Nickvision.Parabolic.NativeHost"
SERVICE_PATH="$APP_PATH/Contents/Library/ParabolicBridge/Nickvision.Parabolic.DownloadService"
BRIDGE_DIR="$APP_PATH/Contents/Library/ParabolicBridge"
TOOLS_DIR="$APP_PATH/Contents/MacOS"
PLIST_PATH="$LAUNCH_AGENT_DIR/com.nickvision.parabolic.download-service.plist"

if [[ ! -x "$HOST_PATH" || ! -x "$SERVICE_PATH" ]]; then
    echo "Déplacez d'abord Parabolic.app dans /Applications, puis relancez ce fichier." >&2
    read -r -p "Appuyez sur Entrée pour fermer…" _
    exit 1
fi

mkdir -p "$HOST_DIR" "$LAUNCH_AGENT_DIR"
printf '%s\n' \
    '{' \
    '  "name": "com.nickvision.parabolic",' \
    '  "description": "Parabolic Firefox Native Messaging bridge",' \
    "  \"path\": \"$HOST_PATH\"," \
    '  "type": "stdio",' \
    '  "allowed_extensions": ["parabolic-media-detector@othmanbenbrahim.dev"]' \
    '}' > "$HOST_DIR/com.nickvision.parabolic.json"

{
    printf '%s\n' '<?xml version="1.0" encoding="UTF-8"?>' '<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">' '<plist version="1.0">' '<dict>' '  <key>Label</key>' '  <string>com.nickvision.parabolic.download-service</string>' '  <key>ProgramArguments</key>' '  <array>'
    printf '    <string>%s</string>\n' "$SERVICE_PATH"
    printf '%s\n' '    <string>--background</string>' '  </array>' '  <key>WorkingDirectory</key>'
    printf '  <string>%s</string>\n' "$BRIDGE_DIR"
    printf '%s\n' '  <key>EnvironmentVariables</key>' '  <dict>' '    <key>PATH</key>'
    printf '    <string>%s:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin</string>\n' "$TOOLS_DIR"
    printf '%s\n' '  </dict>' '  <key>RunAtLoad</key>' '  <true/>' '  <key>KeepAlive</key>' '  <false/>' '</dict>' '</plist>'
} > "$PLIST_PATH"

launchctl bootout "gui/$UID" "$PLIST_PATH" >/dev/null 2>&1 || true
launchctl bootstrap "gui/$UID" "$PLIST_PATH"
echo "Intégration Firefox de Parabolic installée. Redémarrez Firefox s'il était ouvert."
read -r -p "Appuyez sur Entrée pour fermer…" _
