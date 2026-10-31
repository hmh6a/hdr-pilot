(() => {
  let range = "unknown";
  // Standard media APIs expose playback state, not a reliable content HDR flag.
  // Never infer content HDR from the monitor's dynamic-range media query.
  const send = () => {
    const video = document.querySelector("video");
    chrome.runtime.sendMessage({ kind: "playback", playing: !!video && !video.paused && !video.ended && video.readyState >= 2 && document.visibilityState === "visible", fullscreen: !!document.fullscreenElement, range }).catch(() => {});
  };
  chrome.runtime.onMessage.addListener((message, sender, reply) => {
    if (message.kind === "classify" && ["unknown", "hdr", "sdr"].includes(message.range)) { range = message.range; send(); reply({ range }); }
    if (message.kind === "getRange") reply({ range });
  });
  ["play", "pause", "ended", "fullscreenchange", "visibilitychange"].forEach(type => document.addEventListener(type, send, true));
  document.addEventListener("yt-navigate-start", () => { range = "unknown"; send(); });
  // Standard media lifecycle fallback, including stream replacement. Conservative:
  // a quality/source change may require the user to classify the video again.
  document.addEventListener("emptied", event => { if (event.target instanceof HTMLVideoElement) { range = "unknown"; send(); } }, true);
  setInterval(send, 4000);
  send();
})();
