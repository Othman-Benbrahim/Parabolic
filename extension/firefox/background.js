const DEFAULT_SETTINGS = {
  trimPlaylist: false,
  showContextMenu: true,
  detectMedia: true,
  showMediaBadge: true
};

const MAX_MEDIA_PER_TAB = 60;
const MEDIA_EXTENSIONS = new Set([
  "mp4", "webm", "mov", "m4v", "mkv", "avi",
  "mp3", "m4a", "aac", "ogg", "opus", "flac", "wav"
]);
const SEGMENT_EXTENSIONS = new Set(["ts", "m4s", "cmfv", "cmfa"]);
const HLS_MIME_TYPES = new Set([
  "application/vnd.apple.mpegurl",
  "application/x-mpegurl",
  "audio/mpegurl",
  "audio/x-mpegurl"
]);

const mediaByTab = new Map();
let settings = { ...DEFAULT_SETTINGS };

function isHttpUrl(value) {
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:";
  } catch (_) {
    return false;
  }
}

function getHeader(headers, name) {
  const expectedName = name.toLowerCase();
  const header = (headers || []).find((item) => item.name.toLowerCase() === expectedName);
  return header ? header.value || "" : "";
}

function getExtension(url) {
  try {
    const filename = new URL(url).pathname.split("/").pop() || "";
    const dot = filename.lastIndexOf(".");
    return dot === -1 ? "" : filename.slice(dot + 1).toLowerCase();
  } catch (_) {
    return "";
  }
}

function isYouTubePageUrl(value) {
  try {
    const url = new URL(value);
    return ["youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com"].includes(url.hostname)
      && ["/watch", "/shorts/", "/live/"].some((path) => url.pathname === path || url.pathname.startsWith(path));
  } catch (_) {
    return false;
  }
}

function isGoogleVideoPlaybackUrl(value) {
  try {
    const url = new URL(value);
    return (url.hostname === "googlevideo.com" || url.hostname.endsWith(".googlevideo.com"))
      && url.pathname.endsWith("/videoplayback");
  } catch (_) {
    return false;
  }
}

async function detectYouTubeMedia(details) {
  if (!settings.detectMedia || details.tabId < 0 || !isGoogleVideoPlaybackUrl(details.url)) {
    return false;
  }

  try {
    const tab = await browser.tabs.get(details.tabId);
    if (!isYouTubePageUrl(tab.url)) {
      return false;
    }
    const streamUrl = new URL(details.url);
    const streamMime = (streamUrl.searchParams.get("mime") || "").toLowerCase();
    const streamKind = streamMime.startsWith("audio/") ? "audio" : "video";
    addMedia(details.tabId, {
      url: tab.url,
      pageUrl: tab.url,
      source: "youtube-network",
      kind: "youtube",
      label: tab.title || "YouTube video",
      mime: "YouTube adaptive media",
      streamTypes: [streamKind],
      frameId: details.frameId,
      discoveredAt: Date.now()
    });
    return true;
  } catch (_) {
    return false;
  }
}

function filenameFromHeaders(headers) {
  const disposition = getHeader(headers, "content-disposition");
  const utf8Match = disposition.match(/filename\*=UTF-8''([^;]+)/i);
  const regularMatch = disposition.match(/filename="?([^";]+)"?/i);
  const value = utf8Match?.[1] || regularMatch?.[1] || "";
  try {
    return decodeURIComponent(value);
  } catch (_) {
    return value;
  }
}

