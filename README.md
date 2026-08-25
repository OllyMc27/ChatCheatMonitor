# ChatCheatMonitor

[![CI](https://github.com/OllyMc27/ChatCheatMonitor/actions/workflows/ci.yml/badge.svg)](https://github.com/OllyMc27/ChatCheatMonitor/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/OllyMc27/ChatCheatMonitor)](https://github.com/OllyMc27/ChatCheatMonitor/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

ChatCheatMonitor is a non-punitive [IW4MAdmin](https://github.com/RaidMax/IW4M-Admin) plugin that turns cheat, exploit and glitch accusations in player chat into clear reporting guidance.

It does not punish players and does not retain raw chat. By default, it privately reminds the sender how to use `!rep`, avoiding extra server-wide noise.

## Highlights

- Whole-word phrase matching with colour, punctuation, Unicode and optional leetspeak normalization
- Report-informed defaults covering common aimbot, wall-information, ESP, chams and red-box terminology
- Configurable phrase and timeout-protected regex rules grouped into detection categories
- Exclusion phrases and automatic suppression of messages that already use the report command
- Private, public, both or disabled response modes
- Per-player and server-wide cooldowns that are safe under concurrent events
- Optional online-player name detection for more useful report instructions
- Localized response templates and per-server overrides
- Optional threshold-based staff alerts, disabled by default
- Privacy-friendly in-memory statistics with no raw chat retention
- IW4MAdmin webfront dashboard under the authorised Admin navigation
- Moderator commands for status, statistics and rule testing
- Automatic configuration hot reload
- .NET 10 and current IW4MAdmin `IPluginV2` lifecycle support

## Requirements

- A current IW4MAdmin build using .NET 10
- The .NET 10 ASP.NET Core Runtime/Hosting Bundle on the IW4MAdmin host

## Installation

1. Download `ChatCheatMonitor.dll` from the [latest GitHub release](https://github.com/OllyMc27/ChatCheatMonitor/releases/latest).
2. Copy it to the IW4MAdmin `Plugins` directory.
3. Start or restart IW4MAdmin.
4. Edit the generated `Configuration/ChatCheatMonitor.json` file if needed.

Configuration changes are watched and applied automatically. `!ccmreload` is available to rebuild the compiled rule cache explicitly.

Existing v1 `CheatPhrases`, `ReminderMessage` and `AlertCooldownSeconds` settings are recognised and mapped in memory for compatibility. Replace the old file with the v2 structure when convenient so all new options are explicit.

## Default behaviour

- Detection categories: cheating, exploiting and glitching
- Response: private message to the sender
- Player cooldown: 45 seconds
- Server-wide public-message cooldown: 20 seconds
- Target assistance: enabled
- Staff alerts: disabled
- Webfront dashboard: enabled for Moderator and above
- Statistics: in memory only; raw chat is never stored

A complete editable example is available at [examples/ChatCheatMonitor.json](examples/ChatCheatMonitor.json).

## Configuration concepts

### Response modes

| Value | Behaviour |
| --- | --- |
| `Disabled` | Detecting and responding are disabled for the applicable server |
| `Private` | Only the sender receives the reminder |
| `Public` | The server receives a broadcast, subject to the server cooldown |
| `Both` | The sender receives a private reminder and the server receives a rate-limited broadcast |

### Template fields

Reminder messages support:

- `{target}` — the uniquely identified online player, or `<player>`
- `{player}` — the player who sent the accusation
- `{category}` — the matched category name
- `{reportCommand}` — the configured report command
- `{cooldown}` — the effective per-player cooldown in seconds

Staff alert messages additionally support `{count}` and `{window}`.

### Matching

Plain phrases use whole-word matching by default, preventing a rule such as `hack` from matching `hackathon`. Set a category's `MatchMode` to `Substring` only when that behaviour is intentional.

`CommunityReportPhrases` contains the high-signal shorthand and variants derived from historical community reports. `CommunityReportExclusions` prevents ordinary anti-cheat discussion from triggering those rules. These lists remain active for existing v2 configurations that predate the fields; edit or empty either list to tune them for your community.

Regex rules use case-insensitive, culture-invariant matching with a 100 ms timeout. Invalid expressions are skipped and displayed as configuration errors in logs and the webfront dashboard.

### Server overrides

`ServerOverrides` keys use the IW4MAdmin server ID, normally `ip:port`. The special key `*` acts as a fallback. Exact server entries win over `*`.

Overrides can change enabled state, response mode, report command, language, cooldowns and additional exclusion phrases.

## Commands

| Command | Permission | Purpose |
| --- | --- | --- |
| `!ccmstatus` | Moderator | Show enabled state, mode, counters and configuration issue count |
| `!ccmstats` | Moderator | Show aggregate statistics |
| `!ccmstats reset` | SeniorAdmin | Reset in-memory statistics |
| `!ccmtest <message>` | Moderator | Preview the matching category, rule and resolved target |
| `!ccmreload` | SeniorAdmin | Revalidate configuration and rebuild rule caches |

Aliases are `!ccms`, `!ccmst`, `!ccmt` and `!ccmr`.

## Webfront integration

The plugin registers the native `Webfront::Nav::Admin::ChatCheatMonitor` interaction. IW4MAdmin automatically:

- places it in the Admin navigation;
- enforces the built-in `Interaction.Read` webfront policy;
- applies the configured minimum IW4MAdmin permission;
- renders the dashboard through its standard authenticated interaction page.

The dashboard shows aggregate activity, category/server breakdowns, recent non-content outcomes and configuration validation. It deliberately does not expose raw player messages.

## Privacy and enforcement

ChatCheatMonitor is an education and reporting helper, not an anti-cheat. It:

- does not ban, kick, warn or mute players;
- does not treat an accusation as proof;
- does not persist raw chat;
- keeps counters only for the current IW4MAdmin process;
- leaves staff alerts off unless an administrator explicitly enables them.

## Development

Open `ChatCheatMonitor.slnx` in a current Visual Studio installation or use:

```powershell
dotnet restore ChatCheatMonitor.slnx
dotnet build ChatCheatMonitor.slnx --configuration Release
dotnet run --project ChatCheatMonitor.Tests/ChatCheatMonitor.Tests.csproj --configuration Release
```

The test project uses xUnit v3 and Microsoft Testing Platform. Tagged releases matching `v*` are compiled with the tag version and published automatically.

## License

ChatCheatMonitor is available under the [MIT License](LICENSE).
