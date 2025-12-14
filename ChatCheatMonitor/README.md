[![Version](https://img.shields.io/github/v/release/OllyMc27/ChatCheatMonitor?label=version&style=flat-square)](https://github.com/OllyMc27/ChatCheatMonitor/releases)
# ChatCheatMonitor

A lightweight IW4MAdmin plugin that detects cheat-related phrases in player chat and reminds players how to properly report suspected cheaters.

Instead of staff manually explaining the report process over and over, ChatCheatMonitor automatically nudges players to use the correct `!rep` command — keeping chat cleaner and reports consistent.

---

## ✨ Features

- Detects common cheat-related phrases in player chat
- Sends a configurable in-game reminder message
- Per-player cooldown to prevent spam
- Fully configurable phrase list
- Optional debug logging
- Zero impact on gameplay or punishments

This plugin **does not log players**, **does not notify staff**, and **does not issue penalties**.  
It simply reminds players how to report properly.

---

## 🔧 Installation

1. Download the latest `ChatCheatMonitor.dll` from the **Releases** page
2. Place it into your IW4MAdmin `Plugins` folder e.g `<IW4MAdmin Root>/IW4MAdmin/Plugins/`
3. Start IW4MAdmin
4. A config file will be generated automatically:

---

## ⚙ Configuration

Example configuration:

```json
{
  "CheatPhrases": [
    "cheat",
    "cheater",
    "aimbot",
    "cheating",
    "hack",
    "hacker",
    "hacking",
    "hacks",
    "wallhack",
    "spinbot",
    "he is cheating"
  ],
  "ReminderMessage": "^1REMINDER:^3 If you believe a player is cheating, type ^1!rep <player> <reason>^3 to report it to the admins.",
  "AlertCooldownSeconds": 45,
  "Debug": false
}
```

## Configuration Options

| Setting | Description |
| ------ | ----------- |
| CheatPhrases | List of phrases that will trigger the reminder |
| ReminderMessage | In-game message sent to the server |
| AlertCooldownSeconds | Cooldown per player (seconds) |
| Debug | Enables debug logging |

---
