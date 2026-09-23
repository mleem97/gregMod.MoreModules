using UnityEngine;

namespace GregModMoreModules
{
    
    internal sealed class ModuleDefinition
    {
        /// <summary>Display name shown in the shop.</summary>
        public string DisplayName;

        /// <summary>
        /// Real-world speed in Gbps (e.g. 80 for 80 Gbps).
        /// Converted to the game's internal unit automatically (÷ 5).
        /// Game examples: 10 Gbps → internal 2, 25 Gbps → 5, 40 Gbps → 8.
        /// </summary>
        public float SpeedGbps;

        /// <summary>
        /// Shop price = vanilla QSFP+ box price × PriceMultiplier (rounded to int).
        /// </summary>
        public float PriceMultiplier;

        /// <summary>XP required to unlock in the shop (0 = always unlocked).</summary>
        public int XpToUnlock;

        /// <summary>
        /// Unique string used to persist the shop unlock state across saves.
        /// Never reuse or change this after the module has appeared in a save game.
        /// </summary>
        public string ShopGuid;

        /// <summary>Optional color for the module's box and cable in the game. If not set, it will use the default QSFP+ colors.</summary>
        public Color ModuleColor;

        /// <summary>
        /// Vanilla module prefabID to clone (form factor): 0 = RJ45, 1 = SFP+,
        /// 2 = SFP28, 3 = QSFP+. Determines sfpType (port compatibility) and model.
        /// </summary>
        public int BasePrefabID = 3;

        /// <summary>Index into mgm.sfpsBoxedPrefab for the matching vanilla box.</summary>
        public int BaseBoxIndex = 3;

        /// <summary>Connection word for the shop label ("Fiber", "Copper").</summary>
        public string ConnectionLabel = "Fiber";
        
        // Internal helper — used by Core.cs
        internal float InternalSpeed => SpeedGbps / 5f;
    }

    internal static class ModuleList
    {
        internal static readonly ModuleDefinition[] All =
        {
            new ModuleDefinition
            {
                DisplayName     = "QSFP28 100Gbps",
                SpeedGbps       = 100f,
                PriceMultiplier = 2.5f,
                XpToUnlock      = 0,
                ShopGuid        = "more_sfp_qsfp28_100g_v1",
                ModuleColor     = new Color(0f,   0.8f, 0.3f, 1f),
            },
            
            new ModuleDefinition
            {
                DisplayName     = "QSFP56 200Gbps",
                SpeedGbps       = 200f,
                PriceMultiplier = 4.5f,
                XpToUnlock      = 0,
                ShopGuid        = "more_sfp_qsfp56_200g_v1",
                ModuleColor     = new Color(1f,   0.5f, 0f,   1f),
            },
            
            new ModuleDefinition
            {
                DisplayName     = "QSFP-DD 400Gbps",
                SpeedGbps       = 400f,
                PriceMultiplier = 6.5f,
                XpToUnlock      = 0,
                ShopGuid        = "more_sfp_qsfpdd_400g_v1",
                ModuleColor     = new Color(1f, 0.75f, 0f, 1f),
            },
            
            new ModuleDefinition
            {
                DisplayName     = "QSFP-DD 800Gbps",
                SpeedGbps       = 800f,
                PriceMultiplier = 9f,
                XpToUnlock      = 0,
                ShopGuid        = "more_sfp_qsfpdd_800g_v1",
                ModuleColor     = new Color(0.9f, 0.05f, 0.05f, 1f),
            },
            
            new ModuleDefinition
            {
                DisplayName     = "QSFP-DWDM 1600Gbps",
                SpeedGbps       = 1600f,
                PriceMultiplier = 16f,
                XpToUnlock      = 0,
                ShopGuid        = "more_sfp_qsfp_dwdm_1600g_v1",
                ModuleColor     = new Color(0.6f, 0f, 1f, 1f),
            },

            new ModuleDefinition
            {
                DisplayName     = "QSFP-DWDM 3200Gbps",
                SpeedGbps       = 3200f,
                PriceMultiplier = 28f,
                XpToUnlock      = 0,
                ShopGuid        = "more_sfp_qsfp_dwdm_3200g_v1",
                ModuleColor     = new Color(1f, 0f, 0.6f, 1f),
            },
            
            new ModuleDefinition
            {
                DisplayName     = "QSFP-DWDM 6400Gbps",
                SpeedGbps       = 6400f,
                PriceMultiplier = 48f,
                XpToUnlock      = 0,
                ShopGuid        = "more_sfp_qsfp_dwdm_6400g_v1",
                ModuleColor     = new Color(0f, 0.9f, 0.9f, 1f),
            },

            new ModuleDefinition
            {
                DisplayName     = "RJ45 10Gbps",
                SpeedGbps       = 10f,
                PriceMultiplier = 1f,
                XpToUnlock      = 0,
                ShopGuid        = "more_sfp_rj45_10g_v1",
                ModuleColor     = new Color(0.72f, 0.45f, 0.2f, 1f),
                BasePrefabID    = 0,
                BaseBoxIndex    = 0,
                ConnectionLabel = "Copper",
            },

            new ModuleDefinition
            {
                DisplayName     = "SFP+ 10Gbps",
                SpeedGbps       = 10f,
                PriceMultiplier = 1.2f,
                XpToUnlock      = 0,
                ShopGuid        = "more_sfp_sfpplus_10g_v1",
                ModuleColor     = new Color(0.2f, 0.5f, 0.9f, 1f),
                BasePrefabID    = 1,
                BaseBoxIndex    = 1,
            },

            new ModuleDefinition
            {
                DisplayName     = "SFP28 25Gbps",
                SpeedGbps       = 25f,
                PriceMultiplier = 1.5f,
                XpToUnlock      = 0,
                ShopGuid        = "more_sfp_sfp28_25g_v1",
                ModuleColor     = new Color(0.1f, 0.8f, 0.7f, 1f),
                BasePrefabID    = 2,
                BaseBoxIndex    = 2,
            },
        };
    }
}
