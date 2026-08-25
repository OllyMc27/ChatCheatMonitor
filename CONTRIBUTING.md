# Contributing

Contributions and focused bug reports are welcome.

## Development setup

1. Install the .NET 10 SDK.
2. Clone this repository.
3. Open `ChatCheatMonitor.slnx` in Visual Studio, or use the .NET CLI.
4. Run `dotnet restore`, `dotnet build` and `dotnet test`.

For runtime debugging, place the built DLL in the `Plugins` folder of a current IW4MAdmin development installation.

## Pull requests

- Keep ChatCheatMonitor non-punitive.
- Do not add raw-chat persistence or external telemetry.
- Add tests for matching, cooldown or configuration changes.
- Keep regex execution bounded by a timeout.
- Preserve event unsubscription and interaction cleanup.
- Update the changelog for user-visible behaviour.

## Commit and release conventions

Use clear, imperative commit subjects. Releases use Semantic Versioning tags such as `v2.1.0`; the release workflow applies the tag version to the assembly and generates GitHub release notes.
