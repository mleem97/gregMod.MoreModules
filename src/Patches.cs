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
            var arr = __instance.sfpPrefabs;
            int len = arr?.Length ?? 0;

            if (len > 0 && !ModuleRegistry.Entries.ContainsKey(len - 1))
            {
                MelonLogger.Warning("sfpPrefabs was RESET — re-extending in Start.");
                Core.SetupRegistry(__instance);
            }
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
            // Kein manuelles SpawnPhysicalItem hier: ein solcher Add-Spawn wurde vom
            // Spiel nicht uid-verknuepft, sodass der Checkout die Lieferbox separat
            // frisch instanziiert und die Add-Box als zusaetzliche Box liegen bleibt
            // (Triple-Spawn: Add-Box + Liefer-Box + aktiv geparkte Template).
            // Die Lieferung holt sich ihren Prefab ueber ComputerShop.GetPrefabForItem
            // (unser Prefix) und instanziiert genau EINE Box daraus.
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

            // Tray-Pakete: TRAY_ID_BASE + moduleIndex * TraySizeCount + sizeIndex.
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
            var mgm = MainGameManager.instance;
            if (mgm == null) return;

            var arr = mgm.sfpPrefabs;
            if (arr == null) return;

            foreach (var (prefabID, entry) in ModuleRegistry.Entries)
            {
                if (prefabID < 0 || prefabID >= arr.Length) continue;

                if (arr[prefabID] == null)
                {
                    var template = Core.BuildModulePrefab(mgm, prefabID, entry,
                                                          Core.TemplateHolder?.transform);
                    if (template != null)
                        template.name = $"SFPModule_template_{prefabID}";
                    arr[prefabID] = template;
                }
            }
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
            if (__instance == null || __result == null) return;
            int boxType = -1;
            try { boxType = __instance.sfpBoxType; } catch { return; }
            if (!ModuleRegistry.TryGet(boxType, out _)) return;
            try { CustomModuleTags.TakenModuleIds.Add(__result.GetInstanceID()); } catch { }
        }
    }

    [HarmonyPatch(typeof(CableLink), nameof(CableLink.InsertSFP))]
    internal static class PatchCableLinkInsertSFP
    {
        private static void Prefix(float speed, SFPModule module)
        {
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
            int boxType = __instance.sfpBoxType;
            if (!ModuleRegistry.TryGet(boxType, out var entry)) return true;

            __result = (sfpType == entry.ModuleSfpType);
            return false;
        }
    }
}