function classifyNetworkMedia(details) {
  if (!settings.detectMedia || details.tabId < 0 || !isHttpUrl(details.url)) {
    return null;
  }

  const extension = getExtension(details.url);
  if (SEGMENT_EXTENSIONS.has(extension)) {
    return null;
  }

  const mime = getHeader(details.responseHeaders, "content-type")
    .split(";", 1)[0]
    .trim()
    .toLowerCase();

  let kind = "";
  if (extension === "m3u8" || HLS_MIME_TYPES.has(mime)) {
    kind = "hls";
  } else if (extension === "mpd" || mime === "application/dash+xml") {
    kind = "dash";
  } else if (mime.startsWith("video/")) {
    kind = "video";
  } else if (mime.startsWith("audio/")) {
    kind = "audio";
  } else if (MEDIA_EXTENSIONS.has(extension)) {
    kind = ["mp3", "m4a", "aac", "ogg", "opus", "flac", "wav"].includes(extension)
      ? "audio"
      : "video";
  } else {
    return null;
  }

  const contentLength = Number.parseInt(getHeader(details.responseHeaders, "content-length"), 10);
  const path = new URL(details.url).pathname.toLowerCase();
  if (["audio", "video"].includes(kind)
      && /(\/|^)(?:init|seg(?:ment)?|chunk|frag(?:ment)?)[-_\d]/.test(path)
      && Number.isFinite(contentLength)
      && contentLength < 512 * 1024) {
    return null;
  }
  return {
    url: details.url,
    pageUrl: details.documentUrl || details.originUrl || "",
    source: "network",
    kind,
    mime,
    filename: filenameFromHeaders(details.responseHeaders),
    size: Number.isFinite(contentLength) ? contentLength : null,
    frameId: details.frameId,
    discoveredAt: Date.now()
  };
}

function candidateKey(candidate) {
  return `${candidate.kind || "media"}|${candidate.url}`;
}

function addMedia(tabId, candidate) {
  if (!settings.detectMedia || tabId < 0 || !candidate || !isHttpUrl(candidate.url)) {
    return;
  }

  const tabMedia = mediaByTab.get(tabId) || new Map();
  const key = candidateKey(candidate);
  const previous = tabMedia.get(key);
  const merged = {
    ...previous,
    ...candidate,
    discoveredAt: candidate.discoveredAt || Date.now()
  };
  if (previous?.streamTypes || candidate.streamTypes) {
    merged.streamTypes = [...new Set([
      ...(previous?.streamTypes || []),
      ...(candidate.streamTypes || [])
    ])];
  }
  for (const field of ["label", "mime", "filename", "pageUrl", "width", "height", "duration", "size"]) {
    if ((merged[field] === "" || merged[field] === null || typeof merged[field] === "undefined")
        && previous?.[field]) {
      merged[field] = previous[field];
    }
  }
  tabMedia.set(key, merged);

  while (tabMedia.size > MAX_MEDIA_PER_TAB) {
    tabMedia.delete(tabMedia.keys().next().value);
  }

  mediaByTab.set(tabId, tabMedia);
  updateBadge(tabId);
}

async function updateBadge(tabId) {
  const count = mediaByTab.get(tabId)?.size || 0;
  const text = settings.detectMedia && settings.showMediaBadge && count > 0
    ? String(Math.min(count, 99))
    : "";
  try {
    await browser.action.setBadgeBackgroundColor({ tabId, color: "#e63a3f" });
    await browser.action.setBadgeText({ tabId, text });
    await browser.action.setTitle({
      tabId,
      title: count > 0
        ? `${count} media source${count === 1 ? "" : "s"} detected`
        : "Open in Parabolic"
    });
  } catch (_) {
    // The tab may have been closed between detection and badge refresh.
  }
}

async function refreshAllBadges() {
  const tabs = await browser.tabs.query({});
  await Promise.all(tabs.map((tab) => updateBadge(tab.id)));
}

