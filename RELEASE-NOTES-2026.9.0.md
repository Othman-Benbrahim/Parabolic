# Parabolic 2026.9.0

## Highlights

- Added a formal persistent task state machine and typed failure diagnostics.
- Added bounded automatic retries with exponential backoff for transient failures.
- Added RSS and Atom subscriptions with keyword filters, latest-only mode and duplicate protection.
- Added extensible registries for media and collection resolvers.
- Added direct HTTP downloads and opt-in clipboard watching to Firefox 0.9.0.
- Added output verification and optional SHA-256 calculation after downloads.
- Fixed scheduled downloads stopping after completed items were cleared.

## Platforms

The persistent service, resolver pipeline, subscriptions and Firefox Native Messaging protocol remain available on Windows x64/ARM64, Linux Flatpak x86_64/aarch64 and macOS Intel/Apple Silicon.

## Firefox privacy

Clipboard access is explicit and limited to the open popup. RSS configuration stays in the local Parabolic service. Neither clipboard contents nor subscription data are sent to the project maintainers.
