let activeTab = null;
let currentSettings = { quickDownloadPreset: "best", defaultPriority: "normal" };
let clipboardWatchTimer = null;

function isHttpUrl(value) {
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:";
  } catch (_) {
    return false;
  }
}

function readableSize(bytes) {
  if (!Number.isFinite(bytes) || bytes <= 0) {
    return "";
  }
  const units = ["B", "KB", "MB", "GB"];
  let value = bytes;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }
  return `${value.toFixed(unitIndex > 1 ? 1 : 0)} ${units[unitIndex]}`;
}

function candidateTitle(candidate) {
  if (candidate.label) {
    return candidate.label;
  }
  if (candidate.filename) {
    return candidate.filename;
  }
  if (candidate.kind === "hls") {
    return "HLS playlist";
  }
  if (candidate.kind === "dash") {
    return "DASH manifest";
  }
  return candidate.kind === "audio" ? "Audio stream" : "Video stream";
}

function candidateDetails(candidate) {
  const details = [];
  if (candidate.width && candidate.height) {
    details.push(`${candidate.width}×${candidate.height}`);
  }
  const size = readableSize(candidate.size);
  if (size) {
    details.push(size);
  }
  if (candidate.mime) {
    details.push(candidate.mime.replace(/^application\//, ""));
  }
  if (candidate.streamTypes?.length) {
    details.push(candidate.streamTypes.join(" + "));
  }
  return details;
}

function setStatus(message, kind = "success") {
  const errorState = document.getElementById("errorState");
  const statusState = document.getElementById("statusState");
  if (kind === "error") {
    errorState.textContent = message;
    errorState.hidden = false;
    statusState.hidden = true;
  } else {
    statusState.textContent = message;
    statusState.hidden = false;
    errorState.hidden = true;
  }
}

async function pasteDirectUrl() {
  try {
    const granted = await browser.permissions.request({ permissions: ["clipboardRead"] });
    if (!granted) {
      throw new Error("Clipboard permission was not granted.");
    }
    const value = (await navigator.clipboard.readText()).trim();
    if (!isHttpUrl(value)) {
      throw new Error("The clipboard does not contain an HTTP or HTTPS URL.");
    }
    document.getElementById("directUrlInput").value = value;
  } catch (error) {
    setStatus(error.message || "Firefox could not read the clipboard.", "error");
  }
}

async function downloadDirectUrl() {
  const input = document.getElementById("directUrlInput");
  const value = input.value.trim();
  if (!isHttpUrl(value)) {
    setStatus("Enter a valid HTTP or HTTPS URL.", "error");
    return;
  }
  let title = "Direct download";
  try {
    title = decodeURIComponent(new URL(value).pathname.split("/").filter(Boolean).at(-1) || title);
  } catch (_) {
    // The URL was already validated.
  }
  await startDownload({
    pageUrl: value,
    mediaUrl: value,
    title,
    preset: currentSettings.quickDownloadPreset,
    priority: currentSettings.defaultPriority,
    sourceKind: "direct"
  }, document.getElementById("downloadUrlButton"));
}

async function loadSubscriptions() {
  const response = await browser.runtime.sendMessage({ type: "native-list-subscriptions" });
  const list = document.getElementById("subscriptionList");
  list.replaceChildren();
  if (!response?.ok) {
    return;
  }
  for (const subscription of response.result?.subscriptions || []) {
    const row = document.createElement("div");
    row.className = "subscription-item";
    const title = document.createElement("span");
    title.textContent = subscription.title || subscription.feedUrl;
    title.title = subscription.lastError || subscription.feedUrl;
    const remove = document.createElement("button");
    remove.type = "button";
    remove.className = "remove-subscription";
    remove.textContent = "Remove";
    remove.addEventListener("click", async () => {
      const removed = await browser.runtime.sendMessage({
        type: "native-remove-subscription",
        subscriptionId: subscription.id
      });
      if (removed?.ok) {
        await loadSubscriptions();
      } else {
        setStatus(removed?.error?.message || "Unable to remove the subscription.", "error");
      }
    });
    row.append(title, remove);
    list.append(row);
  }
}

async function addSubscription() {
  const input = document.getElementById("feedUrlInput");
  const feedUrl = input.value.trim();
  if (!isHttpUrl(feedUrl)) {
    setStatus("Enter a valid RSS or Atom feed URL.", "error");
    return;
  }
  const response = await browser.runtime.sendMessage({
    type: "native-add-subscription",
    subscription: {
      feedUrl,
      title: new URL(feedUrl).hostname,
      autoDownload: true,
      downloadLatestOnly: document.getElementById("feedLatestOnly").checked,
      keywordFilter: document.getElementById("feedKeywordInput").value.trim(),
      preset: currentSettings.quickDownloadPreset,
      priority: currentSettings.defaultPriority,
      pollMinutes: 180
    }
  });
  if (!response?.ok) {
    setStatus(response?.error?.message || "Unable to add the subscription.", "error");
    return;
  }
  input.value = "";
  setStatus("RSS subscription saved.");
  await loadSubscriptions();
}

function startClipboardWatcher() {
  if (!currentSettings.watchClipboardInPopup || clipboardWatchTimer) {
    return;
  }
  clipboardWatchTimer = setInterval(async () => {
    try {
      const value = (await navigator.clipboard.readText()).trim();
      const input = document.getElementById("directUrlInput");
      if (isHttpUrl(value) && input.value !== value) {
        input.value = value;
      }
    } catch (_) {
      // Clipboard access may be temporarily unavailable without user focus.
    }
  }, 2500);
}

async function startDownload(request, button) {
  if (button) {
    button.disabled = true;
  }
  setStatus("Sending to Parabolic…");
  try {
    const response = await browser.runtime.sendMessage({
      type: "native-download",
      tabId: activeTab.id,
      request
    });
    if (!response?.ok) {
      throw new Error(response?.error?.message || "Parabolic could not start the download.");
    }
    setStatus(response.mode === "legacy"
      ? response.warning || "Opened in compatibility mode."
      : response.result?.status === "scheduled"
        ? "Download scheduled."
        : "Download started in the background.");
  } catch (error) {
    setStatus(error.message || "The Parabolic bridge is unavailable.", "error");
  } finally {
    if (button) {
      button.disabled = false;
    }
  }
}

function renderCandidate(candidate) {
  const item = document.createElement("article");
  item.className = "media-item";
  const copy = document.createElement("div");
  copy.className = "media-copy";
  const title = document.createElement("div");
  title.className = "media-title";
  title.textContent = candidateTitle(candidate);
  title.title = title.textContent;
  const meta = document.createElement("div");
  meta.className = "media-meta";
  const typePill = document.createElement("span");
  typePill.className = "pill";
  typePill.textContent = candidate.kind || "media";
  meta.append(typePill);
  for (const detail of candidateDetails(candidate)) {
    const detailPill = document.createElement("span");
    detailPill.className = "pill";
    detailPill.textContent = detail;
    meta.append(detailPill);
  }
  const url = document.createElement("div");
  url.className = "media-url";
  url.textContent = candidate.url;
  url.title = candidate.url;
  const button = document.createElement("button");
  button.className = "download-button";
  button.type = "button";
  button.textContent = "Download";
  button.addEventListener("click", () => startDownload({
    pageUrl: activeTab.url,
    mediaUrl: candidate.kind === "youtube" || ["hls", "dash"].includes(candidate.kind) ? "" : candidate.url,
    manifestUrl: ["hls", "dash"].includes(candidate.kind) ? candidate.url : "",
    manifestKind: ["hls", "dash"].includes(candidate.kind) ? candidate.kind : "",
    title: candidateTitle(candidate),
    preset: currentSettings.quickDownloadPreset,
    priority: currentSettings.defaultPriority,
    sourceKind: candidate.kind
  }, button));
  copy.append(title, meta, url);
  item.append(copy, button);
  return item;
}

async function updateBridgeStatus() {
  const dot = document.getElementById("bridgeDot");
  const title = document.getElementById("bridgeTitle");
  const details = document.getElementById("bridgeDetails");
  try {
    const response = await browser.runtime.sendMessage({ type: "bridge-status" });
    if (response?.available) {
      dot.dataset.status = "ready";
      title.textContent = "Parabolic is ready";
      details.textContent = response.host?.appVersion
        ? `Native bridge ${response.host.appVersion}`
        : "Background downloads are available";
    } else {
      dot.dataset.status = "missing";
      title.textContent = "Parabolic update required";
      details.textContent = "The current desktop release has no Firefox bridge";
    }
  } catch (_) {
    dot.dataset.status = "missing";
    title.textContent = "Parabolic bridge unavailable";
    details.textContent = "Install the adapted desktop release when it is ready";
  }
}

async function loadPopup() {
  [activeTab] = await browser.tabs.query({ active: true, currentWindow: true });
  if (!activeTab) {
    return;
  }
  updateBridgeStatus();
  document.getElementById("pageTitle").textContent = activeTab.title || "Current page";
  try {
    document.getElementById("pageHost").textContent = new URL(activeTab.url).hostname;
  } catch (_) {
    document.getElementById("pageHost").textContent = activeTab.url || "";
  }

  const downloadPageButton = document.getElementById("downloadPageButton");
  downloadPageButton.addEventListener("click", () => startDownload({
    pageUrl: activeTab.url,
    title: activeTab.title,
    preset: currentSettings.quickDownloadPreset,
    priority: currentSettings.defaultPriority,
    sourceKind: "page"
  }, downloadPageButton));
  document.getElementById("settingsButton").addEventListener("click", () => {
    browser.runtime.openOptionsPage();
    window.close();
  });
  document.getElementById("pasteUrlButton").addEventListener("click", pasteDirectUrl);
  document.getElementById("downloadUrlButton").addEventListener("click", downloadDirectUrl);
  document.getElementById("subscribeButton").addEventListener("click", addSubscription);
  document.getElementById("checkFeedsButton").addEventListener("click", async () => {
    const response = await browser.runtime.sendMessage({ type: "native-check-subscriptions" });
    if (!response?.ok) {
      setStatus(response?.error?.message || "Unable to check subscriptions.", "error");
      return;
    }
    setStatus(`${response.result?.discoveredItems || 0} new RSS item(s) found.`);
    await loadSubscriptions();
  });
  document.getElementById("clearButton").addEventListener("click", async () => {
    await browser.runtime.sendMessage({ type: "clear-media", tabId: activeTab.id });
    document.getElementById("mediaList").replaceChildren();
    document.getElementById("mediaCount").textContent = "Detected sources (0)";
    document.getElementById("emptyState").hidden = false;
  });

  const response = await browser.runtime.sendMessage({ type: "get-media", tabId: activeTab.id });
  currentSettings = { ...currentSettings, ...(response?.settings || {}) };
  startClipboardWatcher();
  await loadSubscriptions();
  const candidates = response?.candidates || [];
  document.getElementById("mediaCount").textContent = `Detected sources (${candidates.length})`;
  document.getElementById("emptyState").hidden = candidates.length !== 0;
  const mediaList = document.getElementById("mediaList");
  for (const candidate of candidates) {
    mediaList.append(renderCandidate(candidate));
  }
}

loadPopup().catch((error) => {
  setStatus(error.message || "Unable to inspect this tab.", "error");
});

window.addEventListener("unload", () => {
  if (clipboardWatchTimer) {
    clearInterval(clipboardWatchTimer);
  }
});
