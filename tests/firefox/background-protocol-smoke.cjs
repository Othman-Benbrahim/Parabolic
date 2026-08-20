// Dependency-free smoke test for the Firefox background/native protocol.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { webcrypto } = require("node:crypto");

const runtimeMessageListeners = [];
const nativeMessageListeners = [];
const nativeDisconnectListeners = [];
const nativeRequests = [];
const tabMessages = [];
const notifications = [];
let nativeHostName = "";

const nativePort = {
  onMessage: { addListener(listener) { nativeMessageListeners.push(listener); } },
  onDisconnect: { addListener(listener) { nativeDisconnectListeners.push(listener); } },
  postMessage(message) {
    nativeRequests.push(message);
    queueMicrotask(() => {
      const payload = message.type === "hello"
        ? { appVersion: "test", protocolVersion: 3, capabilities: ["ytdlp-update", "persistent-queue", "priority", "resolver-pipeline", "scheduling"] }
        : message.type === "list-downloads"
          ? { downloads: [] }
        : message.type === "download"
          ? { downloadId: "download-test", status: "queued" }
          : message.type === "check-ytdlp-update"
            ? {
                currentVersion: "2026.03.17",
                latestVersion: "2026.8.13",
                updateAvailable: true,
                updated: false,
                message: "yt-dlp 2026.8.13 is available."
              }
            : message.type === "update-ytdlp"
              ? {
                  currentVersion: "2026.8.13",
                  latestVersion: "2026.8.13",
                  updateAvailable: false,
                  updated: true,
                  message: "yt-dlp was updated successfully to 2026.8.13."
                }
              : {};
      for (const listener of nativeMessageListeners) {
        listener({
          protocolVersion: 3,
          requestId: message.requestId,
          type: "response",
          ok: true,
          payload
        });
      }
    });
  },
  disconnect() {}
};

const browser = {
  runtime: {
    id: "parabolic-media-detector@othmanbenbrahim.dev",
    lastError: null,
    connectNative(name) {
      nativeHostName = name;
      return nativePort;
    },
    getManifest() { return { version: "0.8.2" }; },
    getURL(value) { return `moz-extension://test/${value}`; },
    onMessage: { addListener(listener) { runtimeMessageListeners.push(listener); } },
    onInstalled: { addListener() {} },
    onStartup: { addListener() {} },
    openOptionsPage: async () => undefined
  },
  storage: {
    sync: {
      async get(defaults) {
        return {
          ...defaults,
          resolverPreference: "yt-dlp",
          cobaltEndpoint: "https://cobalt.example/",
          cobaltAuthScheme: "bearer",
          speedLimitKbps: 2048,
          networkStrategy: "aggressive",
          authenticationMode: "firefox",
          proxyMode: "direct",
          sendPageReferer: true
        };
      },
      async set() {}
    },
    local: {
      async get(defaults) { return { ...defaults, cobaltAuthToken: "local-test-token" }; },
      async set() {}
    },
    onChanged: { addListener() {} }
  },
  contextMenus: {
    async remove() {},
    create() {},
    onClicked: { addListener() {} }
  },
  commands: { onCommand: { addListener() {} } },
  webRequest: { onHeadersReceived: { addListener() {} } },
  action: {
    async setBadgeBackgroundColor() {},
    async setBadgeText() {},
    async setTitle() {}
  },
  tabs: {
    async query() { return []; },
    async get(tabId) { return { id: tabId, url: "https://example.com/watch", title: "Test" }; },
    async update() {},
    async sendMessage(tabId, message) { tabMessages.push({ tabId, message }); },
    onUpdated: { addListener() {} },
    onRemoved: { addListener() {} }
  },
  notifications: {
    async create(id, options) { notifications.push({ id, options }); }
  }
};

const context = vm.createContext({
  browser,
  console,
  crypto: webcrypto,
  URL,
  Map,
  Set,
  Promise,
  Number,
  String,
  Date,
  Math,
  setTimeout,
  clearTimeout,
  queueMicrotask
});

for (const filename of ["utils.js", "background.js"]) {
  const source = fs.readFileSync(
    path.resolve(__dirname, "..", "..", "extension", "firefox", filename),
    "utf8"
  );
  vm.runInContext(source, context, { filename });
}

async function send(message, sender = {}) {
  assert.equal(runtimeMessageListeners.length, 1, "Background message listener must be registered");
  return runtimeMessageListeners[0](message, sender);
}

