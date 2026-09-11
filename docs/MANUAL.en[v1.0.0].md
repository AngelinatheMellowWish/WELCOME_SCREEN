# User Manual (MANUAL)

> User guide for the Control-style Desktop Title Overlay program
> Document version: v1.0.0 (final)
> Version history: registered in CHANGELOG[v1.0.0].md
> English edition of `MANUAL[v1.0.0].md` (bilingual delivery, maintained in sync)

---

## 1. Installation & First Run

### 1.1 System Requirements
| Item | Requirement |
|------|-------------|
| OS | Windows 10 / Windows 11 (64-bit) |
| Runtime | **None required** (self-contained .NET 8 runtime) |
| Disk / Memory | ~700MB disk (self-contained release incl. .NET runtime, extracted); idle memory target ≤250MB (across processes) |

### 1.2 Get & Install (zero-basis)
1. **Get the release package**: download and extract the release zip from GitHub Releases (or build from source, see `README`). It contains the main program plus child processes:
   `Object1688.Main.exe`, `Object1688.Overlay.exe`, `Object1688.Monitor.exe`, `Object1688.Logging.exe`, `Object1688.ConfigUI.exe`, plus `config/`, `resources/`, etc.
2. **Place it in any folder** (e.g. `C:\Object1688` or a user folder). **Keep the whole folder together** — the five processes must sit in the same directory (do not copy a single exe).
3. **No installer**: it is a portable app — double-click `Object1688.Main.exe` to run.

### 1.3 First Run
1. If Windows shows the **SmartScreen "Unknown publisher"** warning: this build is not code-signed, which is expected — click "More info → Run anyway" (see §5).
2. The app has **no main window**; it runs in the **system tray** (bottom-right). A balloon guide appears on first launch.
3. A **welcome title** is shown once on first launch (can be disabled in the config window).
4. Right-click the tray icon → "Settings…" to open the config window, then add your first rule (see §2 Quick Start).

> **Windows 11 tip**: a newly appeared tray icon may be tucked into the bottom-right **`^` "Hidden icons"** flyout — drag it onto the taskbar to pin it. If you cannot find the icon and want to exit, **double-click `build\quit.cmd` in the release folder** (no tray click needed).

### 1.4 Start with Windows
The program **auto-starts at login by default** (writes `HKCU\...\Run`, can be disabled). Toggle under the config window → "DND / Sound / Auto-start" tab. If you move the program and the auto-start path becomes stale, a tray notice appears at launch (PRC-W-2006); re-enable auto-start to repair.

### 1.5 Uninstall
1. Tray right-click → "Exit" (or "Exit and disable auto-start").
2. **Double-click `build\uninstall.cmd` in the release folder** (removes the Run entry + clears `%APPDATA%\Object1688`; pass `-AlsoCleanLogs` from a terminal to also clear logs), or manually delete the release folder and `%APPDATA%\Object1688`.
   > `.ps1` opens in Notepad by default on Windows 11, so a `.cmd` is provided for double-click.

---

## 2. Quick Start

1. After launch, the icon appears in the **system tray** (bottom-right); **left-click** shows a quick menu (Manual title / Pause·Resume), **double-click** opens the config window, **right-click** shows the full menu, **hover** shows the run status; the right-click menu includes "Open log folder" and "Copy last error code" (AC-93), and clicking an error balloon also opens the log
2. **Manual title**: right-click tray icon → "Manual title" → the configured text appears on screen (content editable in the "Manual title" config tab; shortcut Alt+F by default, changeable/disableable)
3. **Add a trigger rule**: right-click tray → "Settings…" to open the config window
   - In the "Trigger rules" tab use the toolbar "**+ New**" (or next to the list) to add; experienced users may edit the form directly
   - Choose match type (process name / window title) and match mode (exact / contains / wildcard)
   - Enter the match value (e.g. `chrome.exe` or `*game*`)
   - Enter display text (multi-line supported; `{appName}` / `{time}` variables), delay (s), hold (s)
   - Optional: set an outline color to stand out; the preview area shows a live render
   - Click "**Test display**" to preview on the real screen → Save → takes effect immediately (hot reload broadcast)
4. **Verify**: launch the target program and the title appears within seconds (note: DND/pause suppresses automatic titles)

> Note: the **new-rule wizard**, **style-preset apply**, **preview drag-positioning**, and **performance-window charts** (ranking/24h) are all provided — see the config window guide.

---

## 3. Config Window Guide

The config window (tray "Settings…") uses a **tabbed** layout; the top toolbar keeps "New rule / New-rule wizard… / Delete rule / Restore defaults / Import… / Export… / Conflict check / Test display / Save" always available:

