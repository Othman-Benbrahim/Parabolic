(() => {
  const reported = new Map();
  let scanTimer = null;

  function normalizeUrl(value) {
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

  function mediaLabel(element, fallback) {
    return element.getAttribute("title")
      || element.getAttribute("aria-label")
      || element.getAttribute("data-title")
      || document.title
      || fallback;
  }

  function inferKind(url, mime, fallback) {
    const normalizedMime = (mime || "").toLowerCase();
    let extension = "";
    try {
      const filename = new URL(url, document.baseURI).pathname.split("/").pop() || "";
      extension = filename.includes(".") ? filename.split(".").pop().toLowerCase() : "";
    } catch (_) {
      return fallback;
    }
    if (extension === "m3u8" || normalizedMime.includes("mpegurl")) {
      return "hls";
    }
    if (extension === "mpd" || normalizedMime === "application/dash+xml") {
      return "dash";
    }
    return fallback;
  }

  function addCandidate(candidates, candidate) {
    const url = normalizeUrl(candidate.url);
    if (!url) {
      return;
    }
    const key = `${candidate.kind}|${url}`;
    candidates.set(key, { ...candidate, url });
  }

  function collectMedia() {
    const candidates = new Map();
    const elements = [...document.querySelectorAll("video, audio")];

    for (const element of elements) {
      const kind = element.tagName.toLowerCase();
      const common = {
        kind,
        label: mediaLabel(element, kind === "video" ? "HTML5 video" : "HTML5 audio"),
        pageUrl: location.href,
        mime: "",
        width: kind === "video" ? element.videoWidth || null : null,
        height: kind === "video" ? element.videoHeight || null : null,
        duration: Number.isFinite(element.duration) ? element.duration : null
      };

      const elementUrl = element.currentSrc || element.src;
      addCandidate(candidates, {
        ...common,
        url: elementUrl,
        kind: inferKind(elementUrl, element.type, kind)
      });
      for (const source of element.querySelectorAll("source[src]")) {
        addCandidate(candidates, {
          ...common,
          url: source.src,
          mime: source.type || "",
          kind: inferKind(source.src, source.type, kind)
        });
      }
    }

    if (elements.length > 0 && candidates.size === 0 && window.top !== window) {
      addCandidate(candidates, {
        url: location.href,
        pageUrl: location.href,
        source: "page",
        kind: "embedded-page",
        label: document.title || "Embedded media player",
        mime: ""
      });
    }

    return [...candidates.values()];
  }

  function sendNewCandidates() {
    const freshCandidates = [];
    for (const candidate of collectMedia()) {
      const key = `${candidate.kind}|${candidate.url}`;
      const serialized = JSON.stringify(candidate);
      if (reported.get(key) !== serialized) {
        reported.set(key, serialized);
        freshCandidates.push(candidate);
      }
    }

    if (freshCandidates.length > 0) {
      browser.runtime.sendMessage({
        type: "media-detected",
        candidates: freshCandidates
      }).catch(() => {
        // The extension may be reloading while the page remains open.
      });
    }
  }

  function scheduleScan() {
    clearTimeout(scanTimer);
    scanTimer = setTimeout(sendNewCandidates, 250);
  }

  const observer = new MutationObserver(scheduleScan);
  observer.observe(document.documentElement, {
    childList: true,
    subtree: true,
    attributes: true,
    attributeFilter: ["src", "type"]
  });

  for (const eventName of ["loadedmetadata", "durationchange", "play"]) {
    document.addEventListener(eventName, scheduleScan, true);
  }

  sendNewCandidates();
})();
