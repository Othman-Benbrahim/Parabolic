![](resources/banner.png)

# Features
- **One-Click Capture**: Send the URL of your active tab to Parabolic instantly by clicking the Extension/Add-on icon.
- **Context Menu Integration**: Right-click and select: `Open link in Parabolic` on any specific link to send that exact URL without needing to open the page. Otherwise, use the current tab's URL.
- **Keyboard shortcut**: Press `Alt` + `P` to send the URL of your active tab to Parabolic.
- **Lightweight & Fast**: Designed to be fast, unobtrusive, easy to use, and respects your privacy.
- **Firefox Media Detection**: The Firefox version detects HTML5 video/audio, HLS playlists and DASH manifests, including media loaded in embedded frames.
- **Detected Sources Popup**: Click the Firefox toolbar icon to analyze the current page or send a detected stream directly to Parabolic.
- **YouTube Adaptive Detection**: Firefox recognizes YouTube's `googlevideo.com` media traffic and groups audio/video requests under the stable watch-page URL.
- **Existing Instance Activation**: On Windows, links are forwarded to an already running Parabolic window instead of being dropped.

# Installation
> [!NOTE]  
> The Parabolic Extension should be available on the Chrome Web Store soon. In the meantime, use the Local Installation. 
#### Chrome Local Install
1. Go to: `chrome://extensions`.
2. Enable `Developer mode`.
3. Click the `Load unpacked` button and select the extension folder.

##### Firefox
[![get-the-addon](resources/firefox.png)](https://addons.mozilla.org/en-US/firefox/addon/parabolic/)

The enhanced Firefox-only detector is documented in [`firefox/ARCHITECTURE.md`](firefox/ARCHITECTURE.md). It can be loaded temporarily from `about:debugging` by selecting `firefox/manifest.json`.
