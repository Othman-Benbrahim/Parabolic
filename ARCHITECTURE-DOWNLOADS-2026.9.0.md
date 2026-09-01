# Parabolic download architecture — 2026.9.0

This release introduces the foundations selected from the Persepolis, Hitomi Downloader and VidBee analysis without importing their UI stacks or engines.

## Cross-platform boundary

The persistent download daemon owns task state, retry policy, RSS polling, resolver registries and post-processing. The same C# sources are compiled for Windows, Linux and macOS. Firefox communicates only through Native Messaging and the per-user named pipe or Unix-domain socket relay.

## Task lifecycle

`scheduled → queued → running → processing → completed`

Alternative transitions cover pause/resume, cancellation, typed failure and `retry-scheduled`. Only network, rate-limit and unknown transient failures are retried automatically. Authentication, permission, disk-space, dependency, geo-restriction and unavailable-media failures require user action.

## Subscriptions and collections

RSS and Atom are the first collection resolver. A subscription stores its URL, preset, priority, interval, optional keyword filter, latest-only behavior and at most 500 seen item identifiers. Automatic items enter the same persistent queue as Firefox and desktop downloads.

Future gallery, channel or site-specific resolvers must implement `ICollectionResolver` or `IMediaResolver`; they must not be added as new branches throughout the Native Messaging server.

## Direct HTTP and clipboard

Direct HTTP links use the existing yt-dlp/aria2 path. Windows and GNOME dialogs continue to prefill from the clipboard. Firefox 0.9.0 adds an explicit paste field and an optional watcher limited to the lifetime of the open popup.

## Post-download pipeline

Every Firefox/daemon task executes `verify-output`. Optional `sha256` calculates and reports the file digest. New processors must implement `IPostDownloadProcessor` and remain operating-system independent unless a platform adapter is provided for all three desktop targets.
