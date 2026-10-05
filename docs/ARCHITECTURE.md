# Architecture

LePa Sentinel is a single .NET 8 WPF executable (`LePaSentinel.exe`) with three roles:

| Command line | Role |
|---|---|
| `LePaSentinel.exe --hook` | Called by Claude Code on every hook event. Reads the event JSON from stdin, updates one state file, exits. |
| `LePaSentinel.exe --install-hooks` / `--uninstall-hooks` | Adds/removes the hooks in Claude Code's user settings (used by the installer and by the tray menu). |
| `LePaSentinel.exe` | The tray app: icon, panel, sounds. Single instance: starting it again opens the panel of the running instance. |

```
Claude Code ──(SessionStart, UserPromptSubmit, Notification, PostToolUse, Stop, SessionEnd)──▶ LePaSentinel.exe --hook
                                                                                                    │ atomic write
                                                                          <ClaudeDir>\lepa-sentinel\<session_id>.json
                                                                                                    │ read every 1 s
                                                     tray icon · panel · sounds ◀── LePaSentinel.exe (tray app)
```

`<ClaudeDir>` is `%CLAUDE_CONFIG_DIR%` if set, otherwise `%USERPROFILE%\.claude`.

## Hook mode (`src/HookHandler.cs`)

Hook mode must be fast and must never disturb Claude Code: it writes nothing to stdout, swallows every exception, and runs before any WPF assembly is loaded (`Program.RunGui` is a separate, non-inlined method). It takes about 0.1 s per call. Set `LEPA_SENTINEL_DEBUG=1` to log exceptions to `%TEMP%\lepa-sentinel-hook.log`.

State machine:

| Event | Effect |
|---|---|
| `SessionStart` | Creates the session as `idle` without a prompt, so it is not shown yet. With `source=clear` it resets to `idle`. |
| `UserPromptSubmit` | `working`; stores the prompt (pasted blocks become "[pasted text]", tags stripped, whitespace collapsed, max 300 chars) and starts the timer. |
| `Notification` | `permission_prompt` / `elicitation_dialog` → `waiting` with a translated message ("Permission needed: Bash"). `idle_prompt` → `waiting` **only if** the session was `working` (that reminder also arrives 60 s after a task finished). `auth_success` is ignored. |
| `PostToolUse` | If `waiting` → back to `working` (the permission was granted). |
| `Stop` | `done`, stores the finish time. |
| `SessionEnd` | Deletes the state file. |

On `SessionStart` and `UserPromptSubmit` (or when it is missing) the hook also records the **Claude Code process**. It walks up the process tree from itself (hook ← shell ← `claude.exe`, falling back to `node.exe`) using a Toolhelp32 snapshot, and stores its PID and start time (`src/ClaudeProcess.cs`).

### State file

```json
{
  "sessionId": "35e92599-…",
  "cwd": "C:\\dev\\acme-api",
  "status": "working",
  "prompt": "Add JWT login endpoint",
  "message": null,
  "startedAt": "2026-10-05T15:48:00Z",
  "finishedAt": null,
  "updatedAt": "2026-10-05T15:48:00Z",
  "transcriptPath": "C:\\Users\\me\\.claude\\projects\\…\\….jsonl",
  "claudePid": 25072,
  "claudeStartedAt": "2026-10-05T15:47:58Z"
}
```

Writes are atomic (temp file + rename); orphan `.tmp` files are cleaned at start-up.

## Tray app

### What is shown (`MainWindow.Refresh`, every second, also while the panel is hidden)

- **Only sessions with a prompt.** The Claude Code VS Code extension keeps a spare `claude.exe` ready for each window; it sends `SessionStart` but never a prompt, and would otherwise show up as an empty duplicate.
- **Dead process → removed.** If `claudePid` is no longer running (or the PID now belongs to a process with a different start time), the state file is deleted.
- **One process, one session.** If several session ids share a PID (for example after `/clear`), only the most recently updated one is kept.
- Files not updated for 12 hours are deleted.
- A `working` session whose transcript has not changed for 5 minutes shows "stalled?". An interrupted session (Esc) sends no event.
- `done` turns into `idle` after `DoneToIdleMinutes` (30).
- Order: waiting > working > done > idle, then by project name.

Sounds play on status **changes** observed after start-up (`src/Sounds.cs` synthesizes both chimes in memory: no audio files).

### Tray icon (`src/TrayIcon.cs`)

A disc drawn at runtime in the colour of the most urgent state (waiting > working > done > idle), with the number of active tasks (working + waiting, "9+" above nine). Left click toggles the panel. The context menu holds the settings.

### Panel (`src/MainWindow.xaml[.cs]`)

