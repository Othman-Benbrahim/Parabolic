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
        ? { appVersion: "test", protocolVersion: 1, capabilities: ["ytdlp-update"] }
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
          protocolVersion: 1,
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
    getManifest() { return { version: "0.4.0" }; },
    getURL(value) { return `moz-extension://test/${value}`; },
    onMessage: { addListener(listener) { runtimeMessageListeners.push(listener); } },
    onInstalled: { addListener() {} },
    onStartup: { addListener() {} },
    openOptionsPage: async () => undefined
  },
  storage: {
    sync: {
      async get(defaults) { return { ...defaults }; },
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
  assert.equal(vm.runInContext("NATIVE_PULSE_DELAY", context), 2000,
    "The second native hello must be delayed long enough to reproduce the manual sequence");
  const bridge = await send({ type: "bridge-status" });
  assert.equal(bridge.available, true);
  assert.equal(nativeHostName, "com.nickvision.parabolic");
  assert.equal(nativeRequests[0].protocolVersion, 1);
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
  assert.equal(request.payload.preset, "720");
  assert.equal(request.payload.formatId, "22");
  assert(nativeRequests.filter((item) => item.type === "hello").length >= 3,
    "The download path must pulse the native loop twice without requiring a second menu click");

  const helloCountBeforePulse = nativeRequests.filter((item) => item.type === "hello").length;
  const pulse = await send({ type: "bridge-pulse" }, sender);
  assert.equal(pulse.ok, true);
  assert.equal(nativeRequests.filter((item) => item.type === "hello").length, helloCountBeforePulse + 2);

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

  const openFolder = await send({ type: "native-open-folder", downloadId: "download-test" }, sender);
  assert.equal(openFolder.ok, true);
  assert(nativeRequests.some((item) => item.type === "open-folder"
    && item.payload.downloadId === "download-test"));

  for (const listener of nativeMessageListeners) {
    listener({
      protocolVersion: 1,
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