| Tab | Description |
|------|-------------|
| Trigger rules | Left rule list + right editor: match type/mode/value, display text (multi-line), delay/hold, font size, position, outline, **style preset apply**; list supports search/copy/↑↓ reorder/batch enable-disable, Ctrl+Z/Y undo-redo |
| Welcome title | Title shown **on every program start** (toggleable; independent lines/size/delay/hold) |
| Manual title | Text shown by tray/hotkey manual trigger (toggleable; independent config; shortcut settable) |
| DND / Sound / Auto-start | Global pause, scheduled quiet-hours toggle, **presentation/mirroring auto-silence toggle**; sound (system/custom source + volume + custom wav/mp3 path); start-with-Windows toggle |
| Help | Built-in usage guide (operations + privacy) |

> Save runs validation → atomic disk write → main process hot-reload broadcast; "Restore defaults" first confirms unsaved changes (AC-38).

### How to fill in outline color / outline width
- **Outline color** (draws a ring around the white title so it stays legible on light backgrounds): click a **Quick colors** swatch for one-tap selection, or click **Pick color…** to use the Windows system color picker; you can also type `#RRGGBB` (`#000000` black / `#FFFFFF` white / `#FF0000` red…); **leave empty = follow the global default** (click **Follow global** to clear).
- **Outline width**: pixels, `0` = no outline (usually 2–6). **Outline mode**: Soft glow (mode A, low cost) / Precise (mode B, sharper).

---

## 4. Performance Monitor Window

- Location: tray menu → "Performance window…"
- Shows: CPU % (per-process curves, 60s/5min switch), memory MB curves, **animation frame-rate curve**, process status table, trigger event stream (ring 500), trigger statistics (persisted cumulative counts), **top-5 rule trigger ranking bar chart / 24h distribution chart**, **last-title review + latest render cost** (time/rule/text, CPU/memory delta, AC-63/68/92/95)
- Closing the window hides it (Monitor stays resident); reopen from tray; window position / time window is remembered
- Export: performance samples CSV (UTF-8 BOM) / events CSV / events TXT / stats JSON → `log/exports/`
- Clear: one-click "Clear history" (event stream + persisted trigger statistics)

> One-click diagnostic bundle: tray right-click → "Export diagnostics…" or script `--control diagnostics` → `log/exports/diagnostics-*.zip` (logs + config + stats + environment, AC-83).

---

## 5. Troubleshooting

| Issue | Resolution |
|-------|------------|
| Tray icon missing / can't exit | Windows 11 may hide it in the `^` "Hidden icons" flyout (drag it out to pin); or **double-click `build\quit.cmd`** in the release folder to exit (or `--control quit`) |
| Title does not appear | Check rule enabled & match value; confirm not in "DND/paused"; open the performance window to inspect the event stream |
| Title hard to read | In the rule editor enable an outline and pick a high-contrast color, or apply a "Style preset" (high-contrast / cinema-dark, F-23/AC-60) |
| Wrong trigger, want it gone | Press the manual hotkey again (default Alt+F) to end the current title early (F-07); or run `--control end` |
| Hotkey does nothing | Ensure the manual shortcut is not cleared; if it is taken by another app, registration fails with a tray notice (AC-71) — change it in the config window |
| No effect in fullscreen games | Some exclusive-fullscreen apps need compatibility handling (best-effort topmost; logs & degrades on failure) — see config window |
| No title over elevated windows | The app runs as the current user and cannot cover elevated (admin) windows; on match it logs OVL-W-3008 and shows a one-time tray notice (AC-94) |
| "Capability unavailable" at startup | Start-up self-check failed and the app is running in degraded mode: follow the on-screen hint (restore default config / reinstall font) then restart |
| Chinese shows as boxes | CJK falls back to the system font by design; if still broken, please report (embedded font covers Latin only) |
| Program crash | Dump file in `log/crash/` (keeps last 10 or ≤200MB); logs in `log/`; report with [error codes](error_codes[v1.0.0].md) |
| Want to submit full context | Tray "Export diagnostics…" or script `--control diagnostics`: creates `log/exports/diagnostics-*.zip` (logs + config + stats + environment, AC-83) |
| Restore defaults | Use "Restore defaults" in the config window toolbar (unsaved changes are confirmed first), or delete `%APPDATA%\Object1688\config.json` and restart |
| Clear records | The performance window "Clear history" wipes the event stream and persisted trigger statistics; log files can be deleted manually under `log/*.log` (or run `build/uninstall.ps1 -AlsoCleanLogs`) |
| SmartScreen "unknown publisher" | This build is not code-signed: click "More info → Run anyway"; trust the exe for long-term use |
| Auto-start broken after moving exe | On startup the app checks whether the Run entry points to the current exe; if invalid it shows a tray notice (PRC-W-2006, AC-53) — toggle auto-start off then on to repair |
| Full uninstall | Run `build/uninstall.ps1` (removes auto-start entry + `%APPDATA%\Object1688`, optionally cleans logs) or follow the manual cleanup steps |

