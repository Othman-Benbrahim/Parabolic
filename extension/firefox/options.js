const DEFAULT_SETTINGS = {
  trimPlaylist: false,
  showContextMenu: true,
  detectMedia: true,
  showMediaBadge: true,
  showOverlay: true,
  quickDownloadPreset: "best",
  overlayPosition: "top-right",
  fallbackToProtocol: false
};

let savedTimer = null;

function showSaved() {
  const savedState = document.getElementById("savedState");
  savedState.textContent = "Saved";
  clearTimeout(savedTimer);
  savedTimer = setTimeout(() => {
    savedState.textContent = "";
  }, 1600);
}

async function saveSetting(event) {
  const element = event.currentTarget;
  const value = element.type === "checkbox" ? element.checked : element.value;
  await browser.storage.sync.set({ [element.id]: value });
  showSaved();
}

document.addEventListener("DOMContentLoaded", async () => {
  try {
    const settings = await browser.storage.sync.get(DEFAULT_SETTINGS);
    for (const [key, fallback] of Object.entries(DEFAULT_SETTINGS)) {
      const element = document.getElementById(key);
      if (!element) {
        continue;
      }
      if (typeof fallback === "boolean") {
        element.checked = Boolean(settings[key]);
      } else {
        element.value = settings[key];
      }
      element.addEventListener("change", saveSetting);
    }
  } catch (error) {
    const savedState = document.getElementById("savedState");
    savedState.textContent = "Unable to load settings";
    savedState.classList.add("error");
    console.error("Unable to load settings:", error);
  }
});
