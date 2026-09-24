# Quickstart — gregMod.MoreModules

> gregMod.MoreModules** adds faster, color-coded QSFP modules to the Data Center shop. Modules use the vanilla QSFP+ form factor and persist when installed in swi

Repo: [https://github.com/mleem97/gregMod.MoreModules](https://github.com/mleem97/gregMod.MoreModules) · Version: `0.1.0` · License: Apache-2.0.

## 1. Clone

```bash
git clone git@github.com:mleem97/gregMod.MoreModules.git
cd gregMod.MoreModules
```

## 2. Build / Run

Choose **one** path depending on the tech stack:

```bash
# .NET
dotnet build -c Release
dotnet run --project src/

# Node / pnpm
pnpm install
pnpm build
pnpm start

# Python
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
python -m <modul>
```

## 3. Test

```bash
dotnet test            # .NET
pnpm test              # Node
pytest                 # Python
```

Details are in [README.md](README.md) and [docs/INDEX.md](docs/INDEX.md).
If you run into problems: open an issue ([Issues](https://github.com/mleem97/gregMod.MoreModules/issues)) or read [CONTRIBUTING.md](CONTRIBUTING.md).
