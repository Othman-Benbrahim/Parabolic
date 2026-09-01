(() => {
  const ADDON_VERSION = browser.runtime.getManifest().version;
  const DEFAULT_SETTINGS = {
    detectMedia: true,
    showOverlay: true,
    quickDownloadPreset: "best",
    defaultPriority: "normal",
    overlayPosition: "top-right"
  };
  const PRESETS = [
    { id: "best", label: "Best quality", detail: "Video + audio" },
    { id: "1080", label: "Up to 1080p", detail: "Video + audio" },
    { id: "720", label: "Up to 720p", detail: "Video + audio" },
    { id: "480", label: "Up to 480p", detail: "Video + audio" },
    { id: "audio", label: "Audio only", detail: "Best audio" }
  ];
  const MIN_VIDEO_WIDTH = 240;
  const MIN_VIDEO_HEIGHT = 120;

  let settings = { ...DEFAULT_SETTINGS };
  let primaryMedia = null;
  let host = null;
  let shadow = null;
  let menu = null;
  let mainButton = null;
  let menuButton = null;
  let toast = null;
  let toastMessage = null;
  let toastActions = null;
  let bridgeIndicator = null;
  let exactFormatsContainer = null;
  let positionFrame = null;
  let scanTimer = null;
  let hideToastTimer = null;
  let downloadWatchdogTimer = null;
  let currentDownloadId = null;
  let currentDownloadActive = false;
  let overlayDismissed = false;
  const acknowledgedDownloads = new Set();
  const earlyDownloadStates = new Map();

  // Firefox does not replace content scripts that are already running when a
  // temporary add-on is reloaded. Remove any overlay left by the previous
  // instance so a manually injected/reloaded build cannot leave the old UI on
  // top of the current one. A normal page reload also clears that old script.
  for (const staleOverlay of document.querySelectorAll("[data-parabolic-overlay]")) {
    staleOverlay.remove();
  }

  const overlayCss = `
    :host {
      all: initial;
      position: fixed;
      z-index: 2147483647;
      display: none;
      color-scheme: light dark;
      font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
      font-size: 13px;
      line-height: 1.35;
      text-align: left;
      pointer-events: auto;
    }
    * { box-sizing: border-box; }
    button { font: inherit; }
    .shell { position: relative; filter: drop-shadow(0 5px 16px rgba(0, 0, 0, .28)); }
    .button-group {
      display: flex;
      overflow: hidden;
      border: 1px solid rgba(255, 255, 255, .42);
      border-radius: 9px;
      background: #e63a3f;
    }
    .quick, .toggle {
      min-height: 36px;
      border: 0;
      background: #e63a3f;
      color: #fff;
      cursor: pointer;
    }
    .quick {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 12px;
      font-weight: 700;
      letter-spacing: .01em;
    }
    .quick::before {
      content: "↓";
      display: grid;
      place-items: center;
      width: 19px;
      height: 19px;
      border: 2px solid currentColor;
      border-radius: 50%;
      font-size: 14px;
      line-height: 1;
    }
    .toggle {
      width: 34px;
      border-left: 1px solid rgba(255, 255, 255, .36);
      font-size: 12px;
    }
    .close {
      position: absolute;
      top: -7px;
      right: -7px;
      z-index: 3;
      display: grid;
      place-items: center;
      width: 20px;
      height: 20px;
      min-height: 20px;
      padding: 0;
      border: 1px solid rgba(255, 255, 255, .72);
      border-radius: 50%;
      background: #b5262b;
      color: #fff;
      cursor: pointer;
      box-shadow: 0 2px 7px rgba(0, 0, 0, .35);
      font-size: 15px;
      font-weight: 800;
      line-height: 1;
    }    .quick:hover, .toggle:hover, .close:hover,
    .quick:focus-visible, .toggle:focus-visible, .close:focus-visible { background: #c92f34; }
    .quick:focus-visible, .toggle:focus-visible, .close:focus-visible, .menu-item:focus-visible, .link-button:focus-visible, .shortcut:focus-visible {
      outline: 2px solid #fff;
      outline-offset: -3px;
    }
    .quick:disabled, .toggle:disabled { cursor: wait; opacity: .75; }
    .shortcut-grid {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 6px;
      padding: 2px 4px 8px;
    }
    .shortcut {
      min-height: 34px;
      padding: 6px 5px;
      border: 1px solid #e63a3f;
      border-radius: 8px;
      background: #e63a3f;
      color: #fff;
      cursor: pointer;
      font-size: 10px;
      font-weight: 750;
      text-align: center;
    }
    .shortcut:hover { background: #c92f34; border-color: #c92f34; }
    .menu {
      position: absolute;
      top: calc(100% + 8px);
      right: 0;
      width: 286px;
      max-height: min(430px, calc(100vh - 70px));
      overflow: auto;
      padding: 8px;
      border: 1px solid #d8d8dc;
      border-radius: 12px;
      background: #fff;
      color: #202124;
      box-shadow: 0 14px 38px rgba(0, 0, 0, .3);
    }
    .menu.above { top: auto; bottom: calc(100% + 8px); }
    .menu[hidden] { display: none; }
    .menu-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      padding: 5px 7px 8px;
      font-weight: 750;
    }
    .bridge {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      color: #68686f;
      font-size: 10px;
      font-weight: 650;
    }
    .bridge::before { content: ""; width: 7px; height: 7px; border-radius: 50%; background: #a3a3a8; }
    .bridge[data-status="ready"]::before { background: #2d9d58; }
    .bridge[data-status="missing"]::before { background: #d18a12; }
    .menu-item {
      display: grid;
      grid-template-columns: 1fr auto;
      width: 100%;
      gap: 10px;
      padding: 9px 8px;
      border: 0;
      border-radius: 8px;
      background: transparent;
      color: inherit;
      cursor: pointer;
      text-align: left;
    }
    .menu-item:hover { background: #f0f0f2; }
    .menu-item strong { font-size: 12px; font-weight: 700; }
    .menu-item span { align-self: center; color: #74747b; font-size: 10px; }
    .menu-item.selected::after { content: "Default"; align-self: center; color: #e63a3f; font-size: 9px; font-weight: 800; text-transform: uppercase; }
    .divider { height: 1px; margin: 6px 4px; background: #e7e7e9; }
    .link-button {
      width: 100%;
      padding: 8px;
      border: 0;
      border-radius: 8px;
      background: transparent;
      color: #b5262b;
      cursor: pointer;
      font-size: 11px;
      font-weight: 700;
      text-align: left;
    }
    .link-button:hover { background: #fff1f1; }
    .formats-title { padding: 7px 8px 3px; color: #68686f; font-size: 10px; font-weight: 800; text-transform: uppercase; }
    .toast {
      position: absolute;
      top: calc(100% + 8px);
      right: 0;
      width: max-content;
      max-width: 320px;
      padding: 9px 11px;
      border-radius: 9px;
      background: #202124;
      color: #fff;
      box-shadow: 0 8px 24px rgba(0, 0, 0, .34);
      font-size: 11px;
      font-weight: 650;
    }
    .toast-message { display: block; }
    .toast-actions { display: flex; gap: 6px; margin-top: 7px; }
    .toast-actions:empty { display: none; }
    .toast-action {
      padding: 5px 7px;
      border: 1px solid rgba(255, 255, 255, .45);
      border-radius: 6px;
      background: transparent;
      color: inherit;
      cursor: pointer;
      font-size: 10px;
      font-weight: 750;
    }
    .toast-action:hover { background: rgba(255, 255, 255, .14); }
    .toast.above { top: auto; bottom: calc(100% + 8px); }
    .toast[data-kind="error"] { background: #9e2228; }
    .toast[data-kind="success"] { background: #237a43; }
    .toast[hidden] { display: none; }
    @media (prefers-color-scheme: dark) {
      .menu { border-color: #48484f; background: #242529; color: #f4f4f5; }
      .menu-item:hover { background: #35363b; }
      .menu-item span, .bridge, .formats-title { color: #b9b9c0; }
      .divider { background: #414248; }
      .link-button { color: #ff8e92; }
      .link-button:hover { background: #41282b; }
    }
  `;

  function normalizeMediaUrl(value) {
    if (!value || value.startsWith("blob:") || value.startsWith("data:")) {
      return "";
    }
    try {
      const url = new URL(value, document.baseURI);
      return ["http:", "https:"].includes(url.protocol) ? url.href : "";
    } catch (_) {
      return "";
    }
  }

  function mediaScore(element) {
    const rect = element.getBoundingClientRect();
    if (rect.width < MIN_VIDEO_WIDTH || rect.height < MIN_VIDEO_HEIGHT
        || rect.bottom <= 0 || rect.right <= 0
        || rect.top >= innerHeight || rect.left >= innerWidth) {
      return -1;
    }
    const visibleWidth = Math.min(rect.right, innerWidth) - Math.max(rect.left, 0);
    const visibleHeight = Math.min(rect.bottom, innerHeight) - Math.max(rect.top, 0);
    const playbackBonus = element.paused ? 0 : 1_000_000;
    return Math.max(0, visibleWidth) * Math.max(0, visibleHeight) + playbackBonus;
  }

  function findPrimaryMedia() {
    let best = null;
    let bestScore = -1;
    for (const element of document.querySelectorAll("video")) {
      const score = mediaScore(element);
      if (score > bestScore) {
        best = element;
        bestScore = score;
      }
    }
    return best;
  }

  function permalinkScore(value) {
    const url = normalizeMediaUrl(value);
    if (!url) {
      return -1;
    }
    const parsed = new URL(url);
    const path = parsed.pathname.toLowerCase();
    if (/facebook\.com$/i.test(parsed.hostname)) {
      if (/^\/(?:reel|videos)\/\d+/.test(path)
          || /\/videos\/\d+/.test(path)
          || /\/posts\//.test(path)
          || /\/groups\/[^/]+\/(?:posts|permalink)\/\d+/.test(path)
          || (path === "/watch/" && parsed.searchParams.has("v"))
          || (path === "/permalink.php" && parsed.searchParams.has("story_fbid"))
          || (path === "/story.php" && parsed.searchParams.has("story_fbid"))
          || (path === "/photo/" && parsed.searchParams.has("fbid"))
          || /^\/share\/(?:v|r)\//.test(path)) {
        return 300;
      }
    }
    if (/linkedin\.com$/i.test(parsed.hostname)) {
      if (/^\/feed\/update\/urn:li:(?:activity|ugcpost):/i.test(parsed.pathname)
          || /\/posts\//.test(path)
          || /\/pulse\//.test(path)) {
        return 300;
      }
    }
    return path === "/" ? 0 : 20;
  }

  function facebookVideoIdCandidates(container) {
    if (!container || !/facebook\.com$/i.test(location.hostname)) {
      return [];
    }
    const candidates = [];
    for (const attribute of ["data-video-id", "data-videoid", "data-store", "data-ft"]) {
      const value = container.getAttribute?.(attribute) || "";
      const match = value.match(/(?:video_?id|videoID)?[\\\"':=\s]+(\d{5,})/i)
        || (attribute.includes("video") ? value.match(/^(\d{5,})$/) : null);
      if (match?.[1]) {
        candidates.push(`https://www.facebook.com/watch/?v=${match[1]}`);
      }
    }

    // Facebook often keeps the post id only in escaped JSON attached to the
    // player/article. Limit the inspected markup so a large feed cannot stall.
    const markup = String(container.outerHTML || "").slice(0, 1_500_000);
    const patterns = [
      /data-video-id=["'](\d{5,})["']/gi,
      /(?:video_id|videoID)(?:\\?"|')?\s*[:=]\s*(?:\\?"|')?(\d{5,})/gi,
      /(?:watch\/\?v=|\/videos\/)(\d{5,})/gi
    ];
    for (const pattern of patterns) {
      for (const match of markup.matchAll(pattern)) {
        candidates.push(`https://www.facebook.com/watch/?v=${match[1]}`);
        if (candidates.length >= 12) {
          return candidates;
        }
      }
    }
    return candidates;
  }

  function findMediaPermalink() {
    const candidates = [
      location.href,
      document.querySelector('link[rel="canonical"]')?.href,
      document.querySelector('meta[property="og:url"]')?.content
    ];

    const containers = [];
    const seenContainers = new Set();
    const addContainer = (container) => {
      if (container && !seenContainers.has(container)) {
        seenContainers.add(container);
        containers.push(container);
      }
    };
    addContainer(primaryMedia?.closest?.('[role="article"]'));
    addContainer(primaryMedia?.closest?.('[role="dialog"]'));
    addContainer(primaryMedia?.closest?.('[data-pagelet*="FeedUnit"], [data-pagelet*="Reels"]'));
    let container = primaryMedia;
    for (let depth = 0; container && depth < 16; depth += 1, container = container.parentElement) {
      addContainer(container);
    }

    for (const candidateContainer of containers) {
      let anchorCount = 0;
      for (const anchor of candidateContainer.querySelectorAll?.("a[href]") || []) {
        if (permalinkScore(anchor.href) >= 300) {
          candidates.push(anchor.href);
        }
        anchorCount += 1;
        if (anchorCount >= 300) {
          break;
        }
      }
      candidates.push(...facebookVideoIdCandidates(candidateContainer));
      const activityUrn = candidateContainer.getAttribute?.("data-urn")
        || candidateContainer.getAttribute?.("data-activity-urn");
      if (/^urn:li:(?:activity|ugcpost):/i.test(activityUrn || "")) {
        candidates.push(`https://www.linkedin.com/feed/update/${activityUrn}`);
      }
    }

    return candidates
      .map((value, index) => ({ value: normalizeMediaUrl(value), score: permalinkScore(value), index }))
      .filter((candidate) => candidate.value)
      .sort((left, right) => right.score - left.score || left.index - right.index)[0]?.value
      || location.href;
  }

  function buildRequest(preset, formatId = "", scheduledAt = "") {
    const mediaUrl = normalizeMediaUrl(primaryMedia?.currentSrc || primaryMedia?.src);
    return {
      pageUrl: findMediaPermalink(),
      frameUrl: location.href,
      mediaUrl,
      userAgent: navigator.userAgent,
      title: primaryMedia?.getAttribute("title")
        || primaryMedia?.getAttribute("aria-label")
        || document.title
        || "Media",
      preset,
      formatId,
      scheduledAt,
      priority: settings.defaultPriority,
      sourceKind: mediaUrl ? "html5" : "page"
    };
  }

  function setBusy(isBusy) {
    if (mainButton) {
      mainButton.disabled = isBusy;
    }
    if (menuButton) {
      menuButton.disabled = isBusy;
    }
  }

  function showToast(message, kind = "info", persistent = false, action = "") {
    if (!toast) {
      return;
    }
    clearTimeout(hideToastTimer);
    toastMessage.textContent = message;
    toastActions.replaceChildren();
    if (action === "cancel" && currentDownloadId) {
      toastActions.append(createToastAction("Cancel", cancelCurrentDownload));
    }
    if (action === "open-folder" && currentDownloadId) {
      toastActions.append(createToastAction("Open folder", openCurrentDownloadFolder));
    }
    if (action === "update-ytdlp") {
      toastActions.append(createToastAction("Update yt-dlp", updateYtdlp));
    }
    toast.dataset.kind = kind;
    toast.hidden = false;
    toast.classList.toggle("above", shouldOpenAbove());
    if (!persistent) {
      hideToastTimer = setTimeout(() => {
        toast.hidden = true;
      }, kind === "error" ? 7000 : 4000);
    }
  }

  function createToastAction(label, handler) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "toast-action";
    button.textContent = label;
    button.addEventListener("click", handler);
    return button;
  }

  async function cancelCurrentDownload() {
    if (!currentDownloadId) {
      return;
    }
    showToast("Cancelling download…", "info", true);
    clearDownloadWatchdog();
    const response = await browser.runtime.sendMessage({
      type: "native-cancel",
      downloadId: currentDownloadId
    });
    if (!response?.ok) {
      showToast(response?.error?.message || "Unable to cancel the download.", "error", true, "cancel");
    }
  }

  function clearDownloadWatchdog() {
    if (downloadWatchdogTimer) {
      clearTimeout(downloadWatchdogTimer);
      downloadWatchdogTimer = null;
    }
  }

  function armDownloadWatchdog(downloadId, timeoutMilliseconds = 30000) {
    clearDownloadWatchdog();
    downloadWatchdogTimer = setTimeout(() => {
      if (currentDownloadActive && (!downloadId || currentDownloadId === downloadId)) {
        const stalledDownloadId = currentDownloadId;
        currentDownloadActive = false;
        currentDownloadId = null;
        if (stalledDownloadId) {
          browser.runtime.sendMessage({
            type: "native-cancel",
            downloadId: stalledDownloadId
          }).catch(() => undefined);
        }
        showToast(
          "No new yt-dlp output was received for 30 seconds. The stalled download was stopped.",
          "error",
          true
        );
      }
      downloadWatchdogTimer = null;
    }, timeoutMilliseconds);
  }

  async function openCurrentDownloadFolder() {
    if (!currentDownloadId) {
      return;
    }
    const response = await browser.runtime.sendMessage({
      type: "native-open-folder",
      downloadId: currentDownloadId
    });
    if (!response?.ok) {
      showToast(response?.error?.message || "Unable to open the download folder.", "error", true, "open-folder");
    } else {
      toast.hidden = true;
    }
  }

  async function startDownload(preset, formatId = "", scheduledAt = "", requestOverride = null) {
    if (!primaryMedia) {
      showToast("No active video was found.", "error");
      return;
    }
    if (currentDownloadActive) {
      showToast("A download is already active.", "info", true, "cancel");
      return;
    }
    setBusy(true);
    showToast("Sending to Parabolic…", "info", true);
    try {
      const response = await browser.runtime.sendMessage({
        type: "native-download",
        request: requestOverride || buildRequest(preset, formatId, scheduledAt)
      });
      if (!response?.ok) {
        throw new Error(response?.error?.message || "Parabolic could not start the download.");
      }
      const downloadId = response.result?.downloadId || null;
      currentDownloadId = downloadId;
      if (downloadId) {
        acknowledgedDownloads.add(downloadId);
      }
      menu.hidden = true;
      if (response.mode === "legacy") {
        clearDownloadWatchdog();
        showToast(response.warning || "Opened Parabolic in compatibility mode.", "info");
      } else if (downloadId && earlyDownloadStates.has(downloadId)) {
        const earlyStatus = earlyDownloadStates.get(downloadId);
        earlyDownloadStates.delete(downloadId);
        const isTerminal = ["completed", "failed", "cancelled"].includes(earlyStatus);
        currentDownloadActive = !isTerminal;
        if (isTerminal) {
          acknowledgedDownloads.delete(downloadId);
        }
        if (["failed", "cancelled"].includes(earlyStatus)) {
          currentDownloadId = null;
        }
      } else {
        currentDownloadActive = true;
        if (response.result?.status === "scheduled") {
          showToast("Download scheduled.", "success", true, "cancel");
        } else {
          armDownloadWatchdog(downloadId);
          showToast(
            response.result?.status === "queued" ? "Download queued…" : "Preparing download…",
            "info",
            true,
            "cancel"
          );
        }
      }
    } catch (error) {
      clearDownloadWatchdog();
      currentDownloadId = null;
      currentDownloadActive = false;
      showToast(
        error.message || "Install the upcoming Parabolic release to enable background downloads.",
        "error",
        true
      );
    } finally {
      setBusy(false);
    }
  }

  function startDirectDownload() {
    const value = window.prompt("Direct HTTP/HTTPS URL", "");
    if (value === null) {
      return;
    }
    const directUrl = normalizeMediaUrl(value.trim());
    if (!directUrl) {
      showToast("Enter a valid HTTP or HTTPS URL.", "error");
      return;
    }
    let title = "Direct download";
    try {
      title = decodeURIComponent(new URL(directUrl).pathname.split("/").filter(Boolean).pop() || title);
    } catch (_) {
      // normalizeMediaUrl already validated the URL.
    }
    menu.hidden = true;
    menuButton.setAttribute("aria-expanded", "false");
    startDownload(settings.quickDownloadPreset, "", "", {
      pageUrl: directUrl,
      mediaUrl: directUrl,
      title,
      preset: settings.quickDownloadPreset,
      priority: settings.defaultPriority,
      sourceKind: "direct"
    });
  }

  async function addRssSubscription() {
    const value = window.prompt(
      "URL de cha\u00eene YouTube, playlist ou flux RSS/Atom",
      location.href
    );
    if (value === null) {
      return;
    }
    const sourceUrl = normalizeMediaUrl(value.trim());
    if (!sourceUrl) {
      showToast("Entrez une URL HTTP/HTTPS valide.", "error");
      return;
    }
    menu.hidden = true;
    menuButton.setAttribute("aria-expanded", "false");
    showToast("Ajout de l\u2019abonnement RSS\u2026", "info", true);
    try {
      const response = await browser.runtime.sendMessage({
        type: "native-add-subscription",
        subscription: {
          feedUrl: sourceUrl,
          title: new URL(sourceUrl).hostname,
          autoDownload: true,
          downloadLatestOnly: true,
          keywordFilter: "",
          preset: settings.quickDownloadPreset,
          priority: settings.defaultPriority,
          pollMinutes: 180
        }
      });
      if (!response?.ok) {
        throw new Error(response?.error?.message || "Impossible d\u2019ajouter l\u2019abonnement.");
      }
      showToast("Abonnement RSS ajout\u00e9.", "success");
    } catch (error) {
      showToast(error.message || "Impossible d\u2019ajouter l\u2019abonnement.", "error", true);
    }
  }
  async function openSettings() {
    menu.hidden = true;
    menuButton.setAttribute("aria-expanded", "false");
    try {
      const response = await browser.runtime.sendMessage({ type: "open-options-page" });
      if (!response?.ok) {
        throw new Error(response?.error?.message || "Unable to open settings.");
      }
    } catch (error) {
      showToast(error.message || "Unable to open settings.", "error");
    }
  }
  function scheduleDownload() {
    const initial = new Date(Date.now() + 60 * 60 * 1000);
    initial.setSeconds(0, 0);
    const localInitial = new Date(initial.getTime() - initial.getTimezoneOffset() * 60000)
      .toISOString()
      .slice(0, 16);
    const value = window.prompt(
      "Start date and time (YYYY-MM-DDTHH:MM)",
      localInitial
    );
    if (value === null) {
      return;
    }
    const scheduled = new Date(value);
    if (!Number.isFinite(scheduled.getTime()) || scheduled.getTime() <= Date.now()) {
      showToast("Choose a valid future date and time.", "error");
      return;
    }
    startDownload(settings.quickDownloadPreset, "", scheduled.toISOString());
  }

  function presetButton(preset) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "menu-item";
    if (settings.quickDownloadPreset === preset.id) {
      button.classList.add("selected");
    }
    const label = document.createElement("strong");
    label.textContent = preset.label;
    const detail = document.createElement("span");
    detail.textContent = preset.detail;
    button.append(label, detail);
    button.addEventListener("click", () => startDownload(preset.id));
    return button;
  }

  function formatButton(format) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "menu-item";
    const label = document.createElement("strong");
    label.textContent = format.label || format.resolution || format.id || "Media format";
    const detail = document.createElement("span");
    detail.textContent = [format.ext, format.filesizeLabel, format.note].filter(Boolean).join(" · ");
    button.append(label, detail);
    button.addEventListener("click", () => startDownload("best", String(format.id || "")));
    return button;
  }

  async function loadExactFormats() {
    exactFormatsContainer.replaceChildren();
    const loading = document.createElement("div");
    loading.className = "formats-title";
    loading.textContent = "Loading formats…";
    exactFormatsContainer.append(loading);
    try {
      const response = await browser.runtime.sendMessage({
        type: "native-formats",
        request: buildRequest("best")
      });
      if (!response?.ok) {
        throw new Error(response?.error?.message || "Formats are unavailable.");
      }
      const formats = Array.isArray(response.result?.formats) ? response.result.formats : [];
      exactFormatsContainer.replaceChildren();
      const title = document.createElement("div");
      title.className = "formats-title";
      title.textContent = formats.length ? "Available formats" : "No separate formats returned";
      exactFormatsContainer.append(title);
      for (const format of formats.slice(0, 20)) {
        exactFormatsContainer.append(formatButton(format));
      }
    } catch (error) {
      exactFormatsContainer.replaceChildren();
      const label = document.createElement("div");
      label.className = "formats-title";
      label.textContent = error.message || "The Parabolic bridge is required for exact formats.";
      exactFormatsContainer.append(label);
    }
  }

  async function updateBridgeStatus() {
    bridgeIndicator.dataset.status = "checking";
    bridgeIndicator.textContent = "Checking app";
    try {
      const response = await browser.runtime.sendMessage({ type: "bridge-status" });
      if (response?.available) {
        bridgeIndicator.dataset.status = "ready";
        const appVersion = response.host?.appVersion || response.appVersion || "";
        bridgeIndicator.textContent = appVersion ? `App ${appVersion}` : "App ready";
      } else {
        bridgeIndicator.dataset.status = "missing";
        bridgeIndicator.textContent = "App update required";
      }
    } catch (_) {
      bridgeIndicator.dataset.status = "missing";
      bridgeIndicator.textContent = "App update required";
    }
  }

  async function openLegacyMode() {
    menu.hidden = true;
    showToast("Opening Parabolic compatibility mode…", "info", true);
    try {
      await browser.runtime.sendMessage({
        type: "legacy-open",
        url: location.href
      });
    } catch (error) {
      showToast(error.message || "Unable to open Parabolic.", "error");
    }
  }

  async function updateYtdlp() {
    menu.hidden = true;
    menuButton.setAttribute("aria-expanded", "false");
    showToast("Checking for a yt-dlp update…", "info", true);
    try {
      const check = await browser.runtime.sendMessage({ type: "native-ytdlp-check" });
      if (!check?.ok) {
        throw new Error(check?.error?.message || "Unable to check for a yt-dlp update.");
      }
      if (!check.result?.updateAvailable) {
        showToast(check.result?.message || "yt-dlp is already up to date.", "success");
        return;
      }
      showToast(`Updating yt-dlp to ${check.result.latestVersion}…`, "info", true);
      const update = await browser.runtime.sendMessage({ type: "native-ytdlp-update" });
      if (!update?.ok) {
        throw new Error(update?.error?.message || "Unable to update yt-dlp.");
      }
      showToast(update.result?.message || "yt-dlp was updated successfully.", "success");
    } catch (error) {
      showToast(error.message || "Unable to update yt-dlp.", "error", true, "update-ytdlp");
    }
  }

  function shouldOpenAbove() {
    if (!primaryMedia) {
      return false;
    }
    const rect = primaryMedia.getBoundingClientRect();
    return innerHeight - rect.top < 470 && rect.bottom > 470;
  }

  function createOverlay() {
    if (host || !document.documentElement) {
      return;
    }
    host = document.createElement("div");
    host.setAttribute("data-parabolic-overlay", "");
    host.setAttribute("data-parabolic-version", ADDON_VERSION);
    shadow = host.attachShadow({ mode: "closed" });

    const style = document.createElement("style");
    style.textContent = overlayCss;
    const shell = document.createElement("div");
    shell.className = "shell";
    const group = document.createElement("div");
    group.className = "button-group";

    const closeButton = document.createElement("button");
    closeButton.type = "button";
    closeButton.className = "close";
    closeButton.textContent = "\u00d7";
    closeButton.title = "Masquer Parabolic sur cette page";
    closeButton.setAttribute("aria-label", "Masquer Parabolic sur cette page");
    closeButton.addEventListener("click", () => {
      overlayDismissed = true;
      host.style.display = "none";
    });
    mainButton = document.createElement("button");
    mainButton.type = "button";
    mainButton.className = "quick";
    mainButton.textContent = "Download video";
    mainButton.title = "Quick download with your default quality";
    mainButton.addEventListener("click", () => startDownload(settings.quickDownloadPreset));

    menuButton = document.createElement("button");
    menuButton.type = "button";
    menuButton.className = "toggle";
    menuButton.textContent = "▾";
    menuButton.title = "Choose quality";
    menuButton.setAttribute("aria-label", "Choose download quality");
    menuButton.setAttribute("aria-expanded", "false");

    menu = document.createElement("div");
    menu.className = "menu";
    menu.hidden = true;
    const header = document.createElement("div");
    header.className = "menu-header";
    const headerTitle = document.createElement("span");
    headerTitle.textContent = `Download with Parabolic · v${ADDON_VERSION}`;
    bridgeIndicator = document.createElement("span");
    bridgeIndicator.className = "bridge";
    bridgeIndicator.dataset.status = "checking";
    bridgeIndicator.textContent = "Checking app";
    header.append(headerTitle, bridgeIndicator);
    menu.append(header);
    const shortcutGrid = document.createElement("div");
    shortcutGrid.className = "shortcut-grid";

    const directButton = document.createElement("button");
    directButton.type = "button";
    directButton.className = "shortcut";
    directButton.textContent = "T\u00e9l\u00e9chargement direct";
    directButton.title = "Download a direct HTTP or HTTPS URL";
    directButton.addEventListener("click", startDirectDownload);

    const rssButton = document.createElement("button");
    rssButton.type = "button";
    rssButton.className = "shortcut";
    rssButton.textContent = "RSS";
    rssButton.title = "Add an RSS or Atom subscription";
    rssButton.addEventListener("click", addRssSubscription);

    const settingsButton = document.createElement("button");
    settingsButton.type = "button";
    settingsButton.className = "shortcut";
    settingsButton.textContent = "Param\u00e8tres";
    settingsButton.title = "Open Parabolic add-on settings";
    settingsButton.addEventListener("click", openSettings);

    shortcutGrid.append(directButton, rssButton, settingsButton);
    menu.append(shortcutGrid);
    for (const preset of PRESETS) {
      menu.append(presetButton(preset));
    }

    const divider = document.createElement("div");
    divider.className = "divider";
    menu.append(divider);
    const exactFormatsButton = document.createElement("button");
    exactFormatsButton.type = "button";
    exactFormatsButton.className = "link-button";
    exactFormatsButton.textContent = "Load exact formats and sizes";
    exactFormatsButton.addEventListener("click", loadExactFormats);
    menu.append(exactFormatsButton);
    const scheduleButton = document.createElement("button");
    scheduleButton.type = "button";
    scheduleButton.className = "link-button";
    scheduleButton.textContent = "Schedule download…";
    scheduleButton.addEventListener("click", scheduleDownload);
    menu.append(scheduleButton);
    exactFormatsContainer = document.createElement("div");
    menu.append(exactFormatsContainer);
    const legacyButton = document.createElement("button");
    legacyButton.type = "button";
    legacyButton.className = "link-button";
    legacyButton.textContent = "Open installed Parabolic (compatibility mode)";
    legacyButton.addEventListener("click", openLegacyMode);
    const ytdlpUpdateButton = document.createElement("button");
    ytdlpUpdateButton.type = "button";
    ytdlpUpdateButton.className = "link-button";
    ytdlpUpdateButton.textContent = "Check and update yt-dlp";
    ytdlpUpdateButton.addEventListener("click", updateYtdlp);
    menu.append(ytdlpUpdateButton);
    menu.append(legacyButton);

    menuButton.addEventListener("click", () => {
      if (currentDownloadActive) {
        browser.runtime.sendMessage({ type: "bridge-pulse" }).catch(() => undefined);
        showToast("Reactivating Parabolic…", "info", true, "cancel");
        return;
      }
      menu.hidden = !menu.hidden;
      menuButton.setAttribute("aria-expanded", String(!menu.hidden));
      if (!menu.hidden) {
        toast.hidden = true;
        menu.classList.toggle("above", shouldOpenAbove());
        updateBridgeStatus();
      }
    });

    toast = document.createElement("div");
    toast.className = "toast";
    toast.setAttribute("role", "status");
    toast.hidden = true;
    toastMessage = document.createElement("span");
    toastMessage.className = "toast-message";
    toastActions = document.createElement("div");
    toastActions.className = "toast-actions";
    toast.append(toastMessage, toastActions);
    group.append(mainButton, menuButton);
    shell.append(closeButton, group, menu, toast);
    shadow.append(style, shell);
    document.documentElement.append(host);

    document.addEventListener("pointerdown", (event) => {
      if (!menu.hidden && !event.composedPath().includes(host)) {
        menu.hidden = true;
        menuButton.setAttribute("aria-expanded", "false");
      }
    }, true);
  }

  function updatePosition() {
    positionFrame = null;
    if (overlayDismissed) {
      if (host) {
        host.style.display = "none";
      }
      return;
    }
    if (!host || !primaryMedia || !settings.detectMedia || !settings.showOverlay) {
      if (host) {
        host.style.display = "none";
      }
      return;
    }

    const fullscreenContainer = document.fullscreenElement;
    const mountTarget = fullscreenContainer
      && fullscreenContainer !== primaryMedia
      && fullscreenContainer.contains(primaryMedia)
      ? fullscreenContainer
      : document.documentElement;
    if (host.parentNode !== mountTarget) {
      mountTarget.append(host);
    }

    const rect = primaryMedia.getBoundingClientRect();
    if (mediaScore(primaryMedia) < 0) {
      host.style.display = "none";
      return;
    }

    host.style.display = "block";
    const width = Math.max(host.getBoundingClientRect().width, 190);
    const horizontalInset = 12;
    const verticalInset = 12;
    const useLeft = settings.overlayPosition.endsWith("left");
    const useBottom = settings.overlayPosition.startsWith("bottom");
    const left = useLeft
      ? rect.left + horizontalInset
      : rect.right - width - horizontalInset;
    const top = useBottom
      ? rect.bottom - host.getBoundingClientRect().height - verticalInset
      : rect.top + verticalInset;
    host.style.left = `${Math.max(8, Math.min(left, innerWidth - width - 8))}px`;
    host.style.top = `${Math.max(8, top)}px`;
  }

  function schedulePosition() {
    if (positionFrame === null) {
      positionFrame = requestAnimationFrame(updatePosition);
    }
  }

  function scan() {
    clearTimeout(scanTimer);
    scanTimer = setTimeout(() => {
      const nextPrimaryMedia = findPrimaryMedia();
      if (nextPrimaryMedia !== primaryMedia) {
        primaryMedia = nextPrimaryMedia;
        if (menu) {
          menu.hidden = true;
          menuButton.setAttribute("aria-expanded", "false");
        }
      }
      if (primaryMedia) {
        createOverlay();
      }
      schedulePosition();
    }, 120);
  }

  browser.runtime.onMessage.addListener((message) => {
    if (message?.type === "parabolic-settings-updated") {
      settings = { ...settings, ...message.settings };
      scan();
    }
    if (message?.type === "parabolic-media-catalog-updated") {
      scan();
    }
    if (message?.type === "parabolic-native-event") {
      const event = message.event || {};
      clearDownloadWatchdog();
      if (event.downloadId && !acknowledgedDownloads.has(event.downloadId)) {
        earlyDownloadStates.set(event.downloadId, event.status);
      }
      if (currentDownloadId && event.downloadId && event.downloadId !== currentDownloadId) {
        return;
      }
      if (event.downloadId && !currentDownloadId) {
        currentDownloadId = event.downloadId;
      }
      if (event.status === "analyzing") {
        currentDownloadActive = true;
        armDownloadWatchdog(event.downloadId, 60000);
        showToast(event.message || "Analyzing available media…", "info", true);
      } else if (event.status === "queued") {
        currentDownloadActive = true;
        showToast("Download queued…", "info", true, "cancel");
      } else if (event.status === "scheduled") {
        currentDownloadActive = true;
        const scheduled = event.scheduledAt ? new Date(event.scheduledAt).toLocaleString() : "later";
        showToast(`Download scheduled for ${scheduled}.`, "success", true, "cancel");
      } else if (event.status === "downloading") {
        currentDownloadActive = true;
        if (Number.isFinite(event.progress) || event.message) {
          armDownloadWatchdog(event.downloadId);
        }
        const statusMessage = Number.isFinite(event.progress)
          ? `Downloading… ${Math.round(event.progress)}%`
          : event.message || "Starting yt-dlp…";
        showToast(statusMessage, "info", true, "cancel");
      } else if (event.status === "merging" || event.status === "processing") {
        currentDownloadActive = true;
        clearDownloadWatchdog();
        showToast(event.processingStep
          ? `Processing: ${event.processingStep}…`
          : "Processing downloaded media…", "info", true, "cancel");
      } else if (event.status === "retry-scheduled") {
        currentDownloadActive = true;
        clearDownloadWatchdog();
        showToast(event.actionHint || "Temporary failure; retry scheduled…", "info", true, "cancel");
      } else if (event.status === "completed") {
        currentDownloadActive = false;
        showToast("Download complete.", "success", true, "open-folder");
      } else if (event.status === "failed") {
        showToast(event.message || "The download failed.", "error", true, "update-ytdlp");
        currentDownloadActive = false;
        currentDownloadId = null;
      } else if (event.status === "cancelled") {
        showToast("Download cancelled.", "info");
        currentDownloadActive = false;
        currentDownloadId = null;
      }
      if (event.downloadId && ["completed", "failed", "cancelled"].includes(event.status)) {
        acknowledgedDownloads.delete(event.downloadId);
      }
    }
  });

  browser.runtime.sendMessage({ type: "get-settings" }).then((response) => {
    settings = { ...settings, ...(response?.settings || {}) };
    scan();
  }).catch(scan);

  const observer = new MutationObserver(scan);
  observer.observe(document.documentElement, {
    childList: true,
    subtree: true,
    attributes: true,
    attributeFilter: ["src", "style", "class"]
  });
  for (const eventName of ["loadedmetadata", "play", "pause", "emptied", "resize", "fullscreenchange"] ) {
    document.addEventListener(eventName, scan, true);
  }
  addEventListener("scroll", schedulePosition, true);
  addEventListener("resize", scan, true);
  setInterval(scan, 1500);
  scan();
})();
