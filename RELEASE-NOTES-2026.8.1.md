# Parabolic 2026.8.1 — Persistent Download Engine

Parabolic now begins its transition from a browser-attached downloader to a
persistent download manager.

## Highlights

- Added a per-user Windows download service that continues accepted downloads after Firefox closes.
- Replaced the long-running Native Messaging process with a lightweight named-pipe relay.
- Added a dedicated SQLite recovery queue for browser-owned downloads.
- Preserved partial files and enabled continuation after service or computer interruption.
- Added High, Normal and Low queue priorities.
- Added pause, resume, cancellation, reprioritization and active-queue synchronization commands.
- Upgraded the Firefox integration to Native Messaging protocol v2.
- Removed the previous double-handshake delay after starting a download.
- Updated the Firefox add-on to Parabolic Download Manager 0.5.0.

## Installation

Install the Windows setup package before loading Firefox add-on 0.5.0. The
installer registers and starts the persistent service automatically. The
portable package does not configure Firefox Native Messaging.

Add-on 0.5.0 and desktop 2026.8.1 must be installed together; the previous
2026.8.0 bridge uses protocol v1.

## Scope of this milestone

This release establishes persistence, recovery and priority scheduling. Cobalt,
the intelligent HLS/DASH fragment manager, DNS/CDN retry and full queue control
inside the desktop interface will follow in later milestones.

DRM decryption is not supported. Users remain responsible for respecting
copyright, access restrictions and website terms.
