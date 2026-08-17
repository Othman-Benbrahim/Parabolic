const DEFAULT_SETTINGS = {
  trimPlaylist: false,
  showContextMenu: true,
  detectMedia: true,
  showMediaBadge: true,
  showOverlay: true,
  quickDownloadPreset: "best",
  defaultPriority: "normal",
  resolverPreference: "auto",
  cobaltEndpoint: "",
  cobaltAuthScheme: "none",
  speedLimitKbps: 0,
  networkStrategy: "balanced",
  authenticationMode: "parabolic",
  proxyMode: "parabolic",
  sendPageReferer: false,
  overlayPosition: "top-right",
  fallbackToProtocol: false
};

const NATIVE_HOST_NAME = "com.nickvision.parabolic";
const NATIVE_PROTOCOL_VERSION = 3;
const NATIVE_REQUEST_TIMEOUT = 8000;
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
const DOWNLOAD_PRESETS = new Set(["best", "1080", "720", "480", "audio"]);
const DOWNLOAD_PRIORITIES = new Set(["high", "normal", "low"]);
const RESOLVER_PREFERENCES = new Set(["auto", "yt-dlp", "cobalt"]);
const NETWORK_STRATEGIES = new Set(["conservative", "balanced", "aggressive"]);
const AUTHENTICATION_MODES = new Set(["parabolic", "firefox", "none"]);
const PROXY_MODES = new Set(["parabolic", "direct"]);

const mediaByTab = new Map();
const downloadTabs = new Map();
let settings = { ...DEFAULT_SETTINGS };

function requestId() {
  if (globalThis.crypto?.randomUUID) {
    return globalThis.crypto.randomUUID();
  }
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function serializableError(error, fallback) {
  return {
    code: error?.code || "PARABOLIC_BRIDGE_ERROR",
    message: error?.message || fallback
  };
}

class NativeBridge {
  constructor() {
    this.port = null;
    this.pending = new Map();
  }

  connect() {
    if (this.port) {
      return this.port;
    }

    const port = browser.runtime.connectNative(NATIVE_HOST_NAME);
    this.port = port;
    port.onMessage.addListener((message) => this.onMessage(message));
    port.onDisconnect.addListener(() => this.onDisconnect(port));
    return port;
  }

  onMessage(message) {
    if (!message || typeof message !== "object") {
      return;
    }

    if (message.type === "event") {
      this.forwardEvent(message).catch(console.error);
      return;
    }

    const correlationId = message.replyTo || message.requestId;
    const pending = this.pending.get(correlationId);
    if (!pending) {
      return;
    }

    clearTimeout(pending.timeout);
    this.pending.delete(correlationId);
    if (message.ok === false) {
      const error = new Error(message.error?.message || "Parabolic rejected the request.");
      error.code = message.error?.code || "PARABOLIC_REQUEST_REJECTED";
      pending.reject(error);
      return;
    }
    pending.resolve(message);
  }

  async forwardEvent(message) {
    const payload = message.payload || {};
    const tabId = Number(payload.tabId ?? downloadTabs.get(payload.downloadId));
    if (Number.isInteger(tabId) && tabId >= 0) {
      await browser.tabs.sendMessage(tabId, {
        type: "parabolic-native-event",
        event: payload
      }).catch(() => undefined);
    }

    if (payload.downloadId && Number.isInteger(tabId)) {
      downloadTabs.set(payload.downloadId, tabId);
    }
    if (["completed", "failed", "cancelled"].includes(payload.status)) {
      downloadTabs.delete(payload.downloadId);
    }
    if (payload.status === "completed") {
      await browser.notifications.create(`parabolic-${payload.downloadId || Date.now()}`, {
        type: "basic",
        iconUrl: browser.runtime.getURL("icons/icon128.png"),
        title: "Parabolic download complete",
        message: payload.filename || "Your media is ready."
      }).catch(() => undefined);
    }
  }

  onDisconnect(port) {
    if (this.port !== port) {
      return;
    }
    this.port = null;
    const message = browser.runtime.lastError?.message
      || "The Parabolic native bridge is not installed or stopped responding.";
    for (const pending of this.pending.values()) {
      clearTimeout(pending.timeout);
      const error = new Error(message);
      error.code = "PARABOLIC_BRIDGE_UNAVAILABLE";
      pending.reject(error);
    }
    this.pending.clear();
  }

  request(type, payload = {}, timeoutMilliseconds = NATIVE_REQUEST_TIMEOUT) {
    const id = requestId();
    return new Promise((resolve, reject) => {
      let port;
      try {
        port = this.connect();
      } catch (error) {
        error.code = error.code || "PARABOLIC_BRIDGE_UNAVAILABLE";
        reject(error);
        return;
      }

      const timeout = setTimeout(() => {
        this.pending.delete(id);
        const error = new Error("Parabolic did not answer in time.");
        error.code = "PARABOLIC_BRIDGE_TIMEOUT";
        reject(error);
      }, timeoutMilliseconds);
      this.pending.set(id, { resolve, reject, timeout });

      try {
        port.postMessage({
          protocolVersion: NATIVE_PROTOCOL_VERSION,
          requestId: id,
          type,
          payload
        });
      } catch (error) {
        clearTimeout(timeout);
        this.pending.delete(id);
        error.code = error.code || "PARABOLIC_BRIDGE_UNAVAILABLE";
        reject(error);
      }
    });
  }
}

const nativeBridge = new NativeBridge();

function nativeHelloPayload() {
  return {
    extensionId: browser.runtime.id,
    extensionVersion: browser.runtime.getManifest().version,
    protocolVersion: NATIVE_PROTOCOL_VERSION
  };
}

async function pulseNativeBridge() {
  return nativeBridge.request("hello", nativeHelloPayload(), 3000);
}

function isHttpUrl(value) {
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:";
  } catch (_) {
    return false;
  }
}

