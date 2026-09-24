# AGENTS.md — Notes for AI agents (gregMod.MoreModules)

Repo: https://github.com/mleem97/gregMod.MoreModules · License: Apache-2.0 · Version: see `VERSION` (1.0.19).

MelonMod for Data Center. Adds extra module (device) variants via a
data-driven registry.

## Duties

1. **Read first:** `README.md`, `docs/INDEX.md`, `docs/ARCHITECTURE.md` — only then make changes.
2. **Do not commit secrets** (keys, tokens, `.env`). Use keys only via environment variables.
3. **Preserve history:** no `push --force`, no history rewrite without instruction.
4. **Verify changes:** before reporting done, build the mod (`dotnet build gregMod.MoreModules.csproj -c Release` or `./build.sh MoreModules` from `ModRepositories/`).
5. **Keep docs in sync:** for new features update `README.md` + `docs/` + `CHANGELOG.md` (Unreleased).
6. **Conventions:** Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:` …), one logical change per commit.
7. **When unsure:** stop and ask instead of guessing — especially for deletes, migrations, CI.

## Build and references

- Target: `net6.0`, x64. Game: Data Center (`MelonGame("Waseku", "Data Center")`).
- `references/` holds absolute symlinks into the Steam Data Center install.
  Never commit `references/*.dll`, `bin/`, or `obj/`.
- After a fresh clone, run `../tools/sync-melon-assemblies.sh`.
- Deploy only with `./build.sh MoreModules --deploy`.

## Hard rules

- New modules go through `ModuleRegistry` + `ModuleDefinition` (data-driven);
  never hard-code a variant into patches.
- **Never** touch gregCore types outside a soft-probe/JIT-split bridge — the
  mod must load without `gregCore.dll`.
- Catalog patches stay observational plus registry-driven injection; keep them
  in sync with the sibling MoreServers/MoreSpools/RealisticModules patterns.

## Layout

- `src/Core.cs` — MelonMod entry. `src/ModuleRegistry.cs`, `src/ModuleDefinition.cs` — registry.
- `src/Patches.cs` — Harmony patches.
