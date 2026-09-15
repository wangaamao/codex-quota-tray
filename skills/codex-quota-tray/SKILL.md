---
name: codex-quota-tray
description: Start, stop, or diagnose the compact Windows Codex quota bar showing five-hour and weekly remaining usage and reset times.
---

# Codex Quota Tray

Use the files relative to this plugin root.

- Start the distributable app with `dist/CodexQuotaTray.exe` when present. For source-only development, run `launch-quota-tray.vbs` after compiling.
- Stop it with `stop-quota-tray.cmd` only when the user asks.

The monitor checks `codex login status` before using the logged-in local Codex app-server session. Never ask for or read raw tokens. The single-instance, always-on-top UI refreshes every 60 seconds, sizes itself to its text, can be dragged, opens Codex on double-click, and has a right-click open/refresh/exit menu.