function normalizedSiteHost(hostname) {
  const labels = String(hostname || "").toLowerCase().split(".").filter(Boolean);
  return labels.length > 2 ? labels.slice(-2).join(".") : labels.join(".");
}

function isSameSite(left, right) {
  try {
    return normalizedSiteHost(new URL(left).hostname) === normalizedSiteHost(new URL(right).hostname);
  } catch (_) {
    return false;
  }
}

function normalizeStablePageUrl(value, depth = 0) {
  if (!isHttpUrl(value) || depth > 2) {
    return "";
  }
  const url = new URL(value);
  url.hash = "";

  if (normalizedSiteHost(url.hostname) === "facebook.com") {
    const redirected = url.searchParams.get("next");
    if (url.pathname.startsWith("/login") && isHttpUrl(redirected)) {
      return normalizeStablePageUrl(redirected, depth + 1);
    }
    url.searchParams.delete("_fb_noscript");
    for (const parameter of ["__cft__", "__tn__", "mibextid", "ref", "refsrc", "rdid", "share_url"]) {
      url.searchParams.delete(parameter);
    }
  } else if (normalizedSiteHost(url.hostname) === "linkedin.com") {
    for (const parameter of ["trackingId", "trk", "lipi", "midToken", "midSig"]) {
      url.searchParams.delete(parameter);
    }
  }
  return url.href;
}

function permalinkScore(value) {
  if (!isHttpUrl(value)) {
    return -1000;
  }
  const url = new URL(value);
  const site = normalizedSiteHost(url.hostname);
  const path = url.pathname.toLowerCase();
  let score = path === "/" ? 0 : 20;

  if (site === "facebook.com") {
    if (/^\/(?:reel|videos)\/\d+/.test(path)
        || /\/videos\/\d+/.test(path)
        || /\/posts\//.test(path)
        || /\/groups\/[^/]+\/(?:posts|permalink)\/\d+/.test(path)
        || path === "/watch/" && url.searchParams.has("v")
        || path === "/permalink.php" && url.searchParams.has("story_fbid")
        || path === "/story.php" && url.searchParams.has("story_fbid")
        || path === "/photo/" && url.searchParams.has("fbid")
        || /^\/share\/(?:v|r)\//.test(path)) {
      score += 200;
    }
    if (path === "/" || path.startsWith("/login") || path === "/watch/") {
      score -= 100;
    }
  } else if (site === "linkedin.com") {
    if (/^\/feed\/update\/urn:li:(?:activity|ugcpost):/i.test(url.pathname)
        || /\/posts\//.test(path)
        || /\/pulse\//.test(path)) {
      score += 200;
    }
    if (path === "/" || path === "/feed/" || path.startsWith("/login")) {
      score -= 100;
    }
  }
  return score;
}

