# Parabolic Flatpak — Firefox integration

The Parabolic Flatpak contains the Native Messaging relay and persistent
download service, but Firefox requires a small host-side manifest and launcher
to start the sandboxed command.

## Install

1. Install the architecture-matched `Parabolic-2026.8.6-*.flatpak` bundle.
2. Extract this integration archive.
3. Run `chmod +x install-flatpak-firefox-integration.sh`.
4. Run `./install-flatpak-firefox-integration.sh`.
5. Restart Firefox and install or update the Parabolic Firefox add-on 0.8.2.

The installer writes only below `~/.local/lib/parabolic-flatpak` and
`~/.mozilla/native-messaging-hosts`.

Firefox installed directly from Mozilla or the distribution is the supported
configuration. A strictly confined Firefox Flatpak or Snap additionally needs
the WebExtensions XDG desktop portal supplied by the host distribution.

## Uninstall

Run `./uninstall-flatpak-firefox-integration.sh`, then remove the application
with `flatpak uninstall org.nickvision.tubeconverter` if desired.
