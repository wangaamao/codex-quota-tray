# Codex Quota Tray

[简体中文说明](README.zh-CN.md)

**Search keywords:** Codex, Codex quota, Codex remaining usage, ChatGPT Codex, ChatGPT usage limits, five-hour limit, weekly limit, rate limit monitor, usage reset time, Windows taskbar quota monitor, OpenAI Codex usage tracker.

Codex Quota Tray is a tiny, open-source Windows utility that shows the remaining quota for the Codex five-hour and weekly usage windows, together with their reset times.

It stays above the Windows taskbar, automatically sizes itself to the displayed text, refreshes once per minute, and allows only one running instance.

> [!IMPORTANT]
> Codex Quota Tray itself makes **no direct network requests**. It does not include networking code, contact third-party servers, read browser data, inspect prompts, or access raw authentication tokens. It asks the locally installed Codex process for the quota values already associated with the current signed-in user. Codex itself may communicate with OpenAI to provide those values, just as it does during normal Codex use.

## Features

- Shows the remaining percentage for the five-hour Codex window.
- Shows the remaining percentage for the weekly Codex window.
- Shows both reset times in the computer's local time zone.
- Refreshes every 60 seconds.
- Stays above the Windows taskbar and other ordinary windows.
- Automatically fits the displayed text with a small margin.
- Can be dragged to any screen position.
- Opens or focuses Codex when double-clicked.
- Provides Refresh, Open Codex, and Exit actions on right-click.
- Uses a system-wide mutex to prevent duplicate instances.
- Ships as a very small standalone Windows executable.

## Quick start

1. Install Codex CLI or the OpenAI Codex extension for VS Code.
2. Sign in to Codex on the current Windows account.
3. Download a release and run `CodexQuotaTray.exe`.

No Python, Node.js, Visual Studio, API key, or configuration file is required to run the compiled executable.

## Suggested GitHub metadata

Repository description:

```text
A tiny Windows taskbar monitor for Codex five-hour and weekly remaining usage, quota, and reset times.
```

Suggested GitHub topics:

```text
codex, chatgpt, openai, codex-quota, remaining-usage, usage-limits,
rate-limit-monitor, quota-monitor, windows, windows-taskbar, winforms
```

To verify the local Codex installation manually:

```powershell
codex --version
codex login status
```

If needed, sign in with:

```powershell
codex login
```

## How it works

The application follows a deliberately small local flow:

```text
CodexQuotaTray.exe
        |
        | starts local processes through standard input/output
        v
codex login status
        |
        | signed in
        v
codex app-server --stdio
        |
        | local JSON-RPC: account/rateLimits/read
        v
five-hour and weekly percentages + reset timestamps
```

At each refresh, the program:

1. Locates the installed `codex.exe`.
2. Runs `codex login status` to confirm that the current Windows user is signed in.
3. Starts `codex app-server --stdio` as a local child process.
4. Sends an `account/rateLimits/read` request over redirected standard input.
5. Reads the response from redirected standard output.
6. Keeps only the two percentages and two reset timestamps in memory.
7. Terminates the temporary app-server child process.

There is no separate backend, telemetry service, analytics SDK, updater, database, or hidden background service in this repository.

## Privacy and network behavior

### What this application does not do

Codex Quota Tray does **not**:

- make HTTP, HTTPS, WebSocket, DNS, or other direct network requests;
- bundle or request an OpenAI API key;
- read or export the raw Codex authentication token;
- inspect conversations, prompts, source files, browser history, or clipboard data;
- write quota or account data to disk;
- send telemetry, analytics, crash reports, or usage data;
- connect to advertising, tracking, or third-party services;
- transfer credentials or account information between computers.

You can verify these claims by reviewing the complete application source in [`src/Program.cs`](src/Program.cs). The program only uses Windows Forms, local process execution, local JSON parsing, and a few Windows user-interface functions.

### Important network clarification

The tray application itself does not connect to the internet. However, the locally installed **Codex** process may contact OpenAI when it checks authentication or retrieves current rate-limit information. That network activity belongs to Codex and is governed by the user's Codex installation and OpenAI account settings.

Without Codex being able to reach its service, fresh quota information may be unavailable. Therefore this is not an offline quota calculator; it is a local display for data returned by Codex.

## Requirements

### To run the compiled executable

- Windows 10 or Windows 11.
- .NET Framework 4.x, normally included with Windows 10/11.
- Codex CLI or the OpenAI Codex VS Code extension.
- A Codex account signed in under the current Windows user.
- Network access required by Codex itself to retrieve current account information.

### To build from source

- Windows 10 or Windows 11.
- .NET Framework 4.x compiler, or Visual Studio / Build Tools with Windows Forms support.
- No NuGet packages or third-party code dependencies.

Run from the repository root:

```bat
build.cmd
```

The build script looks for:

```text
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
```

The compiled application is written to:

