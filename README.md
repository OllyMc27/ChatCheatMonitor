# ChatCheatMonitor

[![CI](https://github.com/OllyMc27/ChatCheatMonitor/actions/workflows/ci.yml/badge.svg)](https://github.com/OllyMc27/ChatCheatMonitor/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/OllyMc27/ChatCheatMonitor)](https://github.com/OllyMc27/ChatCheatMonitor/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

ChatCheatMonitor is a non-punitive [IW4MAdmin](https://github.com/RaidMax/IW4M-Admin) plugin that detects cheat, exploit and glitch accusations in player chat and reminds players to use the server's report command.

It never punishes players or stores raw chat. By default, reminders are sent privately to avoid adding more noise to the server.

## Features

- Detects common cheating, exploiting and glitching phrases, including community-informed variants
- Handles colours, punctuation, Unicode and optional leetspeak normalization
- Supports whole-word, substring and timeout-protected regex rules
- Avoids repeat messages with per-player and server-wide cooldowns
- Can identify an online target and include their name in the reminder
- Provides private, public, both and disabled response modes
- Supports localized messages, per-server overrides and optional staff alerts
- Includes an authenticated IW4MAdmin webfront dashboard with privacy-friendly statistics
- Reloads valid configuration changes automatically

## Requirements

- A current IW4MAdmin build using .NET 10
- The .NET 10 ASP.NET Core Runtime/Hosting Bundle on the IW4MAdmin host

## Installation

1. Download `ChatCheatMonitor.dll` from the [latest release](https://github.com/OllyMc27/ChatCheatMonitor/releases/latest).
2. Copy it into the IW4MAdmin `Plugins` directory.
3. Restart IW4MAdmin.
4. Adjust `Configuration/ChatCheatMonitor.json` if required.

The default configuration sends a private reminder, uses a 45-second player cooldown and keeps staff alerts disabled. The webfront dashboard is available to Moderator-level users and above.

See [examples/ChatCheatMonitor.json](examples/ChatCheatMonitor.json) for a complete configuration containing detection categories, response modes, message templates, cooldowns, exclusions and server overrides.

Existing v1 configuration keys are recognised for compatibility, although replacing the old file with the v2 structure is recommended when convenient.

## Commands

| Command | Permission | Purpose |
| --- | --- | --- |
| `!ccmstatus` | Moderator | Show status, response mode, counters and configuration issues |
| `!ccmstats` | Moderator | Show aggregate detection statistics |
| `!ccmstats reset` | SeniorAdmin | Reset in-memory statistics |
| `!ccmtest <message>` | Moderator | Preview the matched category, rule and target |
| `!ccmreload` | SeniorAdmin | Revalidate the configuration and rebuild rule caches |

Aliases are `!ccms`, `!ccmst`, `!ccmt` and `!ccmr`.

## Webfront dashboard

The plugin adds **Chat Cheat Monitor** to IW4MAdmin's Admin navigation. It displays aggregate activity, server and category breakdowns, recent outcomes and configuration health without exposing chat messages.

## Privacy and enforcement

ChatCheatMonitor is a reporting helper, not an anti-cheat. It does not ban, kick, warn or mute players, and an accusation is never treated as proof. Raw chat is not retained; counters last only for the current IW4MAdmin process.

## License

ChatCheatMonitor is available under the [MIT License](LICENSE).
