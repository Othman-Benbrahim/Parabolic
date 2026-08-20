#!/usr/bin/env bash

set -euo pipefail

PLIST_PATH="$HOME/Library/LaunchAgents/com.nickvision.parabolic.download-service.plist"
launchctl bootout "gui/$UID" "$PLIST_PATH" >/dev/null 2>&1 || true
rm -f "$PLIST_PATH"
rm -f "$HOME/Library/Application Support/Mozilla/NativeMessagingHosts/com.nickvision.parabolic.json"
echo "L'intégration Firefox de Parabolic a été supprimée. L'application n'a pas été effacée."
read -r -p "Appuyez sur Entrée pour fermer…" _
