<p align="center">
  <img src="https://img.shields.io/badge/platform-Windows-0078D6?logo=windows" alt="Windows">
  <img src="https://img.shields.io/badge/stremio-5.x-7B5BF5?logo=stremio" alt="Stremio 5.x">
  <img src="https://img.shields.io/badge/license-MIT-green" alt="MIT License">
  <br>
  <a href="https://feel-it.stream">feel-it.stream</a>
</p>

# Feel It.Stream

**X-Ray mod for Stremio Desktop** — see who's on screen and what's playing, in real time.

While watching a movie or series on Stremio, press the X-Ray button to reveal a sidebar showing the **cast currently on screen** and the **soundtrack** for what you're watching.

> **Zero modification to Stremio.** FIS runs as a separate background process and injects into Stremio's WebView2 via Chrome DevTools Protocol. No files are replaced, patched, or modified. Uninstall cleanly at any time.

---

## Features

- **On-screen cast** — See which actors are in the current scene with photos and character names
- **Soundtrack** — Browse the movie/series soundtrack *(coming soon — see [API Status](#api-status))*
- **Non-invasive** — Stremio runs completely unmodified; FIS injects at runtime
- **Auto-start** — The injector launches with Windows and waits for Stremio
- **One-click install/uninstall** — Single exe, no dependencies, no admin rights needed
- **Auto-update** — The installer checks for new versions on launch
- **Open source** — Full source code available, build it yourself

---

## Installation

### For users

1. Download **`FISInstaller.exe`** from the [latest release](../../releases/latest)
2. Run it
3. Click **INSTALL**
4. Restart Stremio
5. Play any movie or series and click the X-Ray button in the player controls

That's it. The installer downloads everything it needs automatically from [feel-it.stream](https://feel-it.stream).

### Uninstall

Run `FISInstaller.exe` again and click **UNINSTALL**. This removes the bundle, environment variable, startup shortcut, and stops the injector. Stremio is left exactly as it was before.

---

## API Status

FIS connects by default to the [feel-it.stream](https://feel-it.stream) API for fetching movie/series data.

| Feature | Status | Source |
|---------|--------|--------|
| On-screen cast | Available | TMDB API |
| Soundtrack / Music | Not yet available | Spotify API *(coming soon)* |

The music/soundtrack tab is present in the UI but will show placeholder content until the Spotify integration is deployed on the API. This will be enabled in a future update without needing to reinstall — the mod bundle updates automatically.

If you want to self-host the API or point to a different backend, edit the `XRAY_API_BASE_URL` in `src/constants.js` and rebuild the bundle.

---

## How it works

FIS uses a three-part architecture that keeps Stremio completely untouched:

```
                        ┌──────────────────────────────┐
                        │     FIS Installer (.exe)     │
                        │  Sets env var, downloads JS  │
                        │  bundle, creates auto-start  │
                        └──────────────┬───────────────┘
                                       │ launches
                                       v
┌───────────────┐       ┌──────────────────────────────┐
│    Stremio    │       │   FIS Injector (background)   │
│  (WebView2)   │<──────│  Monitors for Stremio process │
│               │  CDP  │  Connects via WebSocket       │
│               │<──────│  Sends Runtime.evaluate       │
└───────┬───────┘       └──────────────────────────────┘
        │ runs inside
        v
┌──────────────────────────────────────────────────────┐
│              feelit.bundle.js (injected)              │
│                                                       │
│  ┌─────────────┐  ┌──────────┐  ┌─────────────────┐ │
│  │   Router     │  │  Player  │  │    Sidebar       │ │
│  │  Observer    │──│ Injector │──│  (React, cast    │ │
│  │ (hash/url)   │  │ (button) │  │   & music)       │ │
│  └─────────────┘  └──────────┘  └─────────────────┘ │
│                                          │            │
└──────────────────────────────────────────┼────────────┘
                                           │ fetches
                                           v
                              feel-it.stream/api/v2/xray
                                    (TMDB + Spotify)
```

### Step by step

1. **Environment variable** — The installer sets `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222` (user scope). This tells Stremio's WebView2 to expose a CDP debugging port on localhost.

2. **Background injector** — A lightweight process (`FISInstaller.exe --inject`) runs in the background. It polls for the `stremio-shell-ng` process every 3 seconds.

3. **CDP connection** — When Stremio is detected, the injector queries `http://127.0.0.1:9222/json` to find the WebView2 page, then connects via WebSocket.

4. **Bundle injection** — The injector sends the full JavaScript bundle via `Runtime.evaluate`. The bundle bootstraps itself inside Stremio's page.

5. **Route observation** — The bundle watches Stremio's URL hash for route changes (`#/player/...`, `#/settings`, etc.) and injects/ejects UI components accordingly.

6. **Player UI** — On the player route, FIS adds an X-Ray button to the control bar. Clicking it opens a sidebar with cast data fetched from the [feel-it.stream](https://feel-it.stream) API.

7. **Re-injection** — If Stremio navigates to a new page or reloads, the injector detects the page change and re-injects automatically.

---

## What gets installed

| Item | Location | Purpose |
|------|----------|---------|
| `feelit.bundle.js` | `%LOCALAPPDATA%\FIS\` | The JavaScript mod (downloaded from [feel-it.stream](https://feel-it.stream)) |
| `fis.log` | `%LOCALAPPDATA%\FIS\` | Runtime logs for debugging |
| Environment variable | User scope | Enables CDP port on Stremio's WebView2 |
| Startup shortcut | `%APPDATA%\...\Start Menu\Programs\Startup\` | Auto-starts the injector with Windows |

No files are added to or modified in Stremio's installation directory.

---

## Build from source

### Prerequisites

- **Node.js** 18+ and npm
- **.NET Framework 4.x** (included with Windows — `csc.exe` is used to compile the installer)

### Build the JavaScript bundle

```bash
git clone https://github.com/MwoaA-a/Stremio-Feel-It-Stream-Mod.git
cd Stremio-Feel-It-Stream-Mod
npm install
npm run build
```

Output: `dist/feelit.bundle.js` (~177 KB, includes React 18 + ReactDOM)

### Build the Windows installer

```bash
cd installer
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo -target:winexe -win32icon:fis.ico -out:FISInstaller.exe fis-installer.cs
```

Output: `installer/FISInstaller.exe` (~56 KB)

### Self-host the API

By default, FIS uses `https://feel-it.stream/api/v2/xray` as its backend. To point to your own API:

1. Edit `src/constants.js` and change `XRAY_API_BASE_URL`
2. Run `npm run build`
3. Deploy the new `dist/feelit.bundle.js`

---

## Project structure

```
src/
  standalone/                # Self-contained injection system
    bootstrap.js             # Entry point — wires up all modules
    router-observer.js       # Watches URL hash for route changes
    css-injector.js          # Injects/removes the stylesheet at runtime
    dom-query.js             # DOM selectors for Stremio's UI elements
    overlay-controller.js    # Keeps control bar visible when sidebar is open
    injectors/
      player-injector.js     # Mounts X-Ray button + sidebar in the player
      settings-injector.js   # Mounts FIS settings panel
    components/
      XRaySidebar.js         # Main sidebar (tabs, cast list, music list)
      XRayButton.js          # X-Ray toggle button for the control bar
      CastCard.js            # Actor card (photo, name, character)
      MusicSection.js        # Soundtrack list with Spotify links
      FeelItSettings.js      # Settings panel component
    components.js            # Shared UI primitives (FISButton, CloseIcon, cx)
    styles.css               # All FIS styles (injected as raw text)
    loader.js                # Lightweight remote loader (for browser/userscript use)
  hooks/
    useXRayData.js           # Fetches cast + music from the API
    useFeelItSettings.js     # Reads/writes FIS preferences in localStorage
  constants.js               # API URL, tab names, color palette
installer/
  fis-installer.cs           # C# source — GUI installer + background injector
  fis.ico                    # Application icon (red glow sphere)
webpack.config.js            # Webpack config (IIFE bundle, React bundled)
package.json                 # npm dependencies
```

---

## FAQ

**Does this modify Stremio's source code?**
No. FIS injects JavaScript at runtime via Chrome DevTools Protocol. Stremio's files are never touched.

**Is this safe?**
The full source code is available in this repo. The injector only communicates with `localhost:9222` (Stremio's own debugging port). The bundle is served from [feel-it.stream](https://feel-it.stream) over HTTPS, or you can build it yourself.

**Why is the music tab empty?**
The Spotify integration is not yet deployed on the [feel-it.stream](https://feel-it.stream) API. It will be enabled in a future update — no reinstall needed.

**Does it work with Stremio Web (browser)?**
The installer is for Stremio Desktop (Windows). For browser use, see `src/standalone/loader.js` for userscript/bookmarklet instructions.

**Will it break if Stremio updates?**
FIS relies on Stremio's DOM structure (CSS class names for the player and control bar). If Stremio significantly changes its UI, FIS may need an update. The injector itself is independent of Stremio's version.

**Can I self-host the API?**
Yes. Change `XRAY_API_BASE_URL` in `src/constants.js`, rebuild the bundle, and deploy it. See [Self-host the API](#self-host-the-api).

**How do I check the logs?**
Open `%LOCALAPPDATA%\FIS\fis.log` or click "Open logs" in the installer.

---

## License

MIT
