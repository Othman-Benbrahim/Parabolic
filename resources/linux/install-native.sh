#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
XDG_DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
XDG_CONFIG_HOME="${XDG_CONFIG_HOME:-$HOME/.config}"
INSTALL_ROOT="${PARABOLIC_INSTALL_ROOT:-$HOME/.local/lib/parabolic}"
BIN_DIR="${PARABOLIC_BIN_DIR:-$HOME/.local/bin}"
FIREFOX_HOST_DIR="$HOME/.mozilla/native-messaging-hosts"
SYSTEMD_DIR="$XDG_CONFIG_HOME/systemd/user"
APP_ID="org.nickvision.tubeconverter"
HOST_NAME="com.nickvision.parabolic"

if [[ -z "$INSTALL_ROOT" || "$INSTALL_ROOT" == "/" || "$INSTALL_ROOT" == "$HOME" ]]; then
    echo "Refusing unsafe installation root: $INSTALL_ROOT" >&2
    exit 2
fi

for required in "$SCRIPT_DIR/app/Nickvision.Parabolic.GNOME" \
    "$SCRIPT_DIR/bridge/Nickvision.Parabolic.NativeHost" \
    "$SCRIPT_DIR/bridge/Nickvision.Parabolic.DownloadService"; do
    [[ -f "$required" ]] || { echo "Package incomplet: $required est absent." >&2; exit 1; }
done

mkdir -p "$INSTALL_ROOT" "$BIN_DIR" "$FIREFOX_HOST_DIR" "$SYSTEMD_DIR" \
    "$XDG_DATA_HOME/applications" "$XDG_DATA_HOME/metainfo" \
    "$XDG_DATA_HOME/dbus-1/services" \
    "$XDG_DATA_HOME/icons/hicolor/scalable/apps" \
    "$XDG_DATA_HOME/icons/hicolor/symbolic/apps"
rm -rf "$INSTALL_ROOT/app" "$INSTALL_ROOT/bridge"
cp -a "$SCRIPT_DIR/app" "$INSTALL_ROOT/"
cp -a "$SCRIPT_DIR/bridge" "$INSTALL_ROOT/"
chmod +x "$INSTALL_ROOT/app/Nickvision.Parabolic.GNOME" \
    "$INSTALL_ROOT/bridge/Nickvision.Parabolic.NativeHost" \
    "$INSTALL_ROOT/bridge/Nickvision.Parabolic.DownloadService"

LAUNCHER="$BIN_DIR/parabolic"
{
    printf '%s\n' '#!/usr/bin/env bash'
    printf 'exec %q "$@"\n' "$INSTALL_ROOT/app/Nickvision.Parabolic.GNOME"
} > "$LAUNCHER"
chmod 0755 "$LAUNCHER"

DESKTOP_FILE="$XDG_DATA_HOME/applications/$APP_ID.desktop"
sed \
    -e "s|@APP_ID@|$APP_ID|g" \
    -e "s|@LIB_DIR@|$INSTALL_ROOT/app|g" \
    -e "s|@OUTPUT_NAME@|Nickvision.Parabolic.GNOME|g" \
    "$SCRIPT_DIR/share/org.nickvision.tubeconverter.desktop.in" > "$DESKTOP_FILE"
DBUS_SERVICE_FILE="$XDG_DATA_HOME/dbus-1/services/$APP_ID.service"
sed \
    -e "s|@APP_ID@|$APP_ID|g" \
    -e "s|@LIB_DIR@|$INSTALL_ROOT/app|g" \
    -e "s|@OUTPUT_NAME@|Nickvision.Parabolic.GNOME|g" \
    "$SCRIPT_DIR/share/org.nickvision.tubeconverter.service.in" > "$DBUS_SERVICE_FILE"
install -m 0644 "$SCRIPT_DIR/share/org.nickvision.tubeconverter.metainfo.xml" \
    "$XDG_DATA_HOME/metainfo/$APP_ID.metainfo.xml"
install -m 0644 "$SCRIPT_DIR/share/icons/$APP_ID.svg" \
    "$XDG_DATA_HOME/icons/hicolor/scalable/apps/$APP_ID.svg"
install -m 0644 "$SCRIPT_DIR/share/icons/$APP_ID-symbolic.svg" \
    "$XDG_DATA_HOME/icons/hicolor/symbolic/apps/$APP_ID-symbolic.svg"

HOST_PATH="$INSTALL_ROOT/bridge/Nickvision.Parabolic.NativeHost"
printf '%s\n' \
    '{' \
    '  "name": "com.nickvision.parabolic",' \
    '  "description": "Parabolic Firefox Native Messaging bridge",' \
    "  \"path\": \"$HOST_PATH\"," \
    '  "type": "stdio",' \
    '  "allowed_extensions": ["parabolic-media-detector@othmanbenbrahim.dev"]' \
    '}' > "$FIREFOX_HOST_DIR/$HOST_NAME.json"
chmod 0644 "$FIREFOX_HOST_DIR/$HOST_NAME.json"

SERVICE_PATH="$INSTALL_ROOT/bridge/Nickvision.Parabolic.DownloadService"
{
    printf '%s\n' '[Unit]' 'Description=Parabolic persistent download service' 'After=graphical-session.target' '' '[Service]' 'Type=simple'
    printf 'ExecStart=%s --background\n' "$SERVICE_PATH"
    printf 'WorkingDirectory=%s\n' "$INSTALL_ROOT/bridge"
    printf 'Environment=PATH=%s:%s:/usr/local/bin:/usr/bin:/bin\n' "$INSTALL_ROOT/bridge" "$INSTALL_ROOT/app"
    printf '%s\n' 'Restart=on-failure' 'RestartSec=2' '' '[Install]' 'WantedBy=default.target'
} > "$SYSTEMD_DIR/parabolic-download-service.service"

if command -v systemctl >/dev/null 2>&1 && systemctl --user show-environment >/dev/null 2>&1; then
    systemctl --user daemon-reload
    systemctl --user enable parabolic-download-service.service
    systemctl --user restart parabolic-download-service.service
else
    echo "systemd utilisateur indisponible : Firefox démarrera le service à la demande."
fi
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$XDG_DATA_HOME/applications" || true
command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -f "$XDG_DATA_HOME/icons/hicolor" || true

for dependency in ffmpeg aria2c; do
    if ! command -v "$dependency" >/dev/null 2>&1; then
        echo "Attention : $dependency n'est pas installé. Certaines opérations de téléchargement ou de fusion échoueront."
    fi
done

echo "Parabolic 2026.8.6 installé pour $USER. Redémarrez Firefox si celui-ci était ouvert."
echo "Commande : $LAUNCHER"
