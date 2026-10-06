using System.Collections;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace GregModMoreModules
{
    // =========================================================================
    // Patch: MainGameManager.Awake (Postfix)
    // Earliest point where sfpPrefabs is populated — before OnLoad() restores
    // save data. Populates the registry and extends sfpPrefabs.
    // =========================================================================
    [HarmonyPatch(typeof(MainGameManager), nameof(MainGameManager.Awake))]
    internal static class PatchMainGameManagerAwake
    {
        private static void Postfix(MainGameManager __instance)
        {
            if (Core.s_disabledBySibling) return;
            MelonLogger.Msg("MainGameManager.Awake → setting up registry.");
            Core.SetupRegistry(__instance);
        }
    }

    // =========================================================================
    // Patch: MainGameManager.Start (Postfix)
    // Safety net — re-runs SetupRegistry if Start() reset sfpPrefabs back to
    // its vanilla size (which would orphan our custom indices).
    // =========================================================================
    [HarmonyPatch(typeof(MainGameManager), nameof(MainGameManager.Start))]
    internal static class PatchMainGameManagerStart
    {
        private static void Postfix(MainGameManager __instance)
        {
            if (Core.s_disabledBySibling) return;
            var arr = __instance.sfpPrefabs;
            int len = arr?.Length ?? 0;

            if (len > 0 && !ModuleRegistry.Entries.ContainsKey(len - 1))
            {
                MelonLogger.Warning("sfpPrefabs was RESET — re-extending in Start.");
                Core.SetupRegistry(__instance);
                return;
            }

            var boxes = __instance.sfpsBoxedPrefab;
            if (boxes != null && boxes.Length > 0 && boxes.Length < Core.MOD_ID_BASE)
            {
                MelonLogger.Warning("sfpsBoxedPrefab was RESET — re-extending in Start.");
                Core.ExtendBoxPrefabs(__instance);
            }
        }
    }

    // =========================================================================
    // Patch: MainGameManager.GetSfpBoxPrefab (Prefix)
    // Custom box types resolve to our parked templates (rebuilt if the Il2Cpp
    // wrapper went stale), so a saved custom box can respawn on load.
    // =========================================================================
    [HarmonyPatch(typeof(MainGameManager), nameof(MainGameManager.GetSfpBoxPrefab))]
    internal static class PatchGetSfpBoxPrefab
    {
        private static bool Prefix(MainGameManager __instance, int __0, ref GameObject __result)
        {
            if (Core.s_disabledBySibling) return true;
            if (!ModuleRegistry.TryGet(__0, out var entry)) return true;
            GameObject go = null;
            try
            {
                var arr = __instance.sfpsBoxedPrefab;
                if (arr != null && __0 < arr.Length) go = arr[__0];
                if (go != null) { var _ = go.transform; }
            }
            catch { go = null; }
            if (go == null)
                go = Core.BuildBoxPrefab(__instance, __0, entry, Core.TemplateHolder != null ? Core.TemplateHolder.transform : null);
            if (go == null) return true;
            __result = go;
            return false;
        }
    }

    // =========================================================================
    // Patch: ComputerShop.ButtonBuyShopItem (Prefix)
    // Adds custom shop items to the cart. The game's regular ButtonBuyShopItem
    // path silently rejects custom item IDs before it reaches GetPrefabForItem,
    // so these IDs use the lower-level spawn + ShopCartItem flow directly.
    // =========================================================================
    [HarmonyPatch(typeof(ComputerShop), nameof(ComputerShop.ButtonBuyShopItem))]
    internal static class PatchButtonBuyShopItem
    {
        private static bool Prefix(ComputerShop __instance, int itemID, int price,
                                   PlayerManager.ObjectInHand itemType, string displayName,
                                   bool isCustomColor)
        {
            if (Core.s_disabledBySibling) return true;
            if (Core.IsCustomItemID(itemID))
            {
                int before = __instance.cartUIItems != null ? __instance.cartUIItems.Count : -1;
                MelonLogger.Msg($"Buy clicked: itemID={itemID}, type={(int)itemType}, " +
                                $"price={price}, customColor={isCustomColor}, name='{displayName}'");

                bool added = AddCustomItemToCart(__instance, itemID, price, itemType, displayName);

                int after = __instance.cartUIItems != null ? __instance.cartUIItems.Count : -1;
                MelonLogger.Msg($"Custom cart add: success={added}, before={before}, after={after}, " +
                                $"currentPrice={__instance.currentPrice}");
                return false;
            }

            return true;
        }

        private static bool AddCustomItemToCart(ComputerShop shop, int itemID, int price,
                                                PlayerManager.ObjectInHand itemType,
                                                string displayName)
        {
            // No manual SpawnPhysicalItem here: such an add-spawn was not
            // uid-linked by game, so checkout would instantiate the delivery box
            // separately fresh and the add box stays behind as extra box
            // (triple spawn: add box + delivery box + actively parked template).
            // Delivery fetches its prefab via ComputerShop.GetPrefabForItem
            // (our prefix) and instantiates exactly ONE box from it.
            if (shop.shopCartItemPrefab == null || shop.parentForShopCartItems == null ||
                shop.cartUIItems == null)
            {
                MelonLogger.Error("Shop cart UI references missing.");
                return false;
            }

            var existingCartItem = FindExistingCartItem(shop, itemID, itemType);
            if (existingCartItem != null)
            {
                shop.BuyAnotherItem(itemID, price, itemType, existingCartItem);
                shop.UpdateCartTotal();
                MelonLogger.Msg($"Custom cart quantity increased: itemID={itemID}, " +
                                $"quantity={existingCartItem.Quantity}");
                return true;
            }

            var cartObject = Object.Instantiate(shop.shopCartItemPrefab,
                                                shop.parentForShopCartItems, false);
            var cartItem = cartObject.GetComponent<ShopCartItem>();
            if (cartItem == null)
            {
                Object.Destroy(cartObject);
                MelonLogger.Error("ShopCartItem component missing on cart prefab clone.");
                return false;
            }

            var noCustomColor = new Il2CppSystem.Nullable<Color>();
            cartItem.Initialize(shop, displayName, itemID, price, itemType, noCustomColor);
            shop.cartUIItems.Add(cartItem);
            shop.UpdateCartTotal();

            MelonLogger.Msg($"Custom cart item created: itemID={itemID}, quantity={cartItem.Quantity}");
            return true;
        }

        private static ShopCartItem FindExistingCartItem(ComputerShop shop, int itemID,
                                                         PlayerManager.ObjectInHand itemType)
        {
            if (shop.cartUIItems == null) return null;

            foreach (var cartItem in shop.cartUIItems)
            {
                if (cartItem == null) continue;
                if (cartItem.ItemID == itemID && cartItem.ItemType == itemType)
                    return cartItem;
            }

            return null;
        }
    }

    // =========================================================================
    // Patch: ComputerShop.ButtonCheckOut (Prefix)
    // Delivery happens at checkout — a fresh tray/bulk box is spawned minutes
    // after the Buy-click (when the scanner may already have stopped). Restart
    // the box scanner so the delivered box gets expanded to its tray capacity.
    // =========================================================================
    [HarmonyPatch(typeof(ComputerShop), nameof(ComputerShop.ButtonCheckOut))]
    internal static class PatchButtonCheckOut
    {
        private static void Prefix(ComputerShop __instance)
        {
            if (Core.s_disabledBySibling) return;
            MelonCoroutines.Start(Core.ExpandAllSizedBoxes());
        }
    }

    // =========================================================================
    // Patch: ComputerShop.GetPrefabForItem (Prefix)
    // Routes our custom itemID to the correct prefab when the player buys from
    // the shop. Handles both SFPBox (type 9) and bare SFPModule (type 8).
    // =========================================================================
    [HarmonyPatch(typeof(ComputerShop), nameof(ComputerShop.GetPrefabForItem))]
    internal static class PatchGetPrefabForItem
    {
        private static bool Prefix(int itemID, PlayerManager.ObjectInHand itemType, ref GameObject __result)
        {
            if (Core.s_disabledBySibling) return true;
            var mgm = MainGameManager.instance;
            if (mgm == null) return true;

            // 32x bulk item: BULK_ID_BASE + i → return a box marked with "_bulk_"
            // in its name. The actual 32-slot expansion is done post-delivery by
            // ExpandAllSizedBoxes (game re-initializes slots after instantiation).
            // The returned template is parked under the inactive TemplateHolder so
            // it never appears as an extra spawned box and the scanner skips it.
            if (itemID >= Core.BULK_ID_BASE && itemID < Core.BULK_ID_BASE + ModuleList.All.Length)
            {
                int regularID = itemID - Core.BULK_ID_BASE + Core.MOD_ID_BASE;
                if (ModuleRegistry.TryGet(regularID, out var bulkEntry) && (int)itemType == 9)
                {
                    MelonLogger.Msg($"GetPrefabForItem custom bulk: itemID={itemID}, regularID={regularID}");
                    __result = Core.BuildBulkBoxPrefab(mgm, itemID, bulkEntry,
                                                       Core.TemplateHolder != null ? Core.TemplateHolder.transform : null);
                    MelonCoroutines.Start(Core.ExpandAllSizedBoxes());
                    return false;
                }
                return true;
            }

            // Tray packs: TRAY_ID_BASE + moduleIndex * TraySizeCount + sizeIndex.
            if (itemID >= Core.TRAY_ID_BASE &&
                itemID < Core.TRAY_ID_BASE + ModuleList.All.Length * Core.TraySizeCount)
            {
                int regularID = Core.RegularIdForTray(itemID);
                if (ModuleRegistry.TryGet(regularID, out var trayEntry) && (int)itemType == 9)
                {
                    MelonLogger.Msg($"GetPrefabForItem custom tray: itemID={itemID}, " +
                                    $"{Core.TraySizeFromItemID(itemID)}x");
                    __result = Core.BuildTrayBoxPrefab(mgm, itemID, trayEntry,
                                                       Core.TemplateHolder != null ? Core.TemplateHolder.transform : null);
                    MelonCoroutines.Start(Core.ExpandAllSizedBoxes());
                    return false;
                }
                return true;
            }

            if (!ModuleRegistry.TryGet(itemID, out var entry)) return true;

            // ObjectInHand.SFPBox == 9, ObjectInHand.SFPModule == 8
            if ((int)itemType == 9)
            {
                MelonLogger.Msg($"GetPrefabForItem custom box: itemID={itemID}");
                __result = Core.BuildBoxPrefab(mgm, itemID, entry);
                return false;
            }
            if ((int)itemType == 8)
            {
                MelonLogger.Msg($"GetPrefabForItem custom module: itemID={itemID}");
                __result = Core.BuildModulePrefab(mgm, itemID, entry);
                return false;
            }

            return true;
        }
    }

    // =========================================================================
    // Patch: SFPBox.LoadSFPsFromSave (Prefix)
    // The load code accesses sfpPrefabs[prefabID] directly — it does NOT call
    // GetSfpPrefab(). Il2Cpp's GC can null our cached template between Awake
    // and the actual load. This prefix rebuilds fresh templates at all custom
    // indices immediately before the load code reads the array.
    // =========================================================================
    [HarmonyPatch(typeof(SFPBox), nameof(SFPBox.LoadSFPsFromSave))]
    internal static class PatchLoadSFPsFromSave
    {
        private static void Prefix()
        {
            if (Core.s_disabledBySibling) return;
            var mgm = MainGameManager.instance;
            if (mgm == null) { MelonLogger.Warning("[Templates] LoadSFPsFromSave: mgm null."); return; }

            var arr = mgm.sfpPrefabs;
            if (arr == null) { MelonLogger.Warning("[Templates] LoadSFPsFromSave: sfpPrefabs null."); return; }

            // TemplateHolder is an Il2Cpp object — it can go "fake-null" (native
            // object gone, C# wrapper still non-null) after a scene reload even
            // though it's DontDestroyOnLoad. Probe it defensively: if accessing
            // .transform throws, rebuild it instead of letting the exception
            // abort this entire loop (which previously left EVERY custom
            // prefabID after the failing one still null, so native load found
            // no template and ejected the inserted module).
            Transform parent = null;
            try { parent = Core.TemplateHolder?.transform; }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[Templates] TemplateHolder fake-null ({ex.Message}) — recreating.");
            }
            if (parent == null)
            {
                try { parent = Core.RecreateTemplateHolder(); }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Templates] TemplateHolder recreate failed: {ex.Message}");
                }
            }

            int rebuilt = 0, already = 0, failed = 0;
            foreach (var (prefabID, entry) in ModuleRegistry.Entries)
            {
                if (prefabID < 0 || prefabID >= arr.Length) continue;

                if (arr[prefabID] != null) { already++; continue; }

                try
                {
                    var template = Core.BuildModulePrefab(mgm, prefabID, entry, parent);
                    if (template != null)
                    {
                        template.name = $"SFPModule_template_{prefabID}";
                        arr[prefabID] = template;
                        rebuilt++;
                    }
                    else
                    {
                        failed++;
                        MelonLogger.Error($"[Templates] LoadSFPsFromSave: BuildModulePrefab returned null for prefabID={prefabID}.");
                    }
                }
                catch (System.Exception ex)
                {
                    failed++;
                    MelonLogger.Error($"[Templates] LoadSFPsFromSave: rebuild threw for prefabID={prefabID}: {ex.Message}");
                }
            }
            if (rebuilt > 0 || failed > 0)
                MelonLogger.Msg($"[Templates] LoadSFPsFromSave template check: already={already}, rebuilt={rebuilt}, failed={failed}.");
        }
    }

    // =========================================================================
    // Patch: CableLink.InsertSFP (Prefix)
    // Child modules taken from a custom box retain the vanilla base prefabID
    // (e.g. 3 for QSFP+ clones) because setting prefabID on active child
    // GameObjects causes the world tracker to spawn infinite loose modules.
    // Instead we fix it here — at the exact moment the module is inserted
    // into a port — so the save stores the correct custom prefabID and load
    // can restore the right module.
    //
    // Match by speed AND vanilla base prefabID, plus a tag for ambiguous
    // speeds: RJ45 and SFP+ share internal speed 2 (vanilla twins), so a
    // speed-only match would rewrite plain vanilla modules to custom IDs
    // (coupling their saves to this mod). Ambiguous speeds only rewrite
    // modules provably taken from a custom box (tagged in
    // PatchTakeSFPFromBox); unique speeds (100G+) keep the legacy match.
    // =========================================================================
    internal static class CustomModuleTags
    {
        internal static readonly System.Collections.Generic.HashSet<int> TakenModuleIds = new();
    }

    [HarmonyPatch(typeof(SFPBox), nameof(SFPBox.TakeSFPFromBox))]
    internal static class PatchTakeSFPFromBox
    {
        private static void Postfix(SFPBox __instance, SFPModule __result)
        {
            if (Core.s_disabledBySibling) return;
            if (__instance == null || __result == null) return;
            int boxType = -1;
            try { boxType = __instance.sfpBoxType; } catch { return; }
            if (!ModuleRegistry.TryGet(boxType, out _)) return;
            try { CustomModuleTags.TakenModuleIds.Add(__result.GetInstanceID()); } catch { }

            // Out of the box the module is a standalone world item: give it its
            // custom ID now (box type == custom prefabID). Previously it kept the
            // vanilla base ID until inserted into a port, so a loose module on
            // the floor/in hand was saved as plain 40G QSFP+ and came back vanilla.
            try
            {
                var u = __result.GetComponent<UsableObject>();
                if (u != null) u.prefabID = boxType;
            }
            catch { }
        }
    }

    // =========================================================================
    // Patch: SFPBox.RemoveSFPFromBox (Postfix)
    // This — not TakeSFPFromBox — is the path the player uses to pull a module
    // out of a box (confirmed live). By the time it runs the module has
    // already left the box for the player's hand, so relabel matching
    // modules in hand: same vanilla base ID and the box tier's speed.
    // =========================================================================
    [HarmonyPatch(typeof(SFPBox), nameof(SFPBox.RemoveSFPFromBox))]
    internal static class PatchRemoveSFPFromBox
    {
        private static void Postfix(SFPBox __instance)
        {
            if (Core.s_disabledBySibling) return;
            try
            {
                int boxType = __instance.sfpBoxType;
                if (!ModuleRegistry.TryGet(boxType, out var entry)) return;
                var hand = PlayerManager.instance?.objectInHandGO;
                if (hand == null) return;
                foreach (var go in hand)
                {
                    if (go == null) continue;
                    var m = go.GetComponent<SFPModule>();
                    var u = go.GetComponent<UsableObject>();
                    if (m == null || u == null) continue;
                    if (u.prefabID != entry.BasePrefabID) continue;
                    if (!Mathf.Approximately(m.speed, entry.SpeedInternal)) continue;
                    u.prefabID = boxType;
                    try { CustomModuleTags.TakenModuleIds.Add(m.GetInstanceID()); } catch { }
                }
            }
            catch { }
        }
    }

    // =========================================================================
    // Patch: SaveSystem.SaveGame (Prefix) — safety net
    // Any live module that still carries a vanilla base ID but runs at a speed
    // only one of our tiers uses (100G+) is ours; relabel it before the save
    // records it, so it doesn't come back as a plain vanilla module.
    // Ambiguous speeds (10G/25G twins of vanilla) are left alone.
    // =========================================================================
    [HarmonyPatch(typeof(SaveSystem), nameof(SaveSystem.SaveGame))]
    internal static class PatchSaveRelabelModules
    {
        private static void Prefix()
        {
            if (Core.s_disabledBySibling) return;
            try
            {
                var mgm = MainGameManager.instance;
                var arr = mgm != null ? mgm.sfpPrefabs : null;
                int fixedCount = 0;
                foreach (var m in Resources.FindObjectsOfTypeAll<SFPModule>())
                {
                    if (m == null || m.isInTheBox) continue;
                    var go = m.gameObject;
                    if (!go.scene.IsValid()) continue;
                    var u = go.GetComponent<UsableObject>();
                    if (u == null || ModuleRegistry.Entries.ContainsKey(u.prefabID)) continue;

                    int pid = u.prefabID;
                    float speed = m.speed;
                    if (arr != null && pid >= 0 && pid < arr.Length && arr[pid] != null)
                    {
                        var baseMod = arr[pid].GetComponent<SFPModule>();
                        if (baseMod != null && Mathf.Approximately(baseMod.speed, speed)) continue; // plain vanilla
                    }

                    int match = -1, hits = 0;
                    foreach (var (id, entry) in ModuleRegistry.Entries)
                    {
                        if (entry.BasePrefabID != pid || !Mathf.Approximately(entry.SpeedInternal, speed)) continue;
                        match = id; hits++;
                    }
                    if (hits != 1) continue;
                    u.prefabID = match;
                    fixedCount++;
                }
                if (fixedCount > 0)
                    MelonLogger.Msg($"Save: relabeled {fixedCount} custom module(s) still carrying a vanilla ID.");
            }
            catch (System.Exception ex) { MelonLogger.Warning("Save relabel failed: " + ex.Message); }
        }
    }

    // =========================================================================
    // Back into a box: restore the vanilla base ID — box children must keep it
    // (see BuildBoxPrefab: custom IDs on box children register them as loose
    // world items). The box's own type still identifies the contents.
    // =========================================================================
    internal static class BoxReturnReset
    {
        internal static void ResetChildren(SFPBox box)
        {
            if (box == null) return;
            try
            {
                foreach (var m in box.GetComponentsInChildren<SFPModule>(true))
                {
                    if (m == null) continue;
                    var u = m.GetComponent<UsableObject>();
                    if (u != null && ModuleRegistry.TryGet(u.prefabID, out var entry))
                        u.prefabID = entry.BasePrefabID;
                }
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(SFPBox), nameof(SFPBox.ReturnSFPDirectly))]
    internal static class PatchReturnSFPDirectly
    {
        private static void Postfix(SFPBox __instance)
        {
            if (Core.s_disabledBySibling) return;
            BoxReturnReset.ResetChildren(__instance);
        }
    }

    [HarmonyPatch(typeof(SFPBox), nameof(SFPBox.InsertSFPBackIntoBox))]
    internal static class PatchInsertSFPBackIntoBox
    {
        private static void Postfix(SFPBox __instance)
        {
            if (Core.s_disabledBySibling) return;
            BoxReturnReset.ResetChildren(__instance);
        }
    }

    [HarmonyPatch(typeof(CableLink), nameof(CableLink.InsertSFP))]
    internal static class PatchCableLinkInsertSFP
    {
        private static void Prefix(float speed, SFPModule module)
        {
            if (Core.s_disabledBySibling) return;
            var usableObj = module?.GetComponent<UsableObject>();
            if (usableObj == null) return;

            int currentPrefabID = -1;
            try { currentPrefabID = usableObj.prefabID; } catch { return; }

            int moduleInstanceId = -1;
            try { moduleInstanceId = module.GetInstanceID(); } catch { }

            int speedUsers = 0;
            foreach (var (_, other) in ModuleRegistry.Entries)
            {
                if (Mathf.Approximately(speed, other.SpeedInternal)) speedUsers++;
            }

            bool tagged = moduleInstanceId >= 0 && CustomModuleTags.TakenModuleIds.Contains(moduleInstanceId);

            foreach (var (prefabID, entry) in ModuleRegistry.Entries)
            {
                if (!Mathf.Approximately(speed, entry.SpeedInternal)) continue;
                if (currentPrefabID != entry.BasePrefabID || currentPrefabID == prefabID) continue;
                if (speedUsers > 1 && !tagged) continue;
                usableObj.prefabID = prefabID;
                try { CustomModuleTags.TakenModuleIds.Remove(moduleInstanceId); } catch { }
                break;
            }
        }
    }

    // =========================================================================
    // Patch: SFPBox.CanAcceptSFP (Prefix)
    // Our custom box uses sfpBoxType == prefabID, but our modules
    // carry sfpType == vanilla QSFP+ type for port compatibility. Without this
    // patch the box would reject our module because the types don't match.
    // =========================================================================
    [HarmonyPatch(typeof(SFPBox), nameof(SFPBox.CanAcceptSFP))]
    internal static class PatchCanAcceptSFP
    {
        private static bool Prefix(SFPBox __instance, int sfpType, ref bool __result)
        {
            if (Core.s_disabledBySibling) return true;
            int boxType = __instance.sfpBoxType;
            if (!ModuleRegistry.TryGet(boxType, out var entry)) return true;

            __result = (sfpType == entry.ModuleSfpType);
            return false;
        }
    }
}
