const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const mediaPath = path.resolve(__dirname, "../../extension/firefox/media-overlay.js");
const backgroundPath = path.resolve(__dirname, "../../extension/firefox/background.js");
const media = fs.readFileSync(mediaPath, "utf8");
const background = fs.readFileSync(backgroundPath, "utf8");

for (const label of ["TÃ©lÃ©chargement direct", "RSS", "ParamÃ¨tres"]) {
  assert.match(
    media,
    new RegExp(`textContent\\s*=\\s*["']${label}["']`),
    `${label} shortcut should be visible in the in-player menu`
  );
}

assert.match(media, /directButton\.addEventListener\("click", startDirectDownload\)/);
assert.match(media, /rssButton\.addEventListener\("click", addRssSubscription\)/);
assert.match(media, /settingsButton\.addEventListener\("click", openSettings\)/);

assert.match(media, /closeButton\.textContent\s*=\s*["']Ã—["']/);
assert.match(media, /overlayDismissed\s*=\s*true/);
assert.match(media, /if \(overlayDismissed\)/);
assert.doesNotMatch(
  media,
  /storage\.(?:local|sync).*overlayDismissed|overlayDismissed.*storage\.(?:local|sync)/s,
  "page-only dismissal must not be persisted"
);

assert.match(media, /type:\s*["']native-download["']/);
assert.match(media, /type:\s*["']native-add-subscription["']/);
assert.match(media, /type:\s*["']open-options-page["']/);

assert.match(background, /message\.type\s*===\s*["']open-options-page["']/);
assert.match(background, /browser\.runtime\.openOptionsPage\(\)/);

console.log("Firefox media overlay UI smoke test passed.");
