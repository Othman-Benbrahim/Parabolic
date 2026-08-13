let activeTab = null;

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

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function openInParabolic(url, button) {
  const errorState = document.getElementById("errorState");
  const statusState = document.getElementById("statusState");
  errorState.hidden = true;
  statusState.textContent = "Sending to Parabolic…";
  statusState.hidden = false;
  if (button) {
    button.disabled = true;
  }
  try {
    // Give Firefox enough time to paint the status before switching to the desktop app.
    await delay(350);
    await browser.runtime.sendMessage({
      type: "open-parabolic",
      tabId: activeTab.id,
      url
    });
    statusState.textContent = "Sent to Parabolic. You can keep browsing in Firefox.";
  } catch (error) {
    errorState.textContent = error.message || "Unable to open Parabolic.";
    errorState.hidden = false;
    statusState.hidden = true;
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
  button.addEventListener("click", () => openInParabolic(candidate.url, button));

  copy.append(title, meta, url);
  item.append(copy, button);
  return item;
}

async function loadPopup() {
  [activeTab] = await browser.tabs.query({ active: true, currentWindow: true });
  if (!activeTab) {
    return;
  }

  document.getElementById("pageTitle").textContent = activeTab.title || "Current page";
  try {
    document.getElementById("pageHost").textContent = new URL(activeTab.url).hostname;
  } catch (_) {
    document.getElementById("pageHost").textContent = activeTab.url || "";
  }

  const downloadPageButton = document.getElementById("downloadPageButton");
  downloadPageButton.addEventListener("click", () => {
    openInParabolic(activeTab.url, downloadPageButton);
  });
  document.getElementById("settingsButton").addEventListener("click", () => {
    browser.runtime.openOptionsPage();
    window.close();
  });
  document.getElementById("clearButton").addEventListener("click", async () => {
    await browser.runtime.sendMessage({ type: "clear-media", tabId: activeTab.id });
    document.getElementById("mediaList").replaceChildren();
    document.getElementById("mediaCount").textContent = "Detected sources (0)";
    document.getElementById("emptyState").hidden = false;
  });

  const response = await browser.runtime.sendMessage({ type: "get-media", tabId: activeTab.id });
  const candidates = response?.candidates || [];
  document.getElementById("mediaCount").textContent = `Detected sources (${candidates.length})`;
  document.getElementById("emptyState").hidden = candidates.length !== 0;

  const mediaList = document.getElementById("mediaList");
  for (const candidate of candidates) {
    mediaList.append(renderCandidate(candidate));
  }
}

loadPopup().catch((error) => {
  const errorState = document.getElementById("errorState");
  errorState.textContent = error.message || "Unable to inspect this tab.";
  errorState.hidden = false;
});
