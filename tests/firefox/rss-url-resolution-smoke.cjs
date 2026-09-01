const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const source = fs.readFileSync(
  path.resolve(__dirname, "../../extension/firefox/background.js"),
  "utf8"
);

const start = source.indexOf("function isYouTubeSubscriptionHost");
const end = source.indexOf("async function openParabolicUrl", start);
assert(start >= 0 && end > start, "YouTube RSS resolver block must exist");

const resolverSource = source.slice(start, end);

const context = vm.createContext({
  URL,
  encodeURIComponent,
  isHttpUrl(value) {
    try {
      const parsed = new URL(value);
      return parsed.protocol === "http:" || parsed.protocol === "https:";
    } catch (_) {
      return false;
    }
  },
  fetch: async () => ({
    ok: true,
    status: 200,
    async text() {
      return '<meta itemprop="channelId" content="UC1234567890123456789012">';
    }
  })
});

vm.runInContext(
  `${resolverSource}\nthis.resolveSubscriptionFeedUrl = resolveSubscriptionFeedUrl;`,
  context
);

(async () => {
  const resolve = context.resolveSubscriptionFeedUrl;

  assert.equal(
    await resolve("https://example.com/feed.xml"),
    "https://example.com/feed.xml"
  );

  assert.equal(
    await resolve("https://www.youtube.com/playlist?list=PL1234567890abcdef"),
    "https://www.youtube.com/feeds/videos.xml?playlist_id=PL1234567890abcdef"
  );

  assert.equal(
    await resolve("https://www.youtube.com/watch?v=abc123&list=PL9876543210xyz"),
    "https://www.youtube.com/feeds/videos.xml?playlist_id=PL9876543210xyz"
  );

  assert.equal(
    await resolve("https://www.youtube.com/channel/UCabcdefghijklmnopqrstuv/videos"),
    "https://www.youtube.com/feeds/videos.xml?channel_id=UCabcdefghijklmnopqrstuv"
  );

  assert.equal(
    await resolve("https://www.youtube.com/@example/videos"),
    "https://www.youtube.com/feeds/videos.xml?channel_id=UC1234567890123456789012"
  );

  console.log("Firefox RSS URL resolution smoke test passed.");
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
