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

### Add an account (how sign-in works)

Sign-in is done by Claude Code's own login command, `claude auth login`.
This app runs that command for you and keeps only the tokens it issues. **It never asks for or stores a password.**

1. Press **[Add account]**.
2. Enter a **name** for the list (e.g. `Work`, `Personal`).
   **Email** is optional; it pre-fills the sign-in page.
3. Press **[Connect]**. The Claude sign-in page opens in your default browser.
4. **Sign in** in the browser, the same way you sign in to claude.ai (email, Google, SSO, …).
   - With **email sign-in** you get a mail with a **[Log in] button**. The link expires after 10 minutes.
   - Open that button **in the same browser** that shows the sign-in page, and you are signed in right away.
   - If you open it in a different browser or inside a mail app, you get **"Continue with verification code"** and a **6-digit number**.
     Enter that number **on the original sign-in page in your browser**. It is not the code this app asks for.
5. On the **approval page**, check that the right account is signed in and press **[Approve]**.
6. The last page shows a **long authentication code**. Copy it, paste it into the app and press **[Finish]**.
   If the code is on your clipboard when you come back to the app window, it is filled in for you.
7. The account is added to the list, and the app asks whether to switch to it now.

**There are two different codes**

| When it appears | What it looks like | Where it goes |
|---|---|---|
| You opened the email sign-in link in another browser | 6-digit number (e.g. `294963`) | The sign-in page **in your browser** |
| The last page after you approve | A long string (dozens of characters) | The code box **in this app** |

If you paste the 6-digit number into the app, it recognises it and tells you to enter it in the browser instead.

**Your current account is untouched while you add another.** The sign-in happens in a temporary folder
(`CLAUDE_CONFIG_DIR`); the app copies the tokens out and deletes the folder. The account only changes when you press **[Switch]**.

**Troubleshooting**

| Symptom | Fix |
|---|---|
| The browser doesn't open | Click "Browser didn't open? Click here" in the window |
| The wrong account got approved | Your browser was signed in to claude.ai with another account. Sign out of claude.ai in the browser and start again from [Connect] |
| "The code is wrong or has expired" | A code works once and only for a few minutes. Start again from [Connect] |
| "No response" (45 s) | Network or Claude's servers were slow. Start again from [Connect] |
| The `claude` command can't be found | Install Claude Code and check that `claude --version` runs in a terminal |

An account Claude Code is already signed in to doesn't need to sign in again:
press **[Save this account]** in the yellow bar at the top of the window.

### Switch

- Select an account and press **[Switch]** (or double-click the account)
- Or right-click the tray icon → account name

After switching you **don't need to restart VS Code.** Open conversations use the new account from their next request.
Type `/usage` (or `/status`) in a conversation to see which account it is on.
If it still shows the previous account, run `Ctrl+Shift+P` → **Developer: Reload Window**.

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
- Open conversations picking up the new account right away was observed in the VS Code extension (October 2026). It may differ between Claude Code versions, so check once with `/usage` after switching.
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
