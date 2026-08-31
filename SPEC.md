# Cursor Usage — specification

Hand this file to a coding agent with: **Implement this specification on Windows 10.**

This is the product contract, not a chat log. Follow the decisions below. Do not resurrect rejected ideas from the “Out of scope” section. Do not implement items in “Later”.

## Goal

A tiny Windows 10 desktop utility that lives in the **notification area** (system tray). It shows **current Cursor included-usage** the way Task Manager’s tray icon shows current CPU: a compact live sparkline, not a history chart.

The number the user is guarding is the **Included in Pro** percentage on [cursor.com/dashboard/spending#included-in-pro](https://cursor.com/dashboard/spending#included-in-pro). At 100% with on-demand disabled, Cursor rate-limits until the billing cycle resets. The app exists so that percentage is visible without opening a browser.

There is no main window. Left click **toggles** a small dashboard. Right click is a native menu.

| Item | Value |
|---|---|
| Product name | Cursor Usage |
| Assembly / EXE name | `CursorUsage.exe` |
| Config / install folder | `%LocalAppData%\CursorUsage\` (stable, never localized) |
| Author | Martin Sladek |
| Website | https://www.martinsladek.com/ |
| Repository | https://github.com/martinsladek/cursor-usage |
| Download (immutable) | https://github.com/martinsladek/cursor-usage/releases/latest/download/CursorUsage.exe |

v1 targets a **personal Pro** plan ($20/mo) with **on-demand usage disabled**. The monthly subscription is the only money that leaves the account. Tokens on the Usage page are a utilization detail, not a second bill.

## What the dashboard numbers mean

Cursor’s UI uses the same words for different things. This app follows **Spending %**, not Usage $.

| Surface | What it is | Does it stop you? | Does it charge extra? |
|---|---|---|---|
| Plan price (Pro $20/mo) | The subscription | No | Yes — that is the bill |
| Usage page token table and “spend” $ | Retail-value estimate of tokens consumed | No | No, while on-demand is off. The $ can exceed $20. |
| **Spending → Included in Pro %** | The real monthly quota | **Yes — at 100%** | No, while on-demand is off (you get rate-limited) |
| Spending → On-demand usage | Pay-as-you-go after the included quota | Only if enabled | Yes, if enabled. **v1 does not treat this as the product.** |

Included usage is two pools that reset with the billing cycle (unused does not roll over):

- **Cursor Models** (dashboard “Auto”): Cursor Grok and Composer. API field `autoPercentUsed`.
- **Other Models** (dashboard “API”): third-party models at provider prices. API field `apiPercentUsed`.

The headline bar **Included in Pro** is `totalPercentUsed`. That is the glance value. Model choice changes how fast the bar climbs. Tokens are how usage is metered into those pools; they are not an infinite free pass, and they are not a separate hard cap on the Usage page.

When on-demand is **disabled** and Included in Pro hits **100%**: the editor warns, further included usage is rate-limited, **no extra charge**. Enabling on-demand is a Cursor dashboard setting; this app must not turn it on.

## Data source

### Auth — logged-in Cursor on this PC

No API key in Settings. No cookie pasted as the normal path.

Read the session Cursor already has:

- `%APPDATA%\Cursor\User\globalStorage\state.vscdb`
- `ItemTable` key `cursorAuth/accessToken` (a WorkOS JWT)
- Cookie `WorkosCursorSessionToken` = `{jwt.sub}%3A%3A{accessToken}` (the `::` is URL-encoded)

Do **not** copy that token into `config.json`. Read it when a request is made. If the file is missing, locked, or the JWT is expired: grey icon, tooltip and balloon tell the user to sign in to Cursor.

POST calls to `cursor.com` need `Origin: https://cursor.com` (CSRF). GET `usage-summary` does not.

These dashboard endpoints are **unofficial**. They are what [cursor.com/dashboard/usage](https://cursor.com/dashboard/usage) and [cursor.com/dashboard/spending](https://cursor.com/dashboard/spending) already call. They can change. Parse defensively; a missing field is not a silent zero for the headline percentage.

### Endpoints (v1)

| Call | Role |
|---|---|
| `GET https://cursor.com/api/usage-summary` | **Primary.** Billing cycle, `membershipType`, `individualUsage.plan.totalPercentUsed` / `autoPercentUsed` / `apiPercentUsed`, `individualUsage.onDemand.enabled` |
| `POST https://cursor.com/api/dashboard/get-filtered-usage-events` | Recent events for the sparkline (activity now) and optional token totals in the dashboard. Body: `startDate` / `endDate` as **epoch milliseconds strings**. Newest-first. Paginate until the previous watermark, then save the watermark only after the full delta is stored. |
| `POST https://cursor.com/api/dashboard/get-current-period-usage` | Optional extra shape of the same cycle percentages if `usage-summary` is incomplete. Same cookie + Origin. |

Do not call the official Team Admin API in v1 (`api.cursor.com/teams/…`). There is no team admin key on this machine.

### Freshness

Events are **not** live CPU. Independent measurement of `get-filtered-usage-events`: median lag ~70 s, tail under ~10 minutes. The sparkline is “recent billed activity”, not the in-flight request.

Poll:

- **~60 s** while the last successful fetch showed activity, or the dashboard is open
- **~5 min** when idle (no new events)
- `usage-summary` on the same cadence — that is the Included % the user is watching

Do not poll every second. Do not hit the network from the icon-paint path.

Cache events under `%LocalAppData%\CursorUsage\` so a restart does not refetch the whole cycle. Deduplicate; the API has no stable request id — key on `(timestamp, model, tokenUsage, chargedCents)`.

If `cursor.com` is unreachable: keep the last good snapshot, grey or last-known icon, tooltip `Usage data unavailable`. Do not invent percentages.

## Behavior

### Icon

Draw a 32×32 tray icon at runtime (GDI+). Do not ship third-party icon assets. This is the Task Manager notification-area graph: **a short sparkline of current utilization**, not a history plot and not a digit-in-a-circle like Departures.

**Sparkline (what “now” looks like)**

- ~16 vertical bars, one per recent poll bucket (~1 minute each → about the last 15–20 minutes).
- Bar height = included-usage **burn in that bucket** (sum of included/plan consumption from new events, or the delta of `totalPercentUsed` if events are empty). Idle = empty/low bars. A heavy agent = tall bars.
- This is a moving window of the present, like Task Manager CPU. It is not a daily/weekly chart.

**Color (how full the month is)**

The sparkline (and a quiet background) use the **Included in Pro** `totalPercentUsed` the user must not exceed:

| Included % | Color | Meaning |
|---|---|---|
| Unknown / signed out / error | Grey (`#707070`) | No trustworthy number |
| 0–69% | Windows blue (`#0078D7`) | Comfortable |
| 70–89% | Amber | Getting close |
| 90–99% | Red-orange | Almost exhausted |
| 100% | Red fill, bars full | Quota exhausted this cycle |

If on-demand is enabled (unusual for v1’s owner): keep the same % color; the dashboard shows a one-line notice. Do not switch the icon into a dollar meter.

Do not overlay a history chart. Do not overlay a 3-digit percentage on the 16px tray; that number belongs in the tooltip and the dashboard.

### Hover

Native tooltip only. No menu on hover. `NotifyIcon.Text` is limited to 63 characters — truncate.

Examples:

- `Included 23% · Auto 18% · API 4%`
- `Included 91% · resets 12 Sep`
- `Included 100% · rate limited`
- `Sign in to Cursor`
- `Usage data unavailable`

### Left click — dashboard

Toggle the mini-dashboard. If it is closed, open it. If it is already open, close it. Do not use a balloon for the main fact — the percentage and the two pools need a small window.

The same physical click that dismisses the dashboard by stealing focus must not reopen it. Ignore a left click for ~250 ms after the dashboard closes.

Do not open a second copy. Refresh stays on the right-click menu, not on a second left click.

### Right click — native context menu

```
   Refresh
   Open Cursor spending
─────────────────
   Start with Windows  ✓
─────────────────
   About
   Exit
```

- **Refresh** — fetch now; ignore if a fetch is already running.
- **Open Cursor spending** — `https://cursor.com/dashboard/spending#included-in-pro` in the default browser.
- **Start with Windows** — checkable; see Autostart. Read live registry state when the menu opens.
- **About** immediately above **Exit**. Opening About again activates the existing dialog.
- **Exit** hides the tray icon and quits.

No separate Settings window in v1. No flyout *instead of* this menu — the dashboard is left click only.

### Mini-dashboard

A small tool window (`FixedToolWindow`), not in the taskbar, no maximize/minimize.

Place it next to the pointer **before the first paint** (the tray icon is where the user clicked). Do not show it at (0,0) and then jump. If it would leave the working area, clamp it. If the tray rect is unavailable, the pointer is the fallback.

Close on:

- Escape
- the window’s close box
- a second left click on the tray icon
- losing activation to another window (click outside)

Losing activation does **not** always happen. Clicks on the notification area, another tray icon, or the taskbar often leave this tool window active. That is a Windows limitation, not a second close rule. The tray toggle covers “I clicked the icon again.”

Separate the visual chapters (headline, two pools, sparkline, tokens, on-demand) with one line of empty space (`MessageBox` font height), not a hairline. On-demand uses that same gap before and after.

Contents, top to bottom — compact, not a website:

1. **Headline:** `Included in Pro` as a large percentage (`totalPercentUsed`, one decimal if < 10%, otherwise whole percent). Subline: plan name (`Pro`) and `Resets {date}` from `billingCycleEnd`.
2. **Two pool bars:** Cursor Models / Auto (`autoPercentUsed`) and Other Models / API (`apiPercentUsed`). Label them in the user’s language; the values stay `%`.
3. **The same sparkline as the icon**, larger (recent ~15–20 min burn). Caption: recent included usage. If data may lag, one short line: usage can lag by several minutes.
4. **Tokens (secondary):** heading `Tokens (recent)`, then input, output, and cache each on its own line. Utilization, not a bill. Do not present a Usage-page dollar total as a limit.
5. **On-demand:** if `onDemand.enabled` is false, a quiet `On-demand off` (no extra charges). If true, a notice that extra spend is possible — still do not build an on-demand dollar product in v1.

A text link **Open spending dashboard** to the same URL as the menu item.

No day-by-day history chart in v1. No CSV export.

### About dialog

Standard modal WinForms dialog (`FixedDialog`, no maximize/minimize, not in the taskbar). Product name stays English. Tagline, credit, and link labels come from `Strings.cs` for the current UI language.

English canonical copy:

> A lightweight Windows desktop utility that shows current Cursor included usage in the notification area.
>
> Developed by Martin Sladek with the help of AI models and workflows.

Clickable links (labels are localized; URLs are not):

| Role | URL |
|---|---|
| Website | https://www.martinsladek.com/ |
| GitHub | https://github.com/martinsladek/cursor-usage |

OK closes the dialog. Product name, GitHub, and OK stay untranslated.

### Language

Read `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName`. UI strings live in `Strings.cs`. Supported:

`en` (default), `cs`

Any other Windows language falls back to English. Product name stays English. README, SPEC, and GitHub stay English only.

## Persistence

```
%LocalAppData%\CursorUsage\CursorUsage.exe
%LocalAppData%\CursorUsage\config.json
%LocalAppData%\CursorUsage\cache\
```

`config.json` in v1 may be empty `{}` or hold only non-secret preferences later. Do not store session cookies, JWTs, or API keys.

The event cache is not config. It may be deleted; the next fetch rebuilds it.

Do not use Roaming: the EXE is large, and the cache is machine-local.

## Autostart

Optional, off by default. No admin rights. Portable until the user opts in.

**Enable**

1. Copy the currently running EXE to `%LocalAppData%\CursorUsage\CursorUsage.exe` (skip if already running from that path).
2. Write `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `CursorUsage` = quoted path to that copy.
3. Write `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run` value `CursorUsage` as enabled (`0x02…`), so Task Manager / Settings → Apps → Startup agree.

**Disable**

1. Delete the Run value and the StartupApproved value. Autostart is off immediately. Keep `config.json` and cache.
2. Delete the installed EXE now if this process is **not** that file.
3. If this process **is** the installed EXE, mark it for deletion and remove it ~1s after Exit via `cmd timeout & del`. Re-checking Start with Windows before Exit cancels that deletion.

On a later portable launch, if Run is not registered, delete any leftover installed EXE.

**Checkbox state**

Checked only if the Run value exists **and** StartupApproved does not mark it disabled (`0x03` / `0x07`). Task Manager “Disable” leaves Run in place; the menu must not show checked in that case. Checking the box again re-enables Approved.

**Updates**

On launch, if Run is registered and this process is a different file than the installed copy (size or last-write), overwrite the LocalAppData EXE so the next logon is not an old download.

## Process

Single instance via mutex `Local\CursorUsage.SingleInstance`. A second launch exits silently.

## Technical stack (required)

| Layer | Choice |
|---|---|
| Language | C# |
| UI | WinForms, `ApplicationContext` + `NotifyIcon` (no main form) |
| Target | `net8.0-windows10.0.19041.0` |
| Output | `WinExe`, self-contained single-file `win-x64` |
| HTTP | `HttpClient` to `cursor.com`. No third-party Cursor SDK. |

Publish:

```powershell
dotnet publish src/CursorUsage/CursorUsage.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
```

The published EXE must run on a PC that has no .NET SDK and no extra runtimes installed.

Do not use trimming (`PublishTrimmed=false`).

No installer. Distribution of the binary is **GitHub Releases**, never git. A Release is created by GitHub Actions on `windows-latest` when a tag matching `v*` is pushed (`CursorUsage.exe` asset). After a public repository exists, `/releases/latest/download/CursorUsage.exe` points at that Release.

## Suggested layout

```
src/CursorUsage/
  CursorUsage.csproj
  app.manifest
  Program.cs
  Strings.cs
  AppConfig.cs
  AppPaths.cs
  Autostart.cs
  TrayApplicationContext.cs
  TrayIcons.cs
  TrayDialog.cs
  AboutForm.cs
  DashboardForm.cs
  CursorAuth.cs
  UsageClient.cs
  UsageCache.cs
  Sparkline.cs
```

`.gitignore`: `bin/`, `obj/`, `dist/`, `.vs/`, `*.user`, `cache/`.

README is English only. The human download link is the `/releases/latest/download/CursorUsage.exe` URL above.

## Later (do not implement in v1)

These are parked on purpose. Do not build them now. Do not delete this section; it is the holding place for the next iteration.

- **On-demand dollar meter** — if the user later enables on-demand, a Task Manager-style sparkline of **charged spend** (`chargedCents`) and a dashboard of extra USD. That was the first design. v1 only notices that on-demand is off.
- **Official Team Admin API** (`POST /teams/spend`, `POST /teams/filtered-usage-events`, Basic auth with an admin key). Hourly aggregation, team/enterprise only. This machine has no admin key. Revisit if a team plan appears.
- Day/week history charts, alerts at 70/90/100%, cookie paste as a Settings field, multiple Cursor accounts.

## Out of scope (do not implement)

These were considered and rejected for this product:

- Treating Usage-page token $ or `totalSpend` cents as the quota (they are not)
- Turning on-demand on from this app
- Digit-in-a-circle as the only icon (Departures-style) instead of the Task Manager sparkline
- A custom tray flyout **instead of** the native right-click menu
- Pairing this app to a cloud proxy or a local MITM of Cursor traffic for “true realtime”
- Reading chat transcripts / `state.vscdb` bubbles as billed cost (often zero; not accounting)
- Java, Python, Node, C++ toolchains
- Putting the self-contained EXE into git
- Installer

No administrator rights are required.

## Definition of done

- Tray icon: Task Manager-style sparkline of recent included-usage burn; color follows Included in Pro %
- Tooltip with Included / Auto / API % (truncated to 63 characters)
- Left click: toggle the mini-dashboard (open if closed, close if open); headline Included %, two pool bars, larger sparkline, secondary tokens, on-demand off/on notice
- Right click: Refresh, Open Cursor spending, Start with Windows, About, Exit
- Auth from the local Cursor session; no key in `config.json`
- Grey signed-out / unreachable states
- About dialog with website and GitHub
- UI localized for `en` and `cs`
- Optional Start with Windows via HKCU Run + StartupApproved, EXE copy only when enabled
- Self-contained `dist/CursorUsage.exe` builds and runs without a local SDK
- Source in git; `dist/` and session tokens not in git
