using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[assembly: MelonInfo(typeof(GregModMoreModules.Core), "gregMod.MoreModules", "1.0.19", "TeamGreg Modding (leoms1408 / mleem97)")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace GregModMoreModules
{
    public class Core : MelonMod
    {
        // Sprite from the vanilla QSFP+ shop entry — reused as icon for all custom modules.
        internal static Sprite BaseQsfpSprite;

        // sfpType of the vanilla QSFP+ module (form-factor; determines port compatibility).
        // Our custom modules keep this value so they fit the same switch ports.
        internal static int BaseQsfpSfpType = -1;

        // prefabID of the vanilla QSFP+ module — used as clone source in BuildModulePrefab.
        internal static int BaseQsfpPrefabID = -1;

        // Index into mgm.sfpsBoxedPrefab of the vanilla QSFP+/Fibre 40G box —
        // clone source for BuildBoxPrefab. Separate from BaseQsfpPrefabID because
        // module prefabIDs and box array indices are different ID spaces.
        internal static int BaseBoxPrefabIndex = -1;

        // Item-ID ranges for shop entries.
        // MOD_ID_BASE: 5x box / bare module (also used as sfpBoxType / prefabID in save data).
        // BULK_ID_BASE: 32x box shop item — distinct ID so GetPrefabForItem can return a
        //               pre-expanded box without any post-delivery scanning.
        // TRAY_ID_BASE: tray packages in configurable piece counts. ID layout:
        //               TRAY_ID_BASE + moduleIndex * TraySizeCount + sizeIndex.
        internal const int MOD_ID_BASE  = 1000;
        internal const int BULK_ID_BASE = 2000;
        internal const int TRAY_ID_BASE = 3000;

        // Piece counts ("trays") per module — in addition to 5x box.
        internal const int TraySizeCount = 4;
        internal static readonly int[] TraySizes = { 16, 36, 64, 128 };

        // Inactive holder for prefab templates — parenting templates here makes their
        // activeInHierarchy = false, so the game's UsableObject tracker ignores them.
        // Object.Instantiate still produces active clones from inactive-hierarchy objects.
        internal static GameObject TemplateHolder { get; private set; }
        private static readonly Dictionary<int, int> ExtendedShopRowsByParent = new Dictionary<int, int>();

        // True when gregMod.RealisticModules is loaded: the successor owns the
        // module catalog (same ID ranges, plus breakout/validation features),
        // so this mod stays inert and every tier appears exactly once.
        // Same pattern as MoreServers yielding to MoreModules.
        internal static bool s_disabledBySibling;

        /// <summary>
        /// Returns true when the successor mod is loaded, in which case this
        /// mod must not register anything. Runs at MainGameManager.Awake,
        /// by which time all MelonMods are registered.
        /// </summary>
        private static bool DetectSiblingConflict()
        {
            if (s_disabledBySibling)
                return true;
            try
            {
                foreach (var mod in MelonLoader.MelonMod.RegisteredMelons)
                {
                    if (mod?.Info == null || mod.Info.SystemType?.Assembly == typeof(Core).Assembly)
                        continue;
                    if (mod.Info.Name == "gregMod.RealisticModules")
                    {
                        // Only yield when the successor is actually active.
                        // If the user turned RealisticModules off via F1, keep
                        // MoreModules running so the catalog is not empty.
                        bool successorActive = true;
                        try
                        {
                            var cat = MelonPreferences.GetCategory("gregMod.RealisticModules");
                            var entry = cat?.GetEntry<bool>("Enabled");
                            if (entry != null) successorActive = entry.Value;
                        }
                        catch { /* missing entry → assume active */ }

                        if (!successorActive)
                        {
                            MelonLogger.Msg("[MoreModules] gregMod.RealisticModules present but " +
                                "disabled — MoreModules stays active.");
                            continue;
                        }

                        s_disabledBySibling = true;
                        MelonLogger.Error("[MoreModules] gregMod.RealisticModules is active — " +
                            "disabling MoreModules to avoid double module handling. " +
                            "Install only one of the two, or disable RealisticModules in F1.");
                        return true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[MoreModules] Sibling check failed: {ex.Message}");
            }
            return false;
        }

        // -----------------------------------------------------------------------
        // Diagnostic: dumps the full vanilla SFP module and SFP box prefab
        // catalogs so we can map each custom speed tier to its real vanilla
        // module type (SFP / QSFP+ / QSFP28 / QSFP-DD / Fiber …). Kept as pure
        // logging until the type mapping is derived from real data.
        // -----------------------------------------------------------------------
        private static void DumpVanillaCatalog(MainGameManager mgm)
        {
            MelonLogger.Msg("=== Vanilla SFP module catalog ===");
            var sfpPrefabs = mgm.sfpPrefabs;
            if (sfpPrefabs != null)
            {
                for (int i = 0; i < sfpPrefabs.Length; i++)
                {
                    var go = sfpPrefabs[i];
                    if (go == null) { MelonLogger.Msg($"[{i}] null"); continue; }
                    var sfpMod = go.GetComponent<SFPModule>();
                    var usable = go.GetComponent<UsableObject>();
                    float speed = sfpMod != null ? sfpMod.speed : -1f;
                    int   st    = sfpMod != null ? sfpMod.sfpType : -1;
                    int   pid   = usable != null ? usable.prefabID : -1;
                    string cell = speed >= 0f ? $"{speed * 5f:0}G" : "?";
                    MelonLogger.Msg($"[{i}] prefabID={pid} sfpType={st} speed={cell} name={go.name}");
                }
            }
            else
            {
                MelonLogger.Msg("(sfpPrefabs is null)");
            }

            MelonLogger.Msg("=== Vanilla SFPBox catalog ===");
            var boxes = mgm.sfpsBoxedPrefab;
            if (boxes != null)
            {
                for (int i = 0; i < boxes.Length; i++)
                {
                    var go = boxes[i];
                    if (go == null) { MelonLogger.Msg($"[{i}] null"); continue; }
                    var sfpBox = go.GetComponent<SFPBox>();
                    int bt = sfpBox != null ? sfpBox.sfpBoxType : -1;
                    MelonLogger.Msg($"[{i}] boxType={bt} name={go.name}");
                }
            }
            else
            {
                MelonLogger.Msg("(sfpsBoxedPrefab is null)");
            }
        }

        // -----------------------------------------------------------------------
        // Scans vanilla sfpPrefabs for the real QSFP+ module (40G, sfpType=3) and
        // the matching Fibre 40G box, stores them as clone sources, then extends
        // the sfpPrefabs array with one slot per custom module at MOD_ID_BASE.
        //
        // Base selection only considers vanilla-named entries (SFP_*). Without that
        // filter a pre-extended array (CustomSFP_* / templates at 100+) is picked as
        // "highest speed" → wrong sfpType (0/RJ45) and a wrong shop template.
        //
        // Called from PatchMainGameManagerAwake — the earliest point where
        // sfpPrefabs is populated, guaranteed to run before OnLoad() restores saves.
        // -----------------------------------------------------------------------
        internal static void SetupRegistry(MainGameManager mgm)
        {
            // Mutual exclusion: gregMod.RealisticModules owns the module
            // catalog. If it is loaded, stay inert so tiers/buttons/prefabs
            // are registered exactly once (no doubles in the shop).
            if (DetectSiblingConflict())
                return;

            ModuleRegistry.Clear();
            BaseQsfpPrefabID = -1;
            BaseQsfpSfpType = -1;
            BaseBoxPrefabIndex = -1;

            var sfpPrefabs = mgm.sfpPrefabs;
            if (sfpPrefabs == null || sfpPrefabs.Length == 0)
            {
                MelonLogger.Warning("sfpPrefabs is empty — skipping setup.");
                return;
            }

            MelonLogger.Msg($"sfpPrefabs length: {sfpPrefabs.Length}");

            float highestVanillaSpeed = -1f;
            int vanillaCount = 0;

            for (int i = 0; i < sfpPrefabs.Length && i < MOD_ID_BASE; i++)
            {
                var go = sfpPrefabs[i];
                if (go == null) continue;

                // Vanilla catalog only: SFP_RJ45 / SFP_fabric* / SFP_QSFP.
                // Reject CustomSFP_*, SFPModule_custom_*, SFPModule_template_*.
                if (!IsVanillaSfpName(go.name)) continue;

                vanillaCount = i + 1;

                var sfpMod    = go.GetComponent<SFPModule>();
                var usableObj = go.GetComponent<UsableObject>();
                float speed   = sfpMod    != null ? sfpMod.speed       : -1f;
                int   sfpType = sfpMod    != null ? sfpMod.sfpType     : -1;
                int   pid     = usableObj != null ? usableObj.prefabID : -1;

                if (speed > highestVanillaSpeed)
                {
                    highestVanillaSpeed = speed;
                    BaseQsfpSfpType     = sfpType;
                    BaseQsfpPrefabID    = pid;
                }
            }

            if (BaseQsfpPrefabID < 0)
            {
                MelonLogger.Error("Could not identify base QSFP+ prefab.");
                return;
            }

            MelonLogger.Msg($"Base QSFP+: prefabID={BaseQsfpPrefabID}, " +
                            $"sfpType={BaseQsfpSfpType}, {highestVanillaSpeed * 5f} Gbps, " +
                            $"vanillaCount={vanillaCount}");

            BaseBoxPrefabIndex = FindBaseBoxIndex(mgm);
            MelonLogger.Msg($"Base box index: {BaseBoxPrefabIndex}" +
                            (BaseBoxPrefabIndex >= 0 && mgm.sfpsBoxedPrefab != null &&
                             BaseBoxPrefabIndex < mgm.sfpsBoxedPrefab.Length &&
                             mgm.sfpsBoxedPrefab[BaseBoxPrefabIndex] != null
                                ? $" ({mgm.sfpsBoxedPrefab[BaseBoxPrefabIndex].name})"
                                : " (missing)"));

            DumpVanillaCatalog(mgm);

            // Create/recreate the inactive holder that hides templates from the world system.
            if (TemplateHolder != null)
                Object.Destroy(TemplateHolder);
            TemplateHolder = new GameObject("gregModMoreModules_TemplateHolder");
            TemplateHolder.SetActive(false);
            Object.DontDestroyOnLoad(TemplateHolder);

            if (vanillaCount > MOD_ID_BASE)
            {
                MelonLogger.Error($"vanillaCount={vanillaCount} exceeds " +
                                  $"MOD_ID_BASE={MOD_ID_BASE}! prefabID collision risk — mod disabled.");
                return;
            }

            // Vanilla entries at their original indices, null padding up to MOD_ID_BASE,
            // then one slot per custom module. Preserve any non-vanilla slots below
            // MOD_ID_BASE that another mod may already own (do not wipe them).
            var extended = new GameObject[MOD_ID_BASE + ModuleList.All.Length];
            for (int i = 0; i < sfpPrefabs.Length && i < MOD_ID_BASE; i++)
                extended[i] = sfpPrefabs[i];

            int nextID = MOD_ID_BASE;

            foreach (var def in ModuleList.All)
            {
                int id = nextID++;
                int formSfpType = ResolveFormSfpType(mgm, def, vanillaCount);
                if (formSfpType < 0)
                {
                    MelonLogger.Error($"No vanilla base prefab for '{def.DisplayName}' " +
                                      $"(BasePrefabID={def.BasePrefabID}) — skipped.");
                    extended[id] = null;
                    continue;
                }

                var entry = new ModuleRegistry.Entry(
                    speedInternal: def.InternalSpeed,
                    moduleSfpType: formSfpType,
                    boxSfpType:    id,
                    basePrefabID:  def.BasePrefabID,
                    baseBoxIndex:  def.BaseBoxIndex
                );
                ModuleRegistry.Register(id, entry);

                // Store a template at sfpPrefabs[id] so LoadSFPsFromSave (which does
                // direct array access) can find the prefab during save loading.
                var template = BuildModulePrefab(mgm, id, entry, TemplateHolder.transform);
                if (template != null)
                    template.name = $"SFPModule_template_{id}";
                extended[id] = template;

                MelonLogger.Msg($"Registered '{def.DisplayName}': " +
                                $"prefabID={id}, {def.SpeedGbps} Gbps, sfpType={formSfpType}");
            }

            mgm.sfpPrefabs = extended;
            MelonLogger.Msg($"sfpPrefabs extended: {sfpPrefabs.Length} → {extended.Length}");
        }

        // sfpType of the vanilla prefab with the definition's BasePrefabID
        // (form factor). -1 when the base is missing (definition skipped).
        private static int ResolveFormSfpType(MainGameManager mgm, ModuleDefinition def, int vanillaCount)
        {
            var sfpPrefabs = mgm.sfpPrefabs;
            if (sfpPrefabs == null) return -1;
            for (int i = 0; i < sfpPrefabs.Length && i < vanillaCount; i++)
            {
                var go = sfpPrefabs[i];
                if (go == null || !IsVanillaSfpName(go.name)) continue;
                var sfpMod = go.GetComponent<SFPModule>();
                var usableObj = go.GetComponent<UsableObject>();
                if (usableObj != null && usableObj.prefabID == def.BasePrefabID && sfpMod != null)
                    return sfpMod.sfpType;
            }

            return -1;
        }

        // Vanilla module shop/object names only (SFP_RJ45, SFP_fabric*, SFP_QSFP).
        private static bool IsVanillaSfpName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.StartsWith("SFP_", System.StringComparison.Ordinal)) return true;
            return false;
        }

        // Clone source for boxes: prefer highest vanilla boxType (Fibre 40G = 3),
        // else last non-null entry. Never use BaseQsfpPrefabID (module ID space).
        private static int FindBaseBoxIndex(MainGameManager mgm)
        {
            var boxes = mgm.sfpsBoxedPrefab;
            if (boxes == null || boxes.Length == 0) return -1;

            int best = -1;
            int bestBoxType = -1;
            int lastNonNull = -1;

            for (int i = 0; i < boxes.Length; i++)
            {
                var go = boxes[i];
                if (go == null) continue;
                lastNonNull = i;
                var sfpBox = go.GetComponent<SFPBox>();
                int bt = sfpBox != null ? sfpBox.sfpBoxType : -1;
                if (bt > bestBoxType)
                {
                    bestBoxType = bt;
                    best = i;
                }
            }

            return best >= 0 ? best : lastNonNull;
        }

        // -----------------------------------------------------------------------
        // Clones the vanilla QSFP+ module prefab and applies our custom speed and
        // prefabID. Called on-demand from patches rather than caching the result,
        // because Il2Cpp's GC can silently invalidate native pointers on cached
        // GameObjects stored in C# data structures.
        //
        // parent: when non-null the clone is instantiated directly under that transform,
        // so it is never active in hierarchy and the world tracker cannot pick it up.
        // Pass TemplateHolder.transform for cached templates, null for live clones.
        // -----------------------------------------------------------------------
        internal static GameObject BuildModulePrefab(MainGameManager mgm, int prefabID,
                                                     ModuleRegistry.Entry entry,
                                                     Transform parent = null)
        {
            var basePrefab = mgm.sfpPrefabs[entry.BasePrefabID];
            if (basePrefab == null)
            {
                MelonLogger.Error($"Base prefab [{entry.BasePrefabID}] is null.");
                return null;
            }

            var clone = parent != null
                ? Object.Instantiate(basePrefab, parent, false)
                : Object.Instantiate(basePrefab);
            clone.name = $"SFPModule_custom_{prefabID}";

            var sfpMod = clone.GetComponent<SFPModule>();
            if (sfpMod != null)
                sfpMod.speed = entry.SpeedInternal;

            var usableObj = clone.GetComponent<UsableObject>();
            if (usableObj != null)
                usableObj.prefabID = prefabID;
            
            ApplyModuleTint(clone, prefabID);

            return clone;
        }

        // -----------------------------------------------------------------------
        // Walks every Renderer in the module hierarchy, clones any material whose
        // name contains "Blue", and recolors it to the tint defined per prefabID.
        // Uses GetComponentsInChildren because the MeshRenderer of the QSFP+ model
        // lives on a child GameObject, not on the root.
        // -----------------------------------------------------------------------
        internal static void ApplyModuleTint(GameObject root, int prefabID)
        {
            if (root == null) return;

            // prefabIDs start at MOD_ID_BASE and map 1:1 to ModuleList.All.
            int defIndex = prefabID - MOD_ID_BASE;
            if (defIndex < 0 || defIndex >= ModuleList.All.Length) return;
            Color tint = ModuleList.All[defIndex].ModuleColor;

            // Common color property names across shaders we might encounter.
            string[] colorProps = { "_Color", "_BaseColor", "_MainColor", "_TintColor", "_Tint", "_AlbedoColor" };

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                var mats = rend.materials; // returns instanced copies — safe to mutate
                bool changed = false;

                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null) continue;
                    if (!mats[m].name.Contains("Blue")) continue;

                    foreach (var prop in colorProps)
                    {
                        if (mats[m].HasProperty(prop))
                            mats[m].SetColor(prop, tint);
                    }
                    changed = true;
                }

                if (changed) rend.materials = mats;
            }
        }

        // -----------------------------------------------------------------------
        // Clones the vanilla QSFP+ box prefab and applies our custom sfpBoxType
        // and prefabID. Also updates all child SFPModule components inside the box
        // so the player receives the correct module when unboxing.
        // -----------------------------------------------------------------------
        internal static GameObject BuildBoxPrefab(MainGameManager mgm, int prefabID,
                                                  ModuleRegistry.Entry entry,
                                                  Transform parent = null)
        {
            var boxPrefabs = mgm.sfpsBoxedPrefab;
            if (boxPrefabs == null) return null;

            // Per-definition box form (RJ45/SFP+/SFP28/QSFP+); fallback to the
            // global QSFP+ box index when unset.
            int wantBox = entry.BaseBoxIndex >= 0 ? entry.BaseBoxIndex : BaseBoxPrefabIndex;
            GameObject baseBox = null;
            if (wantBox >= 0 && wantBox < boxPrefabs.Length)
                baseBox = boxPrefabs[wantBox];

            // Fall back to the last non-null box (highest vanilla boxType).
            if (baseBox == null)
                for (int i = boxPrefabs.Length - 1; i >= 0; i--)
                    if (boxPrefabs[i] != null) { baseBox = boxPrefabs[i]; break; }

            if (baseBox == null)
            {
                MelonLogger.Warning("No base box prefab found.");
                return null;
            }

            var clone = parent != null
                ? Object.Instantiate(baseBox, parent, false)
                : Object.Instantiate(baseBox);
            clone.name = $"SFPBox_custom_{prefabID}";

            var sfpBox = clone.GetComponent<SFPBox>();
            if (sfpBox != null)
                sfpBox.sfpBoxType = prefabID;

            var usableObj = clone.GetComponent<UsableObject>();
            if (usableObj != null)
                usableObj.prefabID = prefabID;

            // The box prefab contains the SFPModules as child GameObjects.
            // Only update speed — do NOT set prefabID on children, as that would
            // register them as independent world items and cause them to spawn loose.
            // PatchCableLinkInsertSFP corrects the prefabID at insertion time instead.
            foreach (var childModule in clone.GetComponentsInChildren<SFPModule>())
            {
                childModule.speed = entry.SpeedInternal;
                ApplyModuleTint(childModule.gameObject, prefabID);
            }

            return clone;
        }

        // -----------------------------------------------------------------------
        // Triggered on every scene load. Starts the shop injection coroutine for
        // any scene other than the main menu (buildIndex 0).
        // -----------------------------------------------------------------------
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            // A running box scan is cancelled on scene change;
            // reset flag so future deliveries expand again.
            _boxScannerRunning = false;

            if (s_disabledBySibling) return;
            if (buildIndex != 0)
                MelonCoroutines.Start(AddShopItems());
        }

        // -----------------------------------------------------------------------
        // Waits for the shop to finish initializing, then injects a shop button
        // for each registered custom module into the "HL Mods" section.
        // The 1.5 s delay is necessary because the shop UI is built after scene load.
        // -----------------------------------------------------------------------
        private IEnumerator AddShopItems()
        {
            // The shop UI may build its item list lazily (tabs, progression).
            // Retry until a usable template appears instead of giving up once.
            const int maxAttempts = 10;
            ShopItem sourceItem = null;
            ComputerShop computerShop = null;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (attempt > 0) yield return new WaitForSeconds(3f);
                else yield return new WaitForSeconds(1.5f);

                var mgm = MainGameManager.instance;
                if (mgm == null) continue;

                computerShop = mgm.computerShop;
                if (computerShop == null) continue;

                sourceItem = FindShopTemplate(computerShop);
                if (sourceItem != null) break;

                if (attempt == 0)
                    LoggerInstance.Warning("No SFP shop template yet — retrying (shop may build lazily).");
            }

            if (sourceItem == null || computerShop == null)
            {
                LoggerInstance.Warning("No shop template found after retries — shop buttons skipped.");
                yield break;
            }

            var shopRoot = computerShop.shopItemParent;
            if (shopRoot == null) { LoggerInstance.Warning("shopItemParent null."); yield break; }

            // Target the "HL Mods" section inside VL-ShopItems so our items appear
            // in the correct category rather than being appended at the end.
            var sfpParent = sourceItem.transform.parent != null
                ? sourceItem.transform.parent.gameObject
                : shopRoot;

            // Target row count: 1 x 5-pack + 4 tray packs (16/36/64/128) per module.
            int packagesPerModule = 1 + TraySizeCount;
            var customRows = EnsureCustomSfpRows(shopRoot, sfpParent,
                                                 ModuleList.All.Length * packagesPerModule);
            if (customRows.Count < 0)
                LoggerInstance.Warning("'HL Mods' not found — falling back to shopItemParent.");

            float itemHeight = 0f;
            var sourceRt = sourceItem.GetComponent<UnityEngine.RectTransform>();
            if (sourceRt != null)
                itemHeight = sourceRt.rect.height;

            int addedSfpCount = 0;
            int packageIndex  = 0;

            // Template/price/sprite per form factor (box index) — cached.
            var formTemplates = new Dictionary<int, ShopItem>();

            for (int i = 0; i < ModuleList.All.Length; i++)
            {
                var def      = ModuleList.All[i];
                int prefabID = MOD_ID_BASE + i;
                if (!ModuleRegistry.TryGet(prefabID, out var entry)) continue;

                var formTemplate = FormShopTemplate(computerShop, sourceItem, formTemplates, def.BaseBoxIndex);
                int basePrice = formTemplate != null && formTemplate.shopItemSO != null
                    ? formTemplate.shopItemSO.price
                    : sourceItem.shopItemSO.price;
                Sprite formSprite = ResolveFormSprite(formTemplate);

                // 5x pack (standard, previous behavior).
                var added5 = AddShopPackage(computerShop, formTemplate ?? sourceItem,
                                            RowForPackage(customRows, sfpParent, packageIndex),
                                            prefabID,
                                            BuildShopLabel("5x", def),
                                            (int)(basePrice * def.PriceMultiplier),
                                            def.XpToUnlock, def.ShopGuid, formSprite);
                if (added5 != null) addedSfpCount++;
                packageIndex++;

                // Tray packs 16 / 36 / 64 / 128 pcs — in addition to 5x box.
                for (int s = 0; s < TraySizeCount; s++)
                {
                    int cap         = TraySizes[s];
                    int trayItemID  = TRAY_ID_BASE + i * TraySizeCount + s;
                    int trayPrice   = (int)(basePrice * def.PriceMultiplier * (cap / 5f));

                    var addedTray = AddShopPackage(computerShop, formTemplate ?? sourceItem,
                                                   RowForPackage(customRows, sfpParent, packageIndex),
                                                   trayItemID,
                                                   BuildShopLabel($"{cap}x", def),
                                                   trayPrice,
                                                   def.XpToUnlock,
                                                   def.ShopGuid + $"_{cap}x", formSprite);
                    if (addedTray != null) addedSfpCount++;
                    packageIndex++;
                }
            }

            EnsureBackplaneTopSpacer(computerShop, shopRoot, sfpParent, itemHeight);

            // The HL Mods container has a fixed height — extend it so the ScrollRect
            // can scroll far enough to reveal our newly added items.
            ExtendVerticalContainer(shopRoot, itemHeight, customRows.Count);

            RebuildShopLayout(shopRoot);
        }

        // Only real SFP boxes are valid templates — prefer the Fibre 40G / QSFP+
        // box (itemID == BaseBoxPrefabIndex), then highest itemID box, then any
        // box. itemID for vanilla boxes tracks the box array index (0=RJ45 … 3=40G),
        // not the module prefabID — matching BaseQsfpPrefabID was wrong and pulled
        // the RJ45 card (itemID=0) as icon/price template.
        // Searches the shopItems array AND the full shop hierarchy including
        // inactive objects (locked/progression-gated boxes are inactive but
        // still valid visual/price templates).
        private static ShopItem FindShopTemplate(ComputerShop computerShop)
        {
            ShopItem exactBox = null;
            ShopItem bestIdBox = null;
            ShopItem anyBox = null;
            int arrayCount = 0;
            int hierarchyBoxes = 0;
            bool yieldedAny = false;

            var items = computerShop.shopItems;
            if (items != null)
            {
                foreach (var si in items)
                {
                    if (si == null || si.shopItemSO == null) continue;
                    arrayCount++;
                    if ((int)si.shopItemSO.itemType != 9) continue;
                    yieldedAny = true;
                    (exactBox, bestIdBox, anyBox) = PreferBox(si, exactBox, bestIdBox, anyBox);
                }
            }

            if (!yieldedAny)
            {
                var root = computerShop.shopItemParent;
                var all = root != null ? root.GetComponentsInChildren<ShopItem>(true) : null;
                if (all != null)
                {
                    foreach (var si in all)
                    {
                        if (si == null || si.shopItemSO == null) continue;
                        if ((int)si.shopItemSO.itemType != 9) continue;
                        hierarchyBoxes++;
                        (exactBox, bestIdBox, anyBox) = PreferBox(si, exactBox, bestIdBox, anyBox);
                    }
                }
            }

            ShopItem picked = exactBox ?? bestIdBox ?? anyBox;
            if (picked != null)
            {
                if (picked.shopItemSO.sprite != null)
                    BaseQsfpSprite = picked.shopItemSO.sprite;
                string tier = picked == exactBox
                    ? $"exact box itemID={picked.shopItemSO.itemID}"
                    : $"SFP box (itemID={picked.shopItemSO.itemID})";
                MelonLoader.MelonLogger.Msg($"Shop template: {tier}.");
            }
            else
            {
                MelonLoader.MelonLogger.Msg(
                    $"Shop scan: {arrayCount} array items, {hierarchyBoxes} hierarchy boxes — no SFP box yet.");
            }
            return picked;
        }

        private static (ShopItem exact, ShopItem bestId, ShopItem any) PreferBox(
            ShopItem si, ShopItem exact, ShopItem bestId, ShopItem any)
        {
            int id = si.shopItemSO != null ? si.shopItemSO.itemID : -1;
            if (BaseBoxPrefabIndex >= 0 && id == BaseBoxPrefabIndex)
                exact = si;
            if (bestId == null || id > (bestId.shopItemSO != null ? bestId.shopItemSO.itemID : -1))
                bestId = si;
            if (any == null)
                any = si;
            return (exact, bestId, any);
        }

        private static System.Collections.Generic.List<GameObject> EnsureCustomSfpRows(GameObject shopRoot,
                                                                                       GameObject templateRow,
                                                                                       int itemCount)
        {
            var rows = new System.Collections.Generic.List<GameObject>();
            if (shopRoot == null || templateRow == null) return rows;

            int rowCount = Mathf.CeilToInt(itemCount / 4f);
            int insertIndex = templateRow.transform.GetSiblingIndex() + 1;

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                string rowName = $"HL gregMod.MoreModules {rowIndex + 1}";
                var existing = shopRoot.transform.Find(rowName);
                GameObject row = existing != null ? existing.gameObject : null;

                if (row == null)
                {
                    row = Object.Instantiate(templateRow, shopRoot.transform, false);
                    row.name = rowName;
                    ClearRowChildren(row);
                }

                row.transform.SetSiblingIndex(insertIndex + rowIndex);
                row.SetActive(true);
                rows.Add(row);
            }

            return rows;
        }

        private static void ClearRowChildren(GameObject row)
        {
            if (row == null) return;

            for (int i = row.transform.childCount - 1; i >= 0; i--)
            {
                var child = row.transform.GetChild(i);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }
        }

        // Row for next shop entry (4 entries per row).
        private static GameObject RowForPackage(System.Collections.Generic.List<GameObject> customRows,
                                                GameObject fallback, int packageIndex)
        {
            return customRows.Count > 0
                ? customRows[Mathf.Min(packageIndex / 4, customRows.Count - 1)]
                : fallback;
        }

        private static string BuildShopLabel(string quantity, ModuleDefinition def)
        {
            string speed = $"{def.SpeedGbps:0}Gbps";
            string moduleName = def.DisplayName;
            if (moduleName.EndsWith(speed))
                moduleName = moduleName.Substring(0, moduleName.Length - speed.Length).TrimEnd();

            string conn = string.IsNullOrEmpty(def.ConnectionLabel) ? "Fiber" : def.ConnectionLabel;
            return $"{quantity} {moduleName} Module {conn} {speed}";
        }

        // Shop template per box form factor (cached): ShopItem whose itemID matches
        // box index (0=RJ45 … 3=40G). Fallback: QSFP+ template.
        private static ShopItem FormShopTemplate(ComputerShop computerShop, ShopItem fallback,
                                                 Dictionary<int, ShopItem> cache, int boxIndex)
        {
            if (cache.TryGetValue(boxIndex, out var cached)) return cached;
            ShopItem found = null;
            try
            {
                var items = computerShop.shopItems;
                if (items != null)
                {
                    foreach (var si in items)
                    {
                        if (si == null || si.shopItemSO == null) continue;
                        if ((int)si.shopItemSO.itemType != 9) continue;
                        if (si.shopItemSO.itemID == boxIndex) { found = si; break; }
                    }
                }
            }
            catch { found = null; }

            if (found == null) found = fallback;
            cache[boxIndex] = found;
            return found;
        }

        private static Sprite ResolveFormSprite(ShopItem formTemplate)
        {
            try
            {
                if (formTemplate != null && formTemplate.shopItemSO != null &&
                    formTemplate.shopItemSO.sprite != null)
                    return formTemplate.shopItemSO.sprite;
            }
            catch { }
            return BaseQsfpSprite;
        }

        private static void ExtendVerticalContainer(GameObject parent, float itemHeight, int addedRows)
        {
            if (parent == null || itemHeight <= 0f || addedRows <= 0) return;

            var containerRt = parent.GetComponent<UnityEngine.RectTransform>();
            if (containerRt == null) return;

            int instanceId = parent.GetInstanceID();
            ExtendedShopRowsByParent.TryGetValue(instanceId, out int alreadyAddedRows);
            int rowsToAdd = addedRows - alreadyAddedRows;
            if (rowsToAdd <= 0) return;

            var sd = containerRt.sizeDelta;
            sd.y += itemHeight * rowsToAdd;
            containerRt.sizeDelta = sd;
            ExtendedShopRowsByParent[instanceId] = addedRows;
        }

        private static void EnsureBackplaneTopSpacer(ComputerShop computerShop,
                                                     GameObject shopRoot,
                                                     GameObject templateRow,
                                                     float itemHeight)
        {
            if (shopRoot == null || templateRow == null) return;
            if (!HasBoostedSystemXItems(computerShop) && !IsBackplaneBoostServersLoaded()) return;

            const string spacerName = "HL Backplane Top Padding";
            var existing = shopRoot.transform.Find(spacerName);
            GameObject spacer = existing != null ? existing.gameObject : null;

            if (spacer == null)
            {
                spacer = Object.Instantiate(templateRow, shopRoot.transform, false);
                spacer.name = spacerName;
                ClearRowChildren(spacer);
                MelonLogger.Msg("Added Backplane shop top padding for clipped SystemX server row.");
            }

            float height = 12f;

            spacer.transform.SetSiblingIndex(0);
            spacer.SetActive(true);

            var rt = spacer.GetComponent<RectTransform>();
            if (rt != null)
            {
                var sd = rt.sizeDelta;
                sd.y = height;
                rt.sizeDelta = sd;
            }

            var layout = spacer.GetComponent<LayoutElement>();
            if (layout == null)
                layout = spacer.AddComponent<LayoutElement>();

            layout.ignoreLayout = false;
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0f;
        }

        private static bool HasBoostedSystemXItems(ComputerShop computerShop)
        {
            var items = computerShop?.shopItems;
            if (items == null) return false;

            foreach (var item in items)
            {
                if (item == null) continue;

                string name = item.itemDisplayName;
                if (string.IsNullOrEmpty(name) && item.txtName != null)
                    name = item.txtName.text;
                if (string.IsNullOrEmpty(name)) continue;

                if (name.Contains("SystemX") &&
                    (name.Contains("100K") || name.Contains("125K") || name.Contains("500K")))
                    return true;
            }

            return false;
        }

        private static bool IsBackplaneBoostServersLoaded()
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (string.IsNullOrEmpty(name)) continue;
                if (name.Contains("BackplaneBoostServers") ||
                    name.Contains("DataCenterAutomatorServers") ||
                    name.Contains("gregMod.Backplanes"))
                    return true;
            }

            return false;
        }

        private static void RebuildShopLayout(GameObject shopRoot)
        {
            if (shopRoot == null) return;

            Canvas.ForceUpdateCanvases();

            var contentRt = shopRoot.GetComponent<RectTransform>();
            if (contentRt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);

            var scrollRect = shopRoot.GetComponentInParent<ScrollRect>();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;

            Canvas.ForceUpdateCanvases();
        }

        // -----------------------------------------------------------------------
        // Clones an existing shop item GameObject, assigns a new ShopItemSO with
        // the custom module's name/price/ID, and adds it to the given parent.
        // Returns the created GameObject, or null if the ShopItem component is missing.
        // -----------------------------------------------------------------------
        private static GameObject AddShopPackage(ComputerShop computerShop, ShopItem source,
                                               GameObject parent, int prefabID,
                                               string label, int price, int xpToUnlock, string guid,
                                               Sprite icon = null)
        {
            string objectName = $"ShopItem_{label.Replace(" ", "_").Replace("/", "_")}";
            if (parent.transform.Find(objectName) != null)
                return null;

            bool alreadyRegistered = ShopItemAlreadyRegistered(computerShop, prefabID, guid);
            Sprite useSprite = icon ?? BaseQsfpSprite;

            var newSO = ScriptableObject.CreateInstance<ShopItemSO>();
            newSO.itemName   = label;
            newSO.price      = price;
            newSO.xpToUnlock = xpToUnlock;
            newSO.itemType   = PlayerManager.ObjectInHand.SFPBox; // always a box, even from non-box templates
            newSO.itemID     = prefabID;
            newSO.eol        = source.shopItemSO.eol;
            newSO.isCustomColor = source.shopItemSO.isCustomColor;
            newSO.sprite     = useSprite;

            var cloned = Object.Instantiate(source.gameObject, parent.transform, false);
            cloned.name = objectName;
            cloned.transform.localPosition = Vector3.zero;
            cloned.transform.localScale    = Vector3.one;

            var shopItem = cloned.GetComponent<ShopItem>();
            if (shopItem == null)
            {
                MelonLogger.Error($"ShopItem component missing for '{label}'.");
                Object.Destroy(cloned);
                return null;
            }

            shopItem.shopItemSO = newSO;
            shopItem.guid       = guid;
            shopItem.itemDisplayName = label;
            shopItem.isUnlocked = true;

            if (shopItem.txtName != null)
                shopItem.txtName.text = label;
            if (shopItem.txtPrice != null)
                shopItem.txtPrice.text = $"{price} $";
            if (shopItem.txtXpToUnlock != null)
                shopItem.txtXpToUnlock.text = "";
            if (shopItem.unlockButton != null)
                shopItem.unlockButton.SetActive(false);
            if (shopItem.itemIcon != null && useSprite != null)
                shopItem.itemIcon.sprite = useSprite;

            if (!alreadyRegistered)
                RegisterShopItem(computerShop, shopItem);
            cloned.SetActive(true);

            MelonLogger.Msg($"Shop pack added: '{newSO.itemName}' " +
                            $"(prefabID={prefabID}, price={newSO.price}, parent={parent.name})");
            return cloned;
        }

        private static bool ShopItemAlreadyRegistered(ComputerShop computerShop, int prefabID, string guid)
        {
            var items = computerShop?.shopItems;
            if (items == null) return false;

            foreach (var item in items)
            {
                if (item == null) continue;
                if (item.guid == guid) return true;
                if (item.shopItemSO != null && item.shopItemSO.itemID == prefabID)
                    return true;
            }

            return false;
        }

        private static void RegisterShopItem(ComputerShop computerShop, ShopItem shopItem)
        {
            var oldItems = computerShop?.shopItems;
            if (oldItems == null || shopItem == null) return;

            var newItems = new Il2CppReferenceArray<ShopItem>(oldItems.Length + 1);
            for (int i = 0; i < oldItems.Length; i++)
                newItems[i] = oldItems[i];
            newItems[oldItems.Length] = shopItem;
            computerShop.shopItems = newItems;
        }

        // -----------------------------------------------------------------------
        // Builds a box prefab for the 32x shop item. Identical to a regular custom
        // box but with "_bulk_" in the name. The actual slot expansion to 32 happens
        // post-delivery via ExpandAllSizedBoxes — the game re-initializes sfpPositions
        // after instantiation, so upgrading at prefab time has no effect.
        // -----------------------------------------------------------------------
        internal static GameObject BuildBulkBoxPrefab(MainGameManager mgm, int bulkItemID,
                                                      ModuleRegistry.Entry entry,
                                                      Transform parent = null)
        {
            int regularPrefabID = bulkItemID - BULK_ID_BASE + MOD_ID_BASE;
            var box = BuildBoxPrefab(mgm, regularPrefabID, entry, parent);
            if (box == null) return null;

            // Mark with distinctive name so the scanner can identify it.
            box.name = $"SFPBox_bulk_{regularPrefabID}";
            return box;
        }

        // -----------------------------------------------------------------------
        // Tray packs (16/36/64/128 pcs). ID layout: TRAY_ID_BASE +
        // moduleIndex * TraySizeCount + sizeIndex. Capacity in name —
        // like 32x bulk, expansion happens post-delivery.
        // -----------------------------------------------------------------------
        internal static bool IsCustomItemID(int itemID)
        {
            if (itemID >= MOD_ID_BASE && itemID < MOD_ID_BASE + ModuleList.All.Length) return true;
            if (itemID >= BULK_ID_BASE && itemID < BULK_ID_BASE + ModuleList.All.Length) return true;
            if (itemID >= TRAY_ID_BASE && itemID < TRAY_ID_BASE + ModuleList.All.Length * TraySizeCount) return true;
            return false;
        }

        internal static bool IsTrayItemID(int itemID, out int moduleIndex, out int sizeIndex)
        {
            moduleIndex = -1;
            sizeIndex   = -1;
            int offset = itemID - TRAY_ID_BASE;
            if (offset < 0) return false;
            moduleIndex = offset / TraySizeCount;
            sizeIndex   = offset % TraySizeCount;
            return moduleIndex < ModuleList.All.Length;
        }

        internal static int RegularIdForTray(int trayItemID)
        {
            return IsTrayItemID(trayItemID, out int moduleIndex, out _)
                ? MOD_ID_BASE + moduleIndex
                : -1;
        }

        internal static int TraySizeFromItemID(int trayItemID)
        {
            return IsTrayItemID(trayItemID, out _, out int sizeIndex)
                ? TraySizes[sizeIndex]
                : -1;
        }

        internal static GameObject BuildTrayBoxPrefab(MainGameManager mgm, int trayItemID,
                                                      ModuleRegistry.Entry entry,
                                                      Transform parent = null)
        {
            if (!IsTrayItemID(trayItemID, out int moduleIndex, out int sizeIndex)) return null;

            int regularPrefabID = MOD_ID_BASE + moduleIndex;
            int capacity        = TraySizes[sizeIndex];

            var box = BuildBoxPrefab(mgm, regularPrefabID, entry, parent);
            if (box == null) return null;

            box.name = $"SFPBox_tray_{regularPrefabID}_{capacity}";
            return box;
        }

        // -----------------------------------------------------------------------
        // Coroutine that scans the world for size-coded module boxes (tray/bulk)
        // that haven't been expanded yet. Runs until no more un-upgraded boxes
        // remain. Capacity comes from the box name:
        //   "_bulk_"                          → 32 (legacy 32x bulk)
        //   "SFPBox_tray_<regularID>_<cap>"  → that capacity (16/36/64/128)
        // -----------------------------------------------------------------------
        private static bool _boxScannerRunning;

        internal static IEnumerator ExpandAllSizedBoxes()
        {
            if (_boxScannerRunning) yield break;
            _boxScannerRunning = true;

            // Scanner must not give up as soon as no
            // unexpanded box is visible: delivery (checkout)
            // may spawn a fresh tray box seconds later. So
            // poll for a longer window instead of cancelling
            // after the first empty pass.
            float deadline = Time.time + 90f;
            int emptyPasses = 0;

            while (Time.time < deadline)
            {
                bool foundAny = false;
                var allBoxes = Object.FindObjectsOfType<SFPBox>();

                foreach (var box in allBoxes)
                {
                    if (box == null) continue;
                    if (!box.gameObject.activeInHierarchy) continue;

                    int capacity = GetTargetCapacity(box.gameObject.name);
                    if (capacity < 0) continue;
                    if (box.sfpPositions != null && box.sfpPositions.Length >= capacity) continue;

                    UpgradeToBulkBox(box, capacity);
                    foundAny = true;
                }

                if (foundAny) emptyPasses = 0;
                else emptyPasses++;

                // After ~8 empty passes (≈ 12 s with no new box) we can
                // stop — next start (purchase/checkout) reactivates.
                if (emptyPasses >= 8) break;

                yield return new WaitForSeconds(1.5f);
            }

            _boxScannerRunning = false;
        }

        internal static int GetTargetCapacity(string boxName)
        {
            if (string.IsNullOrEmpty(boxName)) return -1;

            // Unity appends " (Clone)" to object names on spawn — strip for
            // capacity parsing.
            string name = boxName.Trim();
            const string cloneSuffix = "(Clone)";
            if (name.EndsWith(cloneSuffix, System.StringComparison.Ordinal))
                name = name.Substring(0, name.Length - cloneSuffix.Length);

            // Legacy 32x bulk pack.
            if (name.IndexOf("_bulk_", System.StringComparison.Ordinal) >= 0) return 32;

            // Tray packs: "SFPBox_tray_<regularID>_<cap>".
            int idx = name.IndexOf("_tray_", System.StringComparison.Ordinal);
            if (idx < 0) return -1;
            string tail = name.Substring(idx + "_tray_".Length);
            int under = tail.LastIndexOf('_');
            if (under < 0) return -1;
            return int.TryParse(tail.Substring(under + 1), out int cap) && cap > 0 ? cap : -1;
        }

        // -----------------------------------------------------------------------
        // Expands a live SFPBox from its vanilla capacity (5) to newCapacity (32)
        // by cloning slot positions and using proper Il2Cpp array types.
        // -----------------------------------------------------------------------
        internal static void UpgradeToBulkBox(SFPBox box, int newCapacity)
        {
            var oldPositions = box.sfpPositions;
            if (oldPositions == null || oldPositions.Length == 0) return;

            int oldCap = oldPositions.Length;
            if (oldCap >= newCapacity) return;

            var newPositions = new Il2CppReferenceArray<Transform>(newCapacity);
            var newUsed      = new Il2CppStructArray<int>(newCapacity);

            int fullSlotValue = box.usedPositions != null && box.usedPositions.Length > 0
                ? box.usedPositions[oldCap - 1] : 1;

            // Copy existing slots.
            for (int i = 0; i < oldCap; i++)
            {
                newPositions[i] = oldPositions[i];
                newUsed[i] = box.usedPositions != null && i < box.usedPositions.Length
                    ? box.usedPositions[i] : 0;
            }

            // Clone new slots from the original positions (round-robin).
            for (int i = oldCap; i < newCapacity; i++)
            {
                int baseIdx = i % oldCap;
                Transform baseSlot = oldPositions[baseIdx];

                var newSlotObj = Object.Instantiate(baseSlot.gameObject, baseSlot.parent);
                newSlotObj.name = $"SFPPositionInBox_{i}";
                newSlotObj.transform.localPosition = baseSlot.localPosition;

                newPositions[i] = newSlotObj.transform;
                newUsed[i] = fullSlotValue;
            }

            box.sfpPositions  = newPositions;
            box.usedPositions = newUsed;

            MelonLogger.Msg($"Upgraded box '{box.gameObject.name}' from {oldCap} → {newCapacity} slots.");
        }

    }
}
