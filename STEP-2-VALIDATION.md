# Step 2 validation — Parabolic 2026.8.2 / Firefox 0.6.0

This is a validation milestone, not the final GitHub Release. Publish the final release only after steps 1, 2, and 3 are accepted.

## Workflow validation

After pushing the source, wait for the Windows and Firefox Add-on workflows to finish. Download:

- `NickvisionParabolicSetup-x64` (or ARM64 for an ARM PC);
- `parabolic-download-manager-firefox`.

Install the new setup before loading the unsigned `0.6.0` XPI temporarily from `about:debugging#/runtime/this-firefox`.

## Test A — direct media routing

1. Open a page with a plain MP4, HLS (`.m3u8`), or DASH (`.mpd`) source.
2. Start playback and open the Parabolic toolbar popup.
3. Download the detected media source.
4. Confirm that the download starts and completes without opening the desktop window.

The task snapshot/event reports resolver `direct`.

## Test B — bandwidth limit

1. Open the add-on settings.
2. Set **Bandwidth limit** to `512` KiB/s.
3. Start a sufficiently large download.
4. Confirm that the displayed speed stabilizes close to the configured limit.
5. Reset the setting to `0` when finished.

## Test C — scheduled download

1. Open the arrow menu on the in-player Parabolic button.
2. Select **Schedule download…** and choose a time a few minutes ahead.
3. Confirm the `scheduled` status.
4. Close Firefox; optionally restart Windows or the Parabolic download service.
5. Confirm that the task starts at the requested time.

## Test D — Cobalt (optional)

Use only a self-hosted Cobalt instance or one whose owner authorized your client.

1. Enter the complete Cobalt API endpoint in add-on settings.
2. Select the authentication scheme and token if required.
3. Keep **Automatic** to use Cobalt only after yt-dlp discovery returns no media, or select **Cobalt** to test it directly.
4. Start a non-scheduled download and confirm resolver `cobalt`.

Cobalt scheduling is deliberately deferred to step 3 because its returned media URLs may expire before the scheduled start.
