# Changelog

All notable changes to ChatCheatMonitor are documented here. The project follows Semantic Versioning.

## [Unreleased]

## [2.0.0] - 2026-08-25

### Added

- Private, public, both and disabled response modes
- Server-wide and per-player cooldowns
- Cheat, exploit and glitch detection categories
- Whole-word, substring and timeout-protected regex matching
- Colour, punctuation, Unicode and leetspeak normalization
- Exclusion phrases and report-command suppression
- Online target-name assistance
- Localized reminder templates and server-specific overrides
- Optional repeated-accusation staff alerts
- Privacy-friendly aggregate statistics
- IW4MAdmin Admin-navigation webfront dashboard
- `ccmstatus`, `ccmstats`, `ccmtest` and `ccmreload` commands
- Configuration validation and hot reload
- Automated build, test and tagged-release workflows
- xUnit v3 tests
- Report-informed community phrase and exclusion lists inherited by existing v2 configurations

### Changed

- Updated to .NET 10 and `RaidMax.IW4MAdmin.SharedLibraryCore` 2026.1.6.1
- Reminders now use IW4MAdmin's native messaging events instead of raw RCON commands
- Plugin version is read from assembly metadata
- ChatCheatMonitor is announced in the IW4MAdmin startup console loaded list
- The webfront dashboard shows the IW4MAdmin server hostname and game code alongside its endpoint
- Default cheating detection covers high-signal aimbot and wall-information terminology derived from historical community reports
- Ordinary anti-cheat discussion is excluded from the default rules

### Fixed

- Cooldown state is thread-safe, scoped by server and client, pruned over time and removed on disconnect
- Event subscriptions are released when the plugin unloads
- Whole-word matching avoids common substring false positives

## [1.0.0] - 2025-12-14

- Initial public release

[Unreleased]: https://github.com/OllyMc27/ChatCheatMonitor/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/OllyMc27/ChatCheatMonitor/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/OllyMc27/ChatCheatMonitor/releases/tag/v1.0.0