(async () => {
  const bridge = await send({ type: "bridge-status" });
  assert.equal(bridge.available, true);
  assert.equal(nativeHostName, "com.nickvision.parabolic");
  assert.equal(nativeRequests[0].protocolVersion, 3);
  assert.equal(nativeRequests[0].type, "hello");

  const sender = {
    tab: { id: 42, url: "https://example.com/watch/42", title: "Example title" }
  };
  const download = await send({
    type: "native-download",
    request: {
      pageUrl: "https://untrusted-frame.example/embed",
      mediaUrl: "blob:https://example.com/not-transferable",
      preset: "720",
      formatId: "22",
      sourceKind: "html5"
    }
  }, sender);
  assert.equal(download.ok, true);
  assert.equal(download.mode, "native");
  const request = nativeRequests.find((item) => item.type === "download");
  assert(request, "Download request must reach the native host");
  assert.equal(request.payload.tabId, 42);
  assert.equal(request.payload.pageUrl, "https://example.com/watch/42");
  assert.equal(request.payload.mediaUrl, "");
  assert.equal(request.payload.manifestUrl, "");
  assert.equal(request.payload.preset, "720");
  assert.equal(request.payload.formatId, "22");
  assert.equal(request.payload.priority, "normal");
  assert.equal(request.payload.resolverPreference, "yt-dlp");
  assert.equal(request.payload.cobaltEndpoint, "https://cobalt.example/");
  assert.equal(request.payload.cobaltAuthScheme, "bearer");
  assert.equal(request.payload.cobaltAuthToken, "local-test-token");
  assert.equal(request.payload.speedLimitKbps, 2048);
  assert.equal(request.payload.scheduledAt, "");
  assert.equal(request.payload.networkStrategy, "aggressive");
  assert.equal(request.payload.authenticationMode, "firefox");
  assert.equal(request.payload.proxyMode, "direct");
  assert.equal(request.payload.sendPageReferer, true);

  const facebookSender = {
    tab: { id: 84, url: "https://www.facebook.com/?_fb_noscript=1", title: "Facebook video" },
    frameId: 0
  };
  await send({
    type: "media-detected",
    candidates: [{
      url: "https://video-cdn.example/playlist.m3u8?token=short-lived",
      pageUrl: "https://www.facebook.com/reel/123456789",
      kind: "hls",
      source: "network"
    }]
  }, facebookSender);
  const facebookDownload = await send({
    type: "native-download",
    request: {
      pageUrl: "https://www.facebook.com/reel/123456789?ref=sharing",
      frameUrl: "https://www.facebook.com/?_fb_noscript=1",
      preset: "best",
      sourceKind: "page"
    }
  }, facebookSender);
  assert.equal(facebookDownload.ok, true);
  const facebookRequest = nativeRequests.filter((item) => item.type === "download").at(-1);
  assert.equal(facebookRequest.payload.pageUrl, "https://www.facebook.com/reel/123456789");
  assert.equal(facebookRequest.payload.manifestUrl, "https://video-cdn.example/playlist.m3u8?token=short-lived");
  assert.equal(facebookRequest.payload.manifestKind, "hls");

  await send({
    type: "media-detected",
    candidates: [{
      url: "https://video-cdn.example/facebook-progressive.mp4?token=short-lived",
      pageUrl: "https://www.facebook.com/?_fb_noscript=1",
      kind: "video",
      size: 25 * 1024 * 1024,
      source: "network"
    }]
  }, facebookSender);
  const facebookDirectDownload = await send({
    type: "native-download",
    request: {
      pageUrl: "https://www.facebook.com/?_fb_noscript=1",
      frameUrl: "https://www.facebook.com/?_fb_noscript=1",
      preset: "best",
      sourceKind: "page"
    }
  }, facebookSender);
  assert.equal(facebookDirectDownload.ok, true);
  const facebookDirectRequest = nativeRequests.filter((item) => item.type === "download").at(-1);
  assert.equal(facebookDirectRequest.payload.pageUrl, "https://www.facebook.com/");
  assert.equal(
    facebookDirectRequest.payload.directFallbackUrl,
    "https://video-cdn.example/facebook-progressive.mp4?token=short-lived"
  );
  assert.equal(facebookDirectRequest.payload.directFallbackKind, "video");

  const mediaCatalog = await send({ type: "get-media", tabId: 42 }, sender);
  assert.equal(Object.hasOwn(mediaCatalog.settings, "cobaltAuthToken"), false);

  const helloCountBeforePulse = nativeRequests.filter((item) => item.type === "hello").length;
  const pulse = await send({ type: "bridge-pulse" }, sender);
  assert.equal(pulse.ok, true);
  assert.equal(nativeRequests.filter((item) => item.type === "hello").length, helloCountBeforePulse + 1);

  const updateCheck = await send({ type: "native-ytdlp-check" }, sender);
  assert.equal(updateCheck.ok, true);
  assert.equal(updateCheck.result.updateAvailable, true);
  assert(nativeRequests.some((item) => item.type === "check-ytdlp-update"));

  const update = await send({ type: "native-ytdlp-update" }, sender);
  assert.equal(update.ok, true);
  assert.equal(update.result.updated, true);
  assert(nativeRequests.some((item) => item.type === "update-ytdlp"));

  const cancel = await send({ type: "native-cancel", downloadId: "download-test" }, sender);
  assert.equal(cancel.ok, true);
  assert(nativeRequests.some((item) => item.type === "cancel"
    && item.payload.downloadId === "download-test"));

  const pause = await send({ type: "native-pause", downloadId: "download-test" }, sender);
  assert.equal(pause.ok, true);
  assert(nativeRequests.some((item) => item.type === "pause"));

  const resume = await send({ type: "native-resume", downloadId: "download-test" }, sender);
  assert.equal(resume.ok, true);
  assert(nativeRequests.some((item) => item.type === "resume"));

  const priority = await send({
    type: "native-set-priority",
    downloadId: "download-test",
    priority: "high"
  }, sender);
  assert.equal(priority.ok, true);
  assert(nativeRequests.some((item) => item.type === "set-priority"
    && item.payload.priority === "high"));

  const openFolder = await send({ type: "native-open-folder", downloadId: "download-test" }, sender);
  assert.equal(openFolder.ok, true);
  assert(nativeRequests.some((item) => item.type === "open-folder"
    && item.payload.downloadId === "download-test"));

  for (const listener of nativeMessageListeners) {
    listener({
      protocolVersion: 3,
      type: "event",
      payload: {
        downloadId: "download-test",
        tabId: 42,
        status: "completed",
        filename: "Example.mp4"
      }
    });
  }
  await new Promise((resolve) => setTimeout(resolve, 0));
  assert(tabMessages.some(({ tabId, message }) => tabId === 42
    && message.type === "parabolic-native-event"
    && message.event.status === "completed"));
  assert.equal(notifications.length, 1);
  console.log("Background protocol smoke test passed");
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
