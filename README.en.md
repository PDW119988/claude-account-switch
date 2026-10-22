# Claude Account Switch

**English** | [한국어](README.md)

A small Windows tray app that **switches the account Claude Code is signed in to, in one click.**
If you use more than one account (say, work and personal), you no longer need to log out and log back in each time.

> **Unofficial tool.** Not affiliated with or supported by Anthropic.

## Features

- Pick an account from the list and press **[Switch]**
- Notification-area (tray) icon
  - Hover to see the current account; each account gets its own icon color
  - Left-click opens the window · right-click switches accounts directly
- **[Add account]**: enter a name, press [Connect], approve in the browser, done
- Optional start with Windows
- No installer. A single exe (uses .NET Framework 4, which ships with Windows)

## Install

1. Download `ClaudeAccountSwitch.exe` from [Releases](../../releases) and put it anywhere.
2. Double-click to run.

On first launch Windows may show **"Windows protected your PC"**, because the exe is not code-signed.
Click **More info → Run anyway**. If you'd rather not, [build it yourself](#build-from-source).

Requirements:
- Windows 10 / 11
- [Claude Code](https://docs.claude.com/en/docs/claude-code) installed (the `claude` command works)

## Usage

### Add an account

1. Press **[Add account]**.
2. Enter a name for the list (e.g. `Work`, `Personal`). Email is optional and pre-fills the sign-in page.
3. Press **[Connect]**. The Claude sign-in page opens in your browser. Approve it.
4. **Copy the code** the browser shows, paste it into the window and press **[Finish]**.
   (If the code is on your clipboard when you come back to the window, it is filled in for you.)

> If your browser is already signed in to claude.ai with a different account, that account is the one you approve.
> Check the account on the approval page.

An account Claude Code is already signed in to doesn't need to sign in again:
press **[Save this account]** in the yellow bar at the top of the window.

### Switch

- Select an account and press **[Switch]** (or double-click the account)
- Or right-click the tray icon → account name

**After switching, start a new Claude Code conversation** to use the new account.
Conversations that were already open keep using the previous account.
In the VS Code extension, `Ctrl+Shift+P` → **Developer: Reload Window** is the sure way.

### Window and exit

Closing the window keeps the app in the notification area. To quit, right-click the icon → **Exit**.

## How it works

Claude Code keeps its sign-in in two places. This app saves **just those two values** per account and swaps them.

| File | Value swapped |
|---|---|
| `%USERPROFILE%\.claude\.credentials.json` | `claudeAiOauth` (sign-in tokens) |
| `%USERPROFILE%\.claude.json` | `oauthAccount` (account info) |

- Nothing else in those files (MCP connections, settings, …) is touched. The app does not rewrite the files; it replaces only that one value in place.
- **Adding an account signs in inside a temporary folder.** It runs `claude auth login` with a temporary `CLAUDE_CONFIG_DIR`, takes the tokens, and deletes the folder. The account you are currently using is not affected while you add another.
- Tokens rotate while Claude Code runs. Right before each switch, the current account's latest tokens are copied back into its saved entry, so switching back later still works.
- Each switch keeps a copy of both files in `backup` (last 10 switches).
- If you use the `CLAUDE_CONFIG_DIR` environment variable, the app follows it.

## Storage and security

```
%USERPROFILE%\.claude\account-switch\
  profiles\{name}.json   tokens per account
  backup\                copies taken before each switch
```

- Saved entries **contain your sign-in tokens.** They are protected the same way Claude Code's own `.credentials.json` is. Don't share this folder or move it into a cloud-synced folder.
- The app itself **makes no network requests.** Signing in is done by Claude Code's `claude auth login`.

## Good to know

- The app relies on Claude Code's internal file layout. **A Claude Code update that changes it may break the app.**
- A conversation still running on the previous account may write that account back when it refreshes its token. After switching, close open conversations or reload the window.
- Use each account according to its own terms. This tool is for moving between your accounts conveniently.
- Windows only. (On macOS, Claude Code stores tokens in the Keychain.)

## Build from source

No Visual Studio or SDK needed. It uses the C# compiler that ships with Windows.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Output: `dist\ClaudeAccountSwitch.exe`

```
src\
  Program.cs        entry point, single instance
  TrayApp.cs        tray icon, start with Windows
  MainForm.cs       account list window
  RegisterForm.cs   add-account window
  Login.cs          runs claude auth login in a temp folder
  Store.cs          save, switch, token sync, backups
  Json.cs           replaces a single JSON value in place
  Ui.cs             icons, Korean/English strings
tools\make-icon.ps1 generates the app icon
```

The UI language follows the Windows display language (Korean on Korean Windows, English otherwise).

## Uninstall

1. In the window, uncheck **Start with Windows**.
2. Tray icon → **Exit**.
3. Delete `ClaudeAccountSwitch.exe`.
4. To remove saved accounts as well, delete `%USERPROFILE%\.claude\account-switch\`.
   (Claude Code stays signed in to whichever account was active.)

## License

[MIT](LICENSE)