async function openParabolicUrl(url, tabId) {
  if (!isHttpUrl(url)) {
    throw new Error("Only HTTP and HTTPS URLs can be opened in Parabolic.");
  }

  let processedUrl = url;
  if (settings.trimPlaylist) {
    processedUrl = trimPlaylistFromUrl(url);
  }

  const formattedUrl = processedUrl.replace(/^https?:\/\//, "");
  const schemeUrl = `parabolic://${formattedUrl}`;
  if (Number.isInteger(tabId)) {
    await browser.tabs.update(tabId, { url: schemeUrl });
  } else {
    await browser.tabs.update({ url: schemeUrl });
  }
}

async function createContextMenu() {
  try {
    await browser.contextMenus.remove("openParabolicLink");
  } catch (_) {
    // The item does not exist yet.
  }
  if (settings.showContextMenu) {
    browser.contextMenus.create({
      id: "openParabolicLink",
      title: "Open link in Parabolic",
      contexts: ["link", "page", "video", "audio"]
    });
  }
}

async function loadSettings() {
  settings = { ...DEFAULT_SETTINGS, ...(await browser.storage.sync.get(DEFAULT_SETTINGS)) };
}

browser.runtime.onInstalled.addListener(async () => {
  const stored = await browser.storage.sync.get(Object.keys(DEFAULT_SETTINGS));
  const missingDefaults = {};
  for (const [key, value] of Object.entries(DEFAULT_SETTINGS)) {
    if (typeof stored[key] === "undefined") {
      missingDefaults[key] = value;
    }
  }
  if (Object.keys(missingDefaults).length > 0) {
    await browser.storage.sync.set(missingDefaults);
  }
  await loadSettings();
  await createContextMenu();
});

browser.runtime.onStartup.addListener(async () => {
  await loadSettings();
  await createContextMenu();
});

loadSettings().then(createContextMenu).catch(console.error);

browser.storage.onChanged.addListener(async (changes, namespace) => {
  if (namespace !== "sync") {
    return;
  }
  for (const key of Object.keys(DEFAULT_SETTINGS)) {
    if (changes[key]) {
      settings[key] = changes[key].newValue;
    }
  }
  if (changes.showContextMenu) {
    await createContextMenu();
  }
  if (changes.detectMedia && !settings.detectMedia) {
    mediaByTab.clear();
  }
  if (changes.detectMedia || changes.showMediaBadge) {
    await refreshAllBadges();
  }
});

browser.webRequest.onHeadersReceived.addListener(
  (details) => {
    if (isGoogleVideoPlaybackUrl(details.url)) {
      detectYouTubeMedia(details);
      return;
    }
    const candidate = classifyNetworkMedia(details);
    if (candidate) {
      addMedia(details.tabId, candidate);
    }
  },
  { urls: ["<all_urls>"] },
  ["responseHeaders"]
);

browser.runtime.onMessage.addListener(async (message, sender) => {
  if (!message || typeof message.type !== "string") {
    return undefined;
  }

  if (message.type === "media-detected" && sender.tab?.id !== undefined) {
    for (const candidate of message.candidates || []) {
      addMedia(sender.tab.id, {
        ...candidate,
        source: "page",
        frameId: sender.frameId,
        discoveredAt: Date.now()
      });
    }
    return { ok: true };
  }

  if (message.type === "get-media") {
    const tabId = Number(message.tabId);
    const candidates = [...(mediaByTab.get(tabId)?.values() || [])]
      .sort((left, right) => right.discoveredAt - left.discoveredAt);
    return { candidates, settings };
  }

  if (message.type === "clear-media") {
    const tabId = Number(message.tabId);
    mediaByTab.delete(tabId);
    await updateBadge(tabId);
    return { ok: true };
  }

  if (message.type === "open-parabolic") {
    await openParabolicUrl(message.url, Number(message.tabId));
    return { ok: true };
  }

  return undefined;
});

browser.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId !== "openParabolicLink") {
    return;
  }
  const urlToOpen = info.linkUrl || info.srcUrl || info.pageUrl || tab.url;
  openParabolicUrl(urlToOpen, tab.id).catch(console.error);
});

browser.commands.onCommand.addListener(async (command) => {
  if (command === "open-parabolic-current-tab") {
    const [tab] = await browser.tabs.query({ active: true, currentWindow: true });
    if (tab?.url) {
      await openParabolicUrl(tab.url, tab.id);
    }
  }
  if (command === "open-parabolic-options") {
    await browser.runtime.openOptionsPage();
  }
});

browser.tabs.onUpdated.addListener((tabId, changeInfo) => {
  if (changeInfo.url) {
    mediaByTab.delete(tabId);
    updateBadge(tabId);
  }
});

browser.tabs.onRemoved.addListener((tabId) => {
  mediaByTab.delete(tabId);
});
