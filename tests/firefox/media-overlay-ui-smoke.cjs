const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const media = fs.readFileSync(
  path.resolve(__dirname, "../../extension/firefox/media-overlay.js"),
  "utf8"
);
const background = fs.readFileSync(
  path.resolve(__dirname, "../../extension/firefox/background.js"),
  "utf8"
);

assert.match(
  media,
  /directButton\.textContent\s*=\s*"T\\u00e9l\\u00e9chargement direct"/
);
assert.match(media, /rssButton\.textContent\s*=\s*"RSS"/);
assert.match(
  media,
  /settingsButton\.textContent\s*=\s*"Param\\u00e8tres"/
);

assert.match(media, /directButton\.addEventListener\("click", startDirectDownload\)/);
assert.match(media, /rssButton\.addEventListener\("click", addRssSubscription\)/);
assert.match(media, /settingsButton\.addEventListener\("click", openSettings\)/);

assert.match(media, /closeButton\.textContent\s*=\s*"\\u00d7"/);
assert.match(media, /\.close\s*\{[\s\S]*?position:\s*absolute/);
assert.match(media, /shell\.append\(closeButton, group, menu, toast\)/);
assert.match(media, /group\.append\(mainButton, menuButton\)/);
assert.match(media, /menuButton\.textContent\s*=\s*"\u25be"/);

assert.match(media, /overlayDismissed\s*=\s*true/);
assert.match(media, /if \(overlayDismissed\)/);
assert.doesNotMatch(
  media,
  /storage\.(?:local|sync).*overlayDismissed|overlayDismissed.*storage\.(?:local|sync)/s,
  "page-only dismissal must not be persisted"
);

assert.equal(media.includes("T\u00c3"), false, "corrupted direct-download typography must be gone");
assert.equal(media.includes("Param\u00c3"), false, "corrupted settings typography must be gone");
assert.equal(media.includes("\u00c3\u2014"), false, "corrupted close glyph must be gone");
assert.equal(media.includes("subscription\u00e2"), false, "corrupted RSS ellipsis must be gone");

assert.match(background, /async function resolveSubscriptionFeedUrl/);
assert.match(background, /playlist_id=/);
assert.match(background, /channel_id=/);
assert.match(background, /browser\.runtime\.openOptionsPage\(\)/);

console.log("Firefox media overlay UI smoke test passed.");
