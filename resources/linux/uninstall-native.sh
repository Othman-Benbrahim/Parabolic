#!/usr/bin/env bash

set -euo pipefail

XDG_DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
XDG_CONFIG_HOME="${XDG_CONFIG_HOME:-$HOME/.config}"
INSTALL_ROOT="${PARABOLIC_INSTALL_ROOT:-$HOME/.local/lib/parabolic}"
BIN_DIR="${PARABOLIC_BIN_DIR:-$HOME/.local/bin}"
APP_ID="org.nickvision.tubeconverter"

if [[ -z "$INSTALL_ROOT" || "$INSTALL_ROOT" == "/" || "$INSTALL_ROOT" == "$HOME" ]]; then
    echo "Refusing unsafe installation root: $INSTALL_ROOT" >&2
    exit 2
fi

if command -v systemctl >/dev/null 2>&1; then
    systemctl --user disable --now parabolic-download-service.service >/dev/null 2>&1 || true
fi
rm -f "$XDG_CONFIG_HOME/systemd/user/parabolic-download-service.service"
if command -v systemctl >/dev/null 2>&1; then
    systemctl --user daemon-reload >/dev/null 2>&1 || true
fi
rm -f "$HOME/.mozilla/native-messaging-hosts/com.nickvision.parabolic.json"
rm -f "$BIN_DIR/parabolic"
rm -f "$XDG_DATA_HOME/applications/$APP_ID.desktop"
rm -f "$XDG_DATA_HOME/dbus-1/services/$APP_ID.service"
rm -f "$XDG_DATA_HOME/metainfo/$APP_ID.metainfo.xml"
rm -f "$XDG_DATA_HOME/icons/hicolor/scalable/apps/$APP_ID.svg"
rm -f "$XDG_DATA_HOME/icons/hicolor/symbolic/apps/$APP_ID-symbolic.svg"
rm -rf "$INSTALL_ROOT"
echo "Parabolic et son intégration Firefox ont été supprimés pour $USER."