function choosePreferredPageUrl(source, senderTabUrl) {
  const topUrl = normalizeStablePageUrl(senderTabUrl);
  const candidates = [source.pageUrl, source.frameUrl, topUrl]
    .map((value) => normalizeStablePageUrl(value))
    .filter((value, index, values) => value && values.indexOf(value) === index);
  if (candidates.length === 0) {
    return "";
  }

  return candidates
    .map((value, index) => {
      let score = permalinkScore(value);
      if (topUrl && value === topUrl) {
        score += 10;
      } else if (topUrl && !isSameSite(value, topUrl) && permalinkScore(value) < 100) {
        score -= 150;
      }
      return { value, score, index };
    })
    .sort((left, right) => right.score - left.score || left.index - right.index)[0].value;
}

function isManifestUrl(value, kind = "") {
  if (!isHttpUrl(value)) {
    return false;
  }
  const extension = getExtension(value);
  return kind === "hls" || kind === "dash" || extension === "m3u8" || extension === "mpd";
}

function chooseRecentManifest(tabId, pageUrl, frameId) {
  if (!Number.isInteger(tabId)) {
    return null;
  }
  const cutoff = Date.now() - 10 * 60 * 1000;
  const candidates = [...(mediaByTab.get(tabId)?.values() || [])]
    .filter((candidate) => candidate.discoveredAt >= cutoff && isManifestUrl(candidate.url, candidate.kind))
    .map((candidate) => ({
      candidate,
      score: (candidate.source === "network" ? 20 : 0)
        + (Number.isInteger(frameId) && candidate.frameId === frameId ? 50 : 0)
        + (pageUrl && candidate.pageUrl && isSameSite(pageUrl, candidate.pageUrl) ? 30 : 0)
        + candidate.discoveredAt / 1_000_000_000_000
    }))
    .sort((left, right) => right.score - left.score);
  return candidates[0]?.candidate || null;
}

