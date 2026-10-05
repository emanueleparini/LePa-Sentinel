# Privacy Policy — LePa Sentinel

*Last updated: 5 October 2026*

LePa Sentinel is published by LePa s.r.l. This policy explains what the app does with data.

## Summary

LePa Sentinel **does not collect, transmit or share any data**. It has no network access, no accounts, no analytics and no telemetry. Everything stays on your computer.

## What the app reads and writes locally

To show the status of your Claude Code sessions, the app works only with files in your Windows user profile:

| Data | Where | Why |
|---|---|---|
| Hook events from Claude Code: session id, working folder, the text of your latest prompt (shortened to 300 characters), status messages, timestamps, the process id of Claude Code, the path of the session transcript | `%USERPROFILE%\.claude\lepa-sentinel\` (one small file per session) | To display the cards in the panel. The file of a session is deleted when the session ends; leftovers are deleted after 12 hours. |
| Claude Code's user settings | `%USERPROFILE%\.claude\settings.json` (plus a one-time backup `settings.json.bak-lepa-sentinel`) | To add or remove LePa Sentinel's hooks, only after you agree. Nothing else in the file is changed. |
| App preferences (panel position, sound on/off, pinned) | `%APPDATA%\LePa Sentinel\settings.json` | To remember your choices. |

The app also looks at the titles of open windows and at the list of running processes on your computer, only to bring the right window to the front when you click a session and to detect sessions that have ended. This information is not stored.

The app never reads the content of your transcripts or of your code.

## Removing your data

Uninstalling the app removes the hooks and the folders listed above. You can also disconnect the hooks at any time from the tray menu (**Connected to Claude Code**).

## Contact

Questions about this policy: open an issue at https://github.com/emanueleparini/LePa-Sentinel/issues or contact LePa s.r.l.
