const DEFAULT_SETTINGS = {
  trimPlaylist: false,
  showContextMenu: true,
  detectMedia: true,
  showMediaBadge: true
};

document.addEventListener("DOMContentLoaded", async () => {
  try {
    const settings = await browser.storage.sync.get(DEFAULT_SETTINGS);
    for (const key of Object.keys(DEFAULT_SETTINGS)) {
      document.getElementById(key).checked = settings[key];
    }
  } catch (error) {
    console.error("Unable to load settings:", error);
  }
});

for (const key of Object.keys(DEFAULT_SETTINGS)) {
  document.getElementById(key).addEventListener("change", (event) => {
    browser.storage.sync.set({ [key]: event.target.checked });
  });
}
