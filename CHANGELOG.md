# Changelog — gregMod.MoreModules

Format: [Keep a Changelog](https://keepachangelog.com/de/1.0.0/). Version: siehe [`VERSION`](VERSION).

## [Unreleased]

### Added

- RJ45 10Gbps, SFP+ 10Gbps, SFP28 25Gbps als Shop-Pakete (5x + Trays, eigene Prefabs/Boxen/Preise/Sprites je Formfaktor).
- Einheitliches Open-Source-Layout (README, Docs, Badges) nach gregCore-Vorbild.

### Fixed

- InsertSFP-Umschreibung nur noch bei getaggten Box-Modulen oder eindeutigem Speed (schützt Vanilla-RJ45/SFP+/SFP28-Saves).

## [1.0.18] — 2026-09-23

### Fixed

- **Falsche Shop-Prefabs/Icons:** Base-Modul-Erkennung akzeptiert nur Vanilla-`SFP_*`-Namen (vorher wurde `CustomSFP_*` als „höchste Geschwindigkeit" gewählt → `sfpType=0` RJ45, `Shop template: itemID=0`). Shop-Template matched jetzt die Fibre-40G-Box (`itemID` / höchste Box-ID), nicht die Modul-prefabID.
- **Falsche Liefer-Box:** `BuildBoxPrefab` nutzt `BaseBoxPrefabIndex` (Vanilla-Box-Array) statt `BasePrefabID` (Modul-ID-Raum — Out-of-Range → Fallback RJ45-Box).

## [1.0.17] — 2026-09-23

- Tray-Dreispawn-Fix, Scanner-Fixes (siehe `docs/CHANGELOG.md`).

## [1.0.16] — 2026-09-22

- Tray-Dreispawn-Fix.

## [0.1.0] — 2026-09-22

- Initialer standardisierter Stand.