function chooseRecentDirectMedia(tabId, pageUrl, frameId) {
  if (!Number.isInteger(tabId)) {
    return null;
  }
  const cutoff = Date.now() - 10 * 60 * 1000;
  const candidates = [...(mediaByTab.get(tabId)?.values() || [])]
    .filter((candidate) => candidate.discoveredAt >= cutoff
      && candidate.kind === "video"
      && isHttpUrl(candidate.url)
      && !isManifestUrl(candidate.url, candidate.kind))
    .map((candidate) => ({
      candidate,
      score: (candidate.source === "network" ? 20 : 0)
        + (Number.isInteger(frameId) && candidate.frameId === frameId ? 50 : 0)
        + (pageUrl && candidate.pageUrl && isSameSite(pageUrl, candidate.pageUrl) ? 30 : 0)
        + (Number.isFinite(candidate.size) ? Math.min(candidate.size / (1024 * 1024), 40) : 0)
        + candidate.discoveredAt / 1_000_000_000_000
    }))
    .sort((left, right) => right.score - left.score);
  return candidates[0]?.candidate || null;
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
  browser.tabs.sendMessage(tabId, {
    type: "parabolic-media-catalog-updated",
    count: tabMedia.size
  }).catch(() => undefined);
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
        : "Parabolic downloads and diagnostics"
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

function normalizedDownloadPayload(message, sender) {
  const source = message.request || {};
  const tabId = sender.tab?.id ?? Number(message.tabId);
  const pageUrl = choosePreferredPageUrl(source, sender.tab?.url);
  const explicitManifest = isManifestUrl(source.manifestUrl, source.manifestKind)
    ? { url: source.manifestUrl, kind: source.manifestKind }
    : isManifestUrl(source.mediaUrl, source.sourceKind)
      ? { url: source.mediaUrl, kind: source.sourceKind }
      : null;
  const detectedManifest = explicitManifest || chooseRecentManifest(tabId, pageUrl, sender.frameId);
  const mediaUrl = isHttpUrl(source.mediaUrl) && !isManifestUrl(source.mediaUrl, source.sourceKind)
    ? source.mediaUrl
    : "";
  const detectedDirectMedia = mediaUrl
    ? null
    : chooseRecentDirectMedia(tabId, pageUrl, sender.frameId);
  if (!isHttpUrl(pageUrl) && !mediaUrl && !detectedManifest && !detectedDirectMedia) {
    throw new Error("No downloadable page or media URL was provided.");
  }

  // A YouTube watch/short/live URL always represents the video currently
  // shown by the in-player controls. Keeping list/index makes yt-dlp inspect
  // the complete playlist, which delays or stalls both format discovery and
  // one-click downloads. Explicit /playlist URLs remain playlists unless the
  // user enables the compatibility option that trims all playlist parameters.
  const normalizePageUrl = (value) => {
    if (!isHttpUrl(value)) {
      return "";
    }
    return settings.trimPlaylist || isYouTubePlaybackUrl(value)
      ? trimPlaylistFromUrl(value)
      : value;
  };
  const processedPageUrl = normalizePageUrl(pageUrl);
  const processedFrameUrl = normalizePageUrl(source.frameUrl);
  const fallbackPreset = DOWNLOAD_PRESETS.has(settings.quickDownloadPreset)
    ? settings.quickDownloadPreset
    : "best";
  const preset = DOWNLOAD_PRESETS.has(source.preset)
    ? source.preset
    : fallbackPreset;
  const fallbackPriority = DOWNLOAD_PRIORITIES.has(settings.defaultPriority)
    ? settings.defaultPriority
    : "normal";
  const priority = DOWNLOAD_PRIORITIES.has(source.priority)
    ? source.priority
    : fallbackPriority;
  const resolverPreference = RESOLVER_PREFERENCES.has(settings.resolverPreference)
    ? settings.resolverPreference
    : "auto";
  const cobaltAuthScheme = ["none", "api-key", "bearer"].includes(settings.cobaltAuthScheme)
    ? settings.cobaltAuthScheme
    : "none";
  const speedLimitKbps = Number.parseInt(settings.speedLimitKbps, 10);
  return {
    tabId,
    pageUrl: processedPageUrl,
    mediaUrl,
    manifestUrl: detectedManifest?.url || "",
    manifestKind: detectedManifest?.kind || "",
    directFallbackUrl: detectedDirectMedia?.url || "",
    directFallbackKind: detectedDirectMedia?.kind || "",
    userAgent: String(source.userAgent || globalThis.navigator?.userAgent || "").slice(0, 500),
    title: String(source.title || sender.tab?.title || "Media").slice(0, 500),
    preset,
    formatId: String(source.formatId || "").slice(0, 200),
    sourceKind: String(source.sourceKind || "page").slice(0, 40),
    frameUrl: processedFrameUrl,
    priority,
    resolverPreference,
    cobaltEndpoint: isHttpUrl(settings.cobaltEndpoint) ? settings.cobaltEndpoint : "",
    cobaltAuthScheme,
    cobaltAuthToken: cobaltAuthScheme === "none" ? "" : String(settings.cobaltAuthToken || ""),
    speedLimitKbps: Number.isFinite(speedLimitKbps) && speedLimitKbps >= 32
      ? Math.min(speedLimitKbps, 10000000)
      : 0,
    scheduledAt: String(source.scheduledAt || "").slice(0, 80),
    networkStrategy: NETWORK_STRATEGIES.has(settings.networkStrategy)
      ? settings.networkStrategy
      : "balanced",
    authenticationMode: AUTHENTICATION_MODES.has(settings.authenticationMode)
      ? settings.authenticationMode
      : "parabolic",
    proxyMode: PROXY_MODES.has(settings.proxyMode)
      ? settings.proxyMode
      : "parabolic",
    sendPageReferer: settings.sendPageReferer === true
  };
}

async function requestNativeDownload(message, sender) {
  const payload = normalizedDownloadPayload(message, sender);
  try {
    // Exact formats require discovery; quick presets are enqueued immediately.
    const response = await nativeBridge.request("download", payload, 120000);
    const downloadId = response.payload?.downloadId || response.downloadId;
    if (downloadId && Number.isInteger(payload.tabId)) {
      downloadTabs.set(downloadId, payload.tabId);
    }
    return {
      ok: true,
      mode: "native",
      result: response.payload || response
    };
  } catch (error) {
    if (message.allowLegacy === true || settings.fallbackToProtocol) {
      await openParabolicUrl(payload.pageUrl || payload.mediaUrl, payload.tabId);
      return {
        ok: true,
        mode: "legacy",
        warning: "The native bridge is unavailable, so Parabolic was opened in compatibility mode."
      };
    }
    return {
      ok: false,
      error: serializableError(
        error,
        "Install the upcoming Parabolic release to enable background downloads."
      )
    };
  }
}

async function requestNativeFormats(message, sender) {
  const payload = normalizedDownloadPayload(message, sender);
  try {
    const response = await nativeBridge.request("get-formats", payload, 120000);
    return { ok: true, result: response.payload || response };
  } catch (error) {
    return {
      ok: false,
      error: serializableError(error, "Unable to retrieve formats from Parabolic.")
    };
  }
}

async function checkYtdlpUpdate() {
  try {
    const response = await nativeBridge.request("check-ytdlp-update", {}, 30000);
    return { ok: true, result: response.payload || response };
  } catch (error) {
    return { ok: false, error: serializableError(error, "Unable to check for a yt-dlp update.") };
  }
}

async function updateYtdlp() {
  try {
    const response = await nativeBridge.request("update-ytdlp", {}, 180000);
    return { ok: true, result: response.payload || response };
  } catch (error) {
    return { ok: false, error: serializableError(error, "Unable to update yt-dlp.") };
  }
}

async function probeNativeBridge() {
  try {
    const response = await nativeBridge.request("hello", {
      extensionId: browser.runtime.id,
      extensionVersion: browser.runtime.getManifest().version,
      protocolVersion: NATIVE_PROTOCOL_VERSION
    }, 3000);
    return {
      ok: true,
      available: true,
      host: response.payload || response
    };
  } catch (error) {
    return {
      ok: true,
      available: false,
      error: serializableError(
        error,
        "The installed Parabolic version does not include the Firefox bridge yet."
      )
    };
  }
}

async function syncNativeDownloads() {
  try {
    const response = await nativeBridge.request("list-downloads", {}, 5000);
    for (const download of response.payload?.downloads || []) {
      if (download.downloadId && Number.isInteger(download.tabId)) {
        downloadTabs.set(download.downloadId, download.tabId);
      }
    }
    return response.payload?.downloads || [];
  } catch (_) {
    return [];
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
      title: "Download link with Parabolic",
      contexts: ["link", "page", "video", "audio"]
    });
  }
}

