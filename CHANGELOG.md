# Changelog — gregMod.MoreModules

Format: [Keep a Changelog](https://keepachangelog.com/de/1.0.0/). Version: see [`VERSION`](VERSION).

## [1.0.20] — 2026-09-24

### Changed

- English strings throughout.

## [Unreleased]

### Changed

- Sibling detection now only yields to gregMod.RealisticModules when its `Enabled=true` (v1.0.19).

### Added

- RJ45 10Gbps, SFP+ 10Gbps, SFP28 25Gbps as shop packages (5x + trays, dedicated prefabs/boxes/prices/sprites per form factor).
- Unified open-source layout (README, docs, badges) following the gregCore template.

### Fixed

- InsertSFP rewrite only for tagged box modules or unambiguous speed (protects vanilla RJ45/SFP+/SFP28 saves).

## [1.0.18] — 2026-09-23

### Fixed

- **Wrong shop prefabs/icons:** base-module detection only accepts vanilla `SFP_*` names (previously `CustomSFP_*` was picked as "highest speed" → `sfpType=0` RJ45, `Shop template: itemID=0`). Shop template now matches the Fibre 40G box (`itemID` / highest box ID), not the module prefabID.
- **Wrong delivery box:** `BuildBoxPrefab` uses `BaseBoxPrefabIndex` (vanilla box array) instead of `BasePrefabID` (module ID space — out of range → fallback RJ45 box).

## [1.0.17] — 2026-09-23

- Tray triple-spawn fix, scanner fixes (see `docs/CHANGELOG.md`).

## [1.0.16] — 2026-09-22

- Tray triple-spawn fix.

## [0.1.0] — 2026-09-22

- Initial standardized baseline.