```text
dist/CodexQuotaTray.exe
```

## Codex discovery

The application searches for `codex.exe` in these locations:

1. Directories listed in the current `PATH` environment variable.
2. The current user's VS Code extension directory.
3. The current user's global npm `@openai/codex` directory.

Portable installations and nonstandard editor locations may not be discovered. In that case, add the directory containing `codex.exe` to `PATH`.

## Controls

- Drag the label to reposition it.
- Double-click the label to open or focus Codex.
- Right-click the label to refresh, open Codex, or exit.
- Start the executable again while it is running: the new process exits immediately.

## Troubleshooting

### `Codex Not installed`

Install Codex CLI or the OpenAI Codex VS Code extension. If Codex is already installed in a custom location, add its directory to `PATH`.

### `Codex Not signed in`

Run `codex login`, complete the sign-in flow, then select **Refresh now** from the tray's right-click menu.

Codex sign-in state is scoped to the Windows user. An administrator account, standard account, and remote-desktop account may each have different sign-in state.

### `Codex Unavailable` or a timeout

Possible causes include:

- Codex cannot reach its service because of a firewall, proxy, or network issue.
- The Codex session expired.
- The current user cannot access the local `.codex` directory.
- The installed Codex version does not support the required app-server method.
- A future Codex update changed the local protocol.

Try:

```powershell
codex login status
codex app-server --help
codex update
```

Then restart Codex Quota Tray.

### Antivirus or Windows SmartScreen warning

Release executables may be unsigned. Windows can display a SmartScreen warning for unsigned downloads. Review the source, build it yourself if preferred, or choose **More info** and **Run anyway** only when you trust the download source.

Company-managed devices may also require approval under AppLocker, antivirus, or endpoint-security policies.

Version 1.3.0 removes forceful child-process termination, process enumeration, and high-frequency topmost polling. It also includes a standard `asInvoker` application manifest and Windows version metadata to reduce heuristic false positives. No unsigned executable can be guaranteed safe from every antivirus heuristic; trusted code signing is the strongest long-term reputation signal.

### Double-click does not open Codex

The application first tries to focus an existing Codex window. If none is found, it runs:

```powershell
codex app
```

Update Codex if the installed CLI does not provide the `app` command.

### Another application covers the tray

The utility reapplies its topmost state every 750 milliseconds. Elevated applications and some exclusive full-screen programs may still cover a non-elevated window. Running everyday utilities as administrator is generally not recommended.

### Multiple monitors or display scaling

The label initially appears near the taskbar on the primary display and can then be dragged elsewhere. Custom taskbars, vertical taskbars, or unusual DPI settings can affect its initial position but do not affect quota retrieval.

## Repository files

Every repository file has a specific purpose:

| File | Repository note |
| --- | --- |
| `.codex-plugin/plugin.json` | Codex plugin manifest and user-facing plugin metadata. |
| `.gitignore` | Excludes generated binaries, archives, and local IDE files from source commits. |
| `LICENSE` | MIT license for public reuse, modification, and redistribution. |
| `README.md` | Main GitHub documentation, security explanation, setup, and troubleshooting guide. |
| `README.zh-CN.md` | Simplified Chinese version of the main documentation. |
| `NOTES.txt` | Short plain-text instructions placed beside release binaries. |
| `NOTES.zh-CN.txt` | Simplified Chinese quick-start note placed beside release binaries. |
| `LICENSE.zh-CN.md` | Unofficial Simplified Chinese translation of the MIT license; the English license controls. |
| `build.cmd` | Dependency-free Windows build script using the .NET Framework C# compiler. |
| `launch-quota-tray.vbs` | Optional launcher that starts the compiled app without a console window. |
| `stop-quota-tray.cmd` | Optional helper that stops the running tray process. |
| `src/Program.cs` | Complete production source: Codex discovery, local IPC, UI, and single-instance behavior. |
| `src/AssemblyInfo.cs` | Standard product, publisher, copyright, and version metadata embedded in the EXE. |
| `src/app.manifest` | Declares a normal non-elevated, DPI-aware Windows application. |
| `skills/codex-quota-tray/SKILL.md` | Codex plugin skill instructions for starting and stopping the utility. |
| `dist/CodexQuotaTray.exe` | Generated release executable; excluded from source commits by `.gitignore`. |

Source and script files also include a short header comment or metadata description explaining their role. JSON does not support comments, so the plugin manifest uses its standard `description` and interface metadata fields instead.

## Compatibility

Quota retrieval relies on the local Codex app-server protocol. This protocol can change between Codex releases. Test the application again after major Codex updates.

Codex Quota Tray 1.3.0 was developed against Codex CLI `0.142.0`.

## License

MIT. See [`LICENSE`](LICENSE).

## Disclaimer

This is an independent utility and is not an official OpenAI product. "OpenAI" and "Codex" are trademarks of their respective owner.
