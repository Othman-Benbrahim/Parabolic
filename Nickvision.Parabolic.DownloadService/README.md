# Parabolic persistent download service

This per-user process owns downloads accepted from Firefox on Windows, Linux
Flatpak and macOS. It listens on `Parabolic.DownloadManager.v1` and remains active
after the Firefox Native Messaging relay disconnects. Windows uses a named pipe
protected by an explicit account ACL. Linux and macOS use .NET's Unix domain
socket implementation with `CurrentUserOnly` enabled.

Its first milestone provides:

- an isolated SQLite recovery table named `browser_recovery_queue`;
- High, Normal and Low queue priorities;
- pause, resume, cancel and reprioritize commands;
- active-download synchronization after Firefox reconnects;
- automatic recovery after process or computer restart;
- reuse of Parabolic's yt-dlp, aria2c and FFmpeg download pipeline.

The Windows installer starts the service at logon, the Linux Flatpak relay
starts its sandboxed service on demand, and macOS uses a per-user LaunchAgent.
All three paths run without administrator privileges.
