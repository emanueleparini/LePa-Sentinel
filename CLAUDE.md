# LePa Sentinel — notes for Claude

Architecture, states, hooks, files and build are documented in @docs/ARCHITECTURE.md: read it before changing anything. Contribution rules are in CONTRIBUTING.md.

- The maintainers speak Italian: reply in Italian unless asked otherwise. UI strings live in `src/L.cs` in Italian and English, so always add both.
- Hook mode (`--hook`) runs on every Claude Code event: keep it fast, silent (no stdout), exception-free, and free of WPF.
- `HookInstaller` must only add/remove our own hooks and preserve the rest of the user's `settings.json`.
- Close the running app (`Stop-Process -Name LePaSentinel`) before `./build.ps1` or `dotnet publish`: the exe is locked while it runs.
- To check the UI, use the demo-session procedure in docs/ARCHITECTURE.md → "Testing tips". Remove the demo files afterwards.
- Keep README.md, README.it.md and docs/ARCHITECTURE.md up to date when behaviour changes.
- Release: raise `<Version>` in `src/LePaSentinel.csproj`, then run `./build.ps1 -Sign -Msix`. Signing needs `az login` and the "Artifact Signing Certificate Profile Signer" role. The Store steps are in packaging/store/STORE.md.
