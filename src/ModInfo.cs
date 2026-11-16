using System;
using ICities;
using CitiesHarmony.API;
using HarmonyLib;

namespace InstantBorderDespawn
{
    public sealed class ModInfo : IUserMod
    {
        public const string HARMONY_ID = "com.ngocduy.instantborderdespawn";
        public const string MOD_NAME = "Instant Border Despawn by NgocDuy";
        public const string MOD_DESCRIPTION = "Accelerates vehicle release at outside connections. No more waiting 16-64 frames — vehicles despawn on the next frame after delivery.";

        public string Name => MOD_NAME;
        public string Description => MOD_DESCRIPTION;

        public void OnEnabled()
        {
            HarmonyHelper.DoOnHarmonyReady(() =>
            {
                Patcher.PatchAll();
            });
        }

        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                Patcher.UnpatchAll();
            }
        }
    }

    public static class Patcher
    {
        private static bool s_isPatched = false;

        public static void PatchAll()
        {
            if (s_isPatched) return;
            try
            {
                UnityEngine.Debug.Log("[InstantBorderDespawn] v2.0.0 — Applying Harmony patches...");
                Harmony harmony = new Harmony(ModInfo.HARMONY_ID);
                BorderDespawnPatches.Apply(harmony);
                s_isPatched = true;
                UnityEngine.Debug.Log("[InstantBorderDespawn] All Harmony patches applied successfully.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[InstantBorderDespawn] Patcher.PatchAll error: " + ex);
            }
        }

        public static void UnpatchAll()
        {
            if (!s_isPatched) return;
            try
            {
                UnityEngine.Debug.Log("[InstantBorderDespawn] Reverting Harmony patches...");
                Harmony harmony = new Harmony(ModInfo.HARMONY_ID);
                harmony.UnpatchAll(ModInfo.HARMONY_ID);
                s_isPatched = false;
                UnityEngine.Debug.Log("[InstantBorderDespawn] All Harmony patches reverted.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[InstantBorderDespawn] Patcher.UnpatchAll error: " + ex);
            }
        }
    }
}
