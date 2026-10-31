const select = document.getElementById("range"), status = document.getElementById("status");
chrome.runtime.sendMessage({ kind: "status" }).then(s => { status.textContent = s.connected ? "Connected to HDR Pilot" : "Open HDR Pilot and enable the browser bridge. Install the native host using the included script."; });
chrome.tabs.query({ active: true, currentWindow: true }).then(async tabs => {
  const tab = tabs[0]; if (!tab) return;
  try {
    const response = await chrome.tabs.sendMessage(tab.id, { kind: "getRange" }); select.value = response.range;
    select.onchange = () => chrome.tabs.sendMessage(tab.id, { kind: "classify", range: select.value }).catch(() => {});
  } catch { select.disabled = true; status.textContent = "Open a YouTube video to use the companion."; }
});
