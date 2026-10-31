let port, pending, timer, last = 0, connected = false;
const browser = navigator.userAgent.includes("Edg/") ? "msedge" : "chrome";
function deliver(message) {
  pending = message;
  if (timer) return;
  timer = setTimeout(() => {
    timer = null;
    try {
      if (!port) {
        port = chrome.runtime.connectNative("org.hdrpilot.video");
        port.onMessage.addListener(() => { connected = true; });
        port.onDisconnect.addListener(() => { void chrome.runtime.lastError; port = null; connected = false; });
      }
      last = Date.now(); port.postMessage(pending);
    } catch { port = null; connected = false; }
  }, Math.max(0, 650 - (Date.now() - last)));
}
chrome.runtime.onMessage.addListener((message, sender, reply) => {
  if (message.kind === "status") { reply({ connected }); return; }
  if (message.kind !== "playback" || !sender.tab || sender.frameId !== 0 || !sender.url?.startsWith("https://www.youtube.com/")) return;
  if (!["unknown", "hdr", "sdr"].includes(message.range) || typeof message.playing !== "boolean" || typeof message.fullscreen !== "boolean") return;
  // Only the active YouTube tab in the focused browser window can request HDR.
  chrome.tabs.get(sender.tab.id).then(tab => chrome.windows.get(tab.windowId).then(window => {
    if (!tab.active || !window.focused) return;
    deliver({ version: 1, playing: message.playing, fullscreen: message.fullscreen, range: message.range, browser });
  })).catch(() => {});
});
chrome.tabs.onActivated.addListener(() => deliver({ version: 1, playing: false, fullscreen: false, range: "unknown", browser }));
chrome.tabs.onRemoved.addListener(() => deliver({ version: 1, playing: false, fullscreen: false, range: "unknown", browser }));
chrome.windows.onFocusChanged.addListener(() => deliver({ version: 1, playing: false, fullscreen: false, range: "unknown", browser }));
