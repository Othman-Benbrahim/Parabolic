#!/usr/bin/env bash

set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "Usage: $0 <linux-x64|linux-arm64>" >&2
    exit 2
fi

ASSET_ARCH="$1"
case "$ASSET_ARCH" in
    linux-x64|linux-arm64) ;;
    *) echo "Unsupported N_m3u8DL-RE architecture: $ASSET_ARCH" >&2; exit 2 ;;
esac

for command_name in curl jq tar; do
    command -v "$command_name" >/dev/null 2>&1 || {
        echo "Missing build dependency: $command_name" >&2
        exit 1
    }
done

VERSION="v0.6.0-beta"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUTPUT_DIR="$REPO_ROOT/flatpak/generated"
TEMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TEMP_DIR"' EXIT

AUTH_ARGS=()
if [[ -n "${GH_TOKEN:-}" ]]; then
    AUTH_ARGS=(--header "Authorization: Bearer $GH_TOKEN")
fi

ASSET_URL="$(
    curl --fail --silent --show-error --location "${AUTH_ARGS[@]}" \
        "https://api.github.com/repos/nilaoda/N_m3u8DL-RE/releases/tags/$VERSION" |
        jq -r --arg arch "$ASSET_ARCH" \
            '.assets[] | select(.name | contains($arch)) | .browser_download_url' |
        head -n1
)"
[[ -n "$ASSET_URL" && "$ASSET_URL" != "null" ]] || {
    echo "Unable to locate the N_m3u8DL-RE $ASSET_ARCH release asset." >&2
    exit 1
}

curl --fail --location "$ASSET_URL" -o "$TEMP_DIR/N_m3u8DL-RE.tar.gz"
tar -xzf "$TEMP_DIR/N_m3u8DL-RE.tar.gz" -C "$TEMP_DIR"
EXECUTABLE="$(find "$TEMP_DIR" -type f -name 'N_m3u8DL-RE' -print -quit)"
[[ -n "$EXECUTABLE" ]] || {
    echo "The N_m3u8DL-RE executable is missing from the downloaded archive." >&2
    exit 1
}

mkdir -p "$OUTPUT_DIR"
install -m 0755 "$EXECUTABLE" "$OUTPUT_DIR/N_m3u8DL-RE"
echo "$OUTPUT_DIR/N_m3u8DL-RE"
