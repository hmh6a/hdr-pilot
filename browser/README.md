# YouTube companion (Chrome and Edge)

1. Open Chrome's Extensions page or Edge's Extensions page. Enable the browser's developer extension mode and choose **Load unpacked** for this `browser` folder. This is an explicit user installation; the desktop app does not install extensions.
2. Copy the extension ID (32 lowercase letters a–p).
3. From PowerShell, using your extracted release path:

```powershell
.\scripts\Register-BrowserBridge.ps1 -ExtensionId YOUR_EXTENSION_ID -NativeHostPath 'C:\YourFolder\HDRPilot.NativeHost.exe'
```

4. In HDR Pilot's Video & Browsers page, enable the bridge and save. Reload the YouTube tab. Open the extension popup and mark the current video HDR or SDR; use YouTube's quality menu HDR label to inform your selection. Navigation resets classification to Unknown.

Automatic behavior applies only to the foreground browser and active YouTube tab, with fullscreen required by default. Pausing, ending, tab switching, closure or connection expiry releases the request through normal cooldown/restoration. The protocol carries no URLs or titles. Browser focus and tab IDs stay inside the extension, and no browsing-history permission is requested. The extension's sole declared permission is `nativeMessaging`, with a content script scoped to `https://www.youtube.com/*`.

Standard HTML video APIs do not provide a reliable HDR-content flag across these browsers. A display's dynamic-range media query describes display capability, not video metadata. Consequently, this companion is a playback detector with **manual range classification**. It does not inspect undocumented YouTube player methods or intercept authenticated network traffic. Advanced metadata detection remains unimplemented.

If you choose the SDR workflow, manually enable RTX Video HDR in supported NVIDIA software. HDR Pilot controls Windows HDR only. Unknown videos never request HDR. Desktop signals expire after 12 seconds and pipe connections time out after 15 seconds without valid frames.

Re-run registration after moving the portable host or changing extension ID. Both Chrome and Edge HKCU registrations are created, using an exact accumulated origin allowlist. Unregister with `scripts/Unregister-BrowserBridge.ps1`, then remove the extension in each browser.

Protocol: little-endian 32-bit length + UTF-8 JSON, max 1024 bytes. Exact fields: `version:1`, `playing:boolean`, `fullscreen:boolean`, `range:"unknown"|"sdr"|"hdr"`, `browser:"chrome"|"msedge"`. Native framing follows [Chrome's native messaging specification](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging). See SECURITY.md for same-user trust limits.
