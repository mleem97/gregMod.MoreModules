# gregMod.MoreModules

> Adds faster, color-coded QSFP modules to **Data Center**.

[![Discord](https://img.shields.io/badge/Discord-Join-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://discord.gg/greg)
[![gregFramework](https://img.shields.io/badge/gregFramework-Website-blue?style=for-the-badge)](https://gregframework.eu)
[![License](https://img.shields.io/badge/License-Apache%202.0-green?style=for-the-badge)](./LICENSE)
[![Version](https://img.shields.io/badge/Version-1.0.12-orange?style=for-the-badge)]()
[![GameVersion](https://img.shields.io/badge/Game%20Version-1.1.0-yellow?style=for-the-badge)]()
[![Unity](https://img.shields.io/badge/Unity-6000.4.12f1-black?style=for-the-badge&logo=unity&logoColor=white)]()

## Links

- **Repository:** [github.com/mleem97/gregMod.MoreModules](https://github.com/mleem97/gregMod.MoreModules)
- **Discord / Support:** [discord.gg/greg](https://discord.gg/greg)
- **Website:** [gregframework.eu](https://gregframework.eu)

## Overview

**gregMod.MoreModules** adds faster, color-coded QSFP modules to the Data Center shop. Modules use the vanilla QSFP+ form factor and persist when installed in switch ports.

## Available Modules

| Module             | Speed      | Color   |
|--------------------|------------|---------|
| QSFP28 100Gbps     | 100 Gbps   | Green   |
| QSFP56 200Gbps     | 200 Gbps   | Orange  |
| QSFP-DD 400Gbps    | 400 Gbps   | Yellow  |
| QSFP-DD 800Gbps    | 800 Gbps   | Red     |
| QSFP-DWDM 1600Gbps | 1600 Gbps  | Purple  |
| QSFP-DWDM 3200Gbps | 3200 Gbps  | Magenta |
| QSFP-DWDM 6400Gbps | 6400 Gbps  | Cyan    |

## Features

- Buyable from the **shop** like any vanilla module
- Fully compatible with QSFP+ switch ports
- 32 module boxes available in the shop
- **Color-coded modules** — each tier gets its own tint so you can tell them apart at a glance
- **Save/load persistent** — modules installed in switch ports survive game restarts

## Dependencies

- [MelonLoader](https://melonwiki.xyz/) v0.7.2 or newer

## Installation

1. Install MelonLoader for Data Center if you haven't already
2. Copy `gregMod.MoreModules.dll` into `Data Center/Mods/`
3. Launch the game — the module appears in the shop immediately

## Notes

- Modules use the QSFP+ form factor — they fit any port that accepts a vanilla QSFP+ 40Gbps module

## Build from Source

Requirements:

- .NET 6 SDK
- local Data Center / MelonLoader installation

```bash
dotnet build -c Release
```

Release output: `bin/x64/Release/net6.0/gregMod.MoreModules.dll`

## Project Structure

```
gregMod.MoreModules/
├── src/
│   ├── Core.cs             # MelonLoader entry point and shop integration
│   ├── ModuleDefinition.cs # Custom module definitions
│   ├── ModuleRegistry.cs   # Runtime prefab registry
│   └── Patches.cs          # Game Harmony patches
├── references/             # Current game and MelonLoader assemblies
├── gregMod.MoreModules.csproj
└── README.md
```

## Credits

- Original implementation: [leoms1408](https://github.com/leoms1408)
- gregMod rebranding and current game update: [TeamGreg Modding](https://github.com/teamGregModding)

## License

See the project source and original distribution terms before redistribution.

## 🚀 Join the gregFramework Team!

Contributions, testing, documentation, and feedback are welcome in the [greg Discord](https://discord.gg/greg).