async function loadSettings() {
  const synced = await browser.storage.sync.get(DEFAULT_SETTINGS);
  const local = await browser.storage.local.get({ cobaltAuthToken: "" });
  settings = { ...DEFAULT_SETTINGS, ...synced, cobaltAuthToken: local.cobaltAuthToken || "" };
}

function contentSettings() {
  return {
    detectMedia: settings.detectMedia,
    showOverlay: settings.showOverlay,
    quickDownloadPreset: settings.quickDownloadPreset,
    defaultPriority: settings.defaultPriority,
    overlayPosition: settings.overlayPosition
  };
}

async function initializeBackground() {
  await loadSettings();
  await createContextMenu();
  // Start and keep the native host ready before the user's first download click.
  const bridge = await probeNativeBridge();
  if (bridge.available) {
    await syncNativeDownloads();
  }
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
  await initializeBackground();
});

browser.runtime.onStartup.addListener(initializeBackground);

initializeBackground().catch(console.error);

browser.storage.onChanged.addListener(async (changes, namespace) => {
  if (namespace === "local" && changes.cobaltAuthToken) {
    settings.cobaltAuthToken = changes.cobaltAuthToken.newValue || "";
  } else if (namespace !== "sync") {
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
  const tabs = await browser.tabs.query({});
  await Promise.all(tabs.map((tab) => browser.tabs.sendMessage(tab.id, {
    type: "parabolic-settings-updated",
    settings: contentSettings()
  }).catch(() => undefined)));
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
    const tabId = Number(message.tabId ?? sender.tab?.id);
    const candidates = [...(mediaByTab.get(tabId)?.values() || [])]
      .sort((left, right) => right.discoveredAt - left.discoveredAt);
    return { candidates, settings: contentSettings() };
  }

  if (message.type === "get-settings") {
    return { ok: true, settings: contentSettings() };
  }

  if (message.type === "clear-media") {
    const tabId = Number(message.tabId);
    mediaByTab.delete(tabId);
    await updateBadge(tabId);
    return { ok: true };
  }

  if (message.type === "bridge-status") {
    return probeNativeBridge();
  }

  if (message.type === "bridge-pulse") {
    try {
      const response = await pulseNativeBridge();
      return { ok: true, available: true, host: response.payload || response };
    } catch (error) {
      return { ok: false, available: false, error: serializableError(error, "Unable to reactivate the Parabolic bridge.") };
    }
  }

  if (message.type === "native-download") {
    return requestNativeDownload(message, sender);
  }

  if (message.type === "native-formats") {
    return requestNativeFormats(message, sender);
  }

  if (message.type === "native-ytdlp-check") {
    return checkYtdlpUpdate();
  }

  if (message.type === "native-ytdlp-update") {
    return updateYtdlp();
  }

  if (message.type === "native-cancel") {
    try {
      const response = await nativeBridge.request("cancel", {
        downloadId: message.downloadId
      });
      return { ok: true, result: response.payload || response };
    } catch (error) {
      return { ok: false, error: serializableError(error, "Unable to cancel the download.") };
    }
  }

  if (message.type === "native-pause" || message.type === "native-resume") {
    try {
      const response = await nativeBridge.request(
        message.type === "native-pause" ? "pause" : "resume",
        { downloadId: message.downloadId }
      );
      return { ok: true, result: response.payload || response };
    } catch (error) {
      return { ok: false, error: serializableError(error, "Unable to change the download state.") };
    }
  }

  if (message.type === "native-set-priority") {
    try {
      const response = await nativeBridge.request("set-priority", {
        downloadId: message.downloadId,
        priority: message.priority
      });
      return { ok: true, result: response.payload || response };
    } catch (error) {
      return { ok: false, error: serializableError(error, "Unable to change the download priority.") };
    }
  }

  if (message.type === "native-open-folder") {
    try {
      const response = await nativeBridge.request("open-folder", {
        downloadId: message.downloadId
      });
      return { ok: true, result: response.payload || response };
    } catch (error) {
      return { ok: false, error: serializableError(error, "Unable to open the download folder.") };
    }
  }

  if (message.type === "legacy-open" || message.type === "open-parabolic") {
    await openParabolicUrl(message.url, Number(message.tabId ?? sender.tab?.id));
    return { ok: true, mode: "legacy" };
  }

  return undefined;
});

browser.contextMenus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId !== "openParabolicLink") {
    return;
  }
  const response = await requestNativeDownload({
    request: {
      pageUrl: info.pageUrl || tab.url,
      mediaUrl: info.srcUrl || info.linkUrl || "",
      preset: settings.quickDownloadPreset,
      title: tab.title,
      sourceKind: info.mediaType || "context-menu"
    }
  }, { tab });
  if (!response.ok) {
    console.error(response.error?.message);
  }
});

browser.commands.onCommand.addListener(async (command) => {
  if (command === "open-parabolic-current-tab") {
    const [tab] = await browser.tabs.query({ active: true, currentWindow: true });
    if (tab?.url) {
      await requestNativeDownload({
        request: {
          pageUrl: tab.url,
          preset: settings.quickDownloadPreset,
          title: tab.title,
          sourceKind: "keyboard"
        }
      }, { tab });
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
