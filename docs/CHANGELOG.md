# Changelog

## v1.0.18

- **Fixed wrong shop prefabs / icons:** base-module detection only accepts
  vanilla `SFP_*` names (log had picked a pre-existing `CustomSFP_*` entry as
  “highest speed” → `sfpType=0` RJ45 and `Shop template: itemID=0`). Shop
  template now matches the Fibre 40G box `itemID` / highest box ID, not the
  module prefabID.
- **Fixed delivered box clone source:** `BuildBoxPrefab` uses a dedicated
  `BaseBoxPrefabIndex` (vanilla box array) instead of `BasePrefabID` (module
  ID space — out of range → fallback to RJ45 box).

## v1.0.17

- Diagnostic build (no behavior change): the full vanilla SFP module and SFP
  box catalogs are dumped to the log at startup (`=== Vanilla SFP module
  catalog ===`, `=== Vanilla SFPBox catalog ===`) so every custom tier can be
  mapped to its real vanilla module type (SFP / QSFP+ / QSFP28 / QSFP-DD /
  Fiber) with the correct sfpType — instead of shipping every tier as a clone
  of the same generic module.

## v1.0.16

- Fixed the tray box spawning three times per purchase (log showed three
  `Upgraded box` lines for one item): the manual `SpawnPhysicalItem` at
  add-to-cart created an unlinked extra box, checkout instantiated a second
  one via `GetPrefabForItem`, and the freshly built template itself remained
  active in the scene as a third. The add-time manual spawn is removed (cart
  entry only) and the tray/bulk templates returned by `GetPrefabForItem` are
  parked under the inactive `TemplateHolder`, so exactly one delivery box is
  instantiated and expanded.

## v1.0.15

- The box scanner stopped too early: once no un-upgraded box was visible it
  broke out, but the actual delivery spawns the tray box seconds later at
  checkout. The scanner now polls for a time window (≈12 s after the last box
  it found) and is additionally restarted on `ComputerShop.ButtonCheckOut`,
  so the delivered box really reaches its tray capacity.
- Dropped the `(Clone)` name requirement — tray/bulk boxes are matched purely
  by the `_tray_`/`_bulk_` marker (upgrades are idempotent per box).

## v1.0.14

- Fixed tray delivery not expanding the box: the parser in `GetTargetCapacity`
  failed on the Unity `(Clone)` name suffix (the capacity was never extracted,
  so boxes stayed at 5 slots). Bulk (32x) was unaffected as it never parses a
  number.
- Reset the box-scanner guard on scene load so an interrupted scan (scene
  switch mid-scan) can't block future expansions.

## v1.0.13

- Added tray packages per module: **16 / 36 / 64 / 128 pieces** next to the
  classic 5-piece box (IDs `TRAY_ID_BASE` 3000+, price scales with the piece
  count). Delivery expands the box slots to the selected capacity via the
  generalized `ExpandAllSizedBoxes` scanner (box name carries the capacity).
- The legacy 32x bulk path keeps working through the same scanner.

## v1.0.12

- Rebranded the plugin as `gregMod.MoreModules`
- Updated game interop references for Data Center 1.1.0 on Unity 6000.4.12f1
- Made the project buildable from the repository on Linux
