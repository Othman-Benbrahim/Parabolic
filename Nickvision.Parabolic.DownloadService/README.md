# Parabolic persistent download service

This Windows per-user process owns downloads accepted from Firefox. It listens
on the named pipe `Parabolic.DownloadManager.v1`, protected by an explicit ACL
for the owning Windows account, and remains active after the Firefox Native
Messaging relay disconnects. The ACL intentionally permits the same account at
different elevation levels so a non-elevated browser can reach a service that
an elevated installer started.

Its first milestone provides:

- an isolated SQLite recovery table named `browser_recovery_queue`;
- High, Normal and Low queue priorities;
- pause, resume, cancel and reprioritize commands;
- active-download synchronization after Firefox reconnects;
- automatic recovery after process or computer restart;
- reuse of Parabolic's yt-dlp, aria2c and FFmpeg download pipeline.

The Windows installer starts the service after installation and registers it
for the current user's logon. It runs without administrator privileges.