- Opens above the notification area, on whichever edge the taskbar is. It hides when it loses focus, unless it is **pinned**. Dragging the header also pins it.
- Clicking the tray icon while the panel is open closes it: the click first deactivates (hides) the panel, and a `TogglePanel` within 400 ms does not reopen it. A focus loss within 600 ms of opening is ignored, because Windows sometimes bounces the focus.
- When visible it is `Topmost`, and every 2 s it re-asserts `HWND_TOPMOST` without taking focus.
- **Clicking a card** (`src/WindowFocus.cs`) looks for the VS Code window whose title ("file - workspace - Visual Studio Code") contains the session folder or one of its parents, since sessions often run in a subfolder of the workspace. Next it tries the first ancestor window of the `claude.exe` process (a standalone terminal). Only when the project is not open anywhere does it run `code <folder>`.

### Connecting to Claude Code (`src/HookInstaller.cs`)

- Edits `<ClaudeDir>\settings.json` with `System.Text.Json.Nodes`, so every other setting is preserved. A one-time backup `settings.json.bak-lepa-sentinel` is made first.
- Adds one hook per event (`PostToolUse` with matcher `*`) with the command `"<exe path with forward slashes>" --hook` and a 5 s timeout. The command works both in Git Bash and in cmd.
- Hooks are recognised as ours by `--hook` plus the executable name (also the pre-release name `Sentinella.exe`), so reinstalling never duplicates them.
- At start-up the tray app makes sure the hooks point at the current executable (after an update or a move). The very first time it asks for consent. `Settings.HooksWanted` remembers the user's choice.

### Microsoft Store (MSIX) specifics

- `AppInfo.IsPackaged` (via `GetCurrentPackageFullName`).
- The package folder changes on every update, so hooks call the **app execution alias** `%LOCALAPPDATA%\Microsoft\WindowsApps\LePaSentinel.exe` declared in the manifest.
- "Start with Windows" uses the manifest `StartupTask` (`LePaSentinelStartup`) instead of the `Run` registry key.
- `%APPDATA%` writes are virtualized per package: the preferences stay private to the package. `<ClaudeDir>` is outside `AppData`, so the hook and the app share it normally.

## Files

| File | Purpose |
|---|---|
| `src/Program.cs` | Entry point and command-line dispatch, single instance (mutex `Local\LePaSentinel.GUI`, event `Local\LePaSentinel.Show`) |
| `src/AppInfo.cs` | Name, version, packaged detection, hook executable path, Claude config dir |
| `src/L.cs` | UI strings (Italian / English, `LEPA_SENTINEL_LANG` override) |
| `src/HookHandler.cs` | Hook state machine |
| `src/HookInstaller.cs` | Connect/disconnect Claude Code hooks |
| `src/ClaudeProcess.cs` | Find the `claude.exe` ancestor, check if a PID is alive |
| `src/Store.cs` | `SessionState` model and state files |
| `src/Settings.cs` | App preferences, autostart (Run key / StartupTask) |
| `src/Sounds.cs` | In-memory WAV synthesis |
| `src/WindowFocus.cs` | Bring a session's window to the front |
| `src/TrayIcon.cs` | Tray icon, menu, first-run consent |
| `src/MainWindow.xaml[.cs]` | Panel |
| `installer/LePaSentinel.iss` | Inno Setup script (per-user install, tasks "connect" and "autostart") |
| `installer/artifact-signing.json` | Azure Artifact Signing account and profile (not secret) |
| `packaging/msix/` | MSIX manifest template, Store identity, tile images |
| `packaging/store/` | Microsoft Store listing texts and submission notes |
| `tools/make-assets.py` | Regenerates the app icon, the MSIX/Store images and the README images in `docs/images` (Pillow) |
| `build.ps1` | Build, sign, zip, installer, MSIX |

## Paths at runtime

| What | Where |
|---|---|
| Session state | `<ClaudeDir>\lepa-sentinel\*.json` |
| Preferences | `%APPDATA%\LePa Sentinel\settings.json` |
| Hooks | `<ClaudeDir>\settings.json` → `hooks` |
| Autostart (installer/portable) | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `LePa Sentinel` |

## Testing tips

- Hook events can be simulated by piping JSON to `LePaSentinel.exe --hook`. Prefer a Python or PowerShell script file: Git Bash's `echo` mangles backslashes, and `bash` launched from Python on Windows may be WSL's.
- Demo sessions for screenshots: write files to `<ClaudeDir>\lepa-sentinel\demo-*.json` with a `prompt` and **without** `claudePid` (otherwise they are removed as dead). Run with `LEPA_SENTINEL_LANG=en` for English. The panel window is created only the first time it is opened. In PowerShell, `FindWindow($null, …)` passes `""` and finds nothing, so use `EnumWindows`.