---

## 6. Log & Data Locations

| Item | Location |
|------|----------|
| Config file | `%APPDATA%\Object1688\config.json` |
| Config history backups | `%APPDATA%\Object1688\config.bak1..3` |
| Trigger statistics (persisted) | `%APPDATA%\Object1688\stats.json` |
| Logs | `log/` (under program directory) |
| Crash dumps | `log/crash/` (keeps last 10 or ≤200MB) |
| Monitoring exports / diagnostic bundle | `log/exports/` |

---

## 7. FAQ

**Q: Does the program use the network or upload data?**
A: No. It is fully local — no network, no data uploaded (see "Data & Privacy" below).

**Q: Why does the tray icon change color or pop a balloon?**
A: The icon reflects run state (normal / paused / DND / error, AC-62); a fatal error pops a one-time balloon with an error code, and clicking it opens the log folder (AC-93).

**Q: Can a title cover fullscreen games or admin windows?**
A: Fullscreen apps are best-effort topmost, but exclusive fullscreen may not be coverable (logged and degraded); elevated (admin) windows cannot be covered and show a one-time OVL-W-3008 notice (AC-94).

**Q: How do `{appName}` and `{time}` variables work?**
A: `{appName}` is replaced with the matched process name / window title; `{time}` with the trigger time, formatted per the global `timeFormat` (auto / HH:mm / HH:mm:ss / datetime / 24h).

**Q: How do I show a rule on a specific monitor only?**
A: Set "target screen" (primary or an index) in the rule editor; on multi-monitor setups the title shows on that screen (AC-24).

**Q: How do I temporarily suppress titles?**
A: Tray left/right-click "Pause", or enable scheduled quiet hours; for presentations/mirroring enable "auto-silence" (AC-90).

**Q: How do I roll back a bad config?**
A: Use "Restore defaults" in the config window, or delete `%APPDATA%\Object1688\config.json` and restart; imports are auto-backed-up to `config.bak1..3`.

**Q: What is the default hotkey? Can I change it?**
A: The manual-title default is `Alt+F` (changeable/disableable in the "Manual title" tab); if another app occupies it, registration fails with a tray notice (AC-71).

### Data & Privacy
- This program is **fully local**: no network, no data uploaded
- Logs / event stream record matched process names and window titles (for trigger debugging only), stored locally in `log/` and `%APPDATA%\Object1688`
- Use "Clear history" in the config / performance window to erase records

### Command-line arguments
- `--config <path>` specify config; `--lang <zh-CN|en-US>` override language; `--debug` debug logging; `--no-autostart` portable run; `--version` print version

### Scripting / Control Interface
While the app is running, other scripts can invoke its features (**current user only**, no network):

**Option 1: CLI wrapper (recommended)**
```
Object1688.Main.exe --control status
Object1688.Main.exe --control banner --text "Hello world"
Object1688.Main.exe --control pause
```
- Commands: `ping` / `status` / `banner` (`--text` text, `--screen` target screen) / `manual` / `pause` / `resume` / `toggle-pause` / `end` / `reload` / `config` / `perf` / `about` / `diagnostics` / `quit`
- The JSON response is printed to stdout; exit code `0`=success, `2`=command failed (response carries an error code), `3`=primary instance not running

**Option 2: Named pipe JSON**
- Connect to `\\.\pipe\Object1688.control`, send one line of JSON request, read one line of JSON response
- Request e.g. `{"command":"status"}`; banner request e.g. `{"command":"banner","text":"Hello\nworld","targetScreen":"primary"}`
- Response e.g. `{"ok":true,"data":{...}}` or `{"ok":false,"errorCode":"IPC-E-7005","message":"..."}`

> **Full API reference** (all commands & parameters, request/response fields, PowerShell/Python examples, exit codes & error codes) is in the project root **[`API调用说明.md`](../API调用说明.md)**.

### Third-Party Licenses
- Licensed under GPL-3.0; third-party dependency and embedded font licenses ship with `THIRD_PARTY_NOTICES` (see the "About" window)
