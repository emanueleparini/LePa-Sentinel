# Contributing to LePa Sentinel

Thanks for your interest! Bug reports, ideas and pull requests are welcome.

## Reporting a bug

Open an issue with:
- Windows version, LePa Sentinel version (tray menu → About) and how it was installed (Store / installer / portable);
- how you run Claude Code (terminal, VS Code extension, other);
- what you expected and what happened. For hook problems, set the environment variable `LEPA_SENTINEL_DEBUG=1`, reproduce the problem and attach `%TEMP%\lepa-sentinel-hook.log`.

Please do not paste prompts, transcripts or paths you consider private.

## Development

```powershell
dotnet run --project src
```

Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) first. Some rules the code relies on:

- **Hook mode** (`--hook`) runs on every Claude Code event: keep it fast, silent (no stdout) and exception-free, and do not touch WPF from it.
- `HookInstaller` must only add/remove LePa Sentinel's own hooks and preserve everything else in the user's `settings.json`.
- UI strings go in `src/L.cs`, in both Italian and English.
- Close the running app before `./build.ps1` or `dotnet publish`: the executable is locked while it runs.

## Pull requests

- One topic per pull request, with a short description of what changes for the user.
- Match the style of the surrounding code.
- Check it builds (`dotnet build src -c Release`) and try the change in the real app with a Claude Code session.

By contributing you agree that your contribution is licensed under the [MIT License](LICENSE).
