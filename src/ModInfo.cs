using System;
using ICities;
using CitiesHarmony.API;
using HarmonyLib;
using ColossalFramework;
using UnityEngine;

namespace InstantBorderDespawn
{
    public sealed class ModInfo : IUserMod
    {
        public const string HARMONY_ID = "com.ngocduy.instantborderdespawn";
        public const string MOD_NAME = "Instant Border Despawn by NgocDuy";
        public const string MOD_DESCRIPTION = "Instantly clears trains, ships, and vehicles upon arriving at outside connections to eliminate border traffic jams and train gridlocks.";

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

    public sealed class LoadingExtension : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode)
        {
            UnityEngine.Debug.Log("[InstantBorderDespawn] OnLevelLoaded called, mode=" + mode);
            // Cleanup is now handled by SimulationStep prefixes during gameplay.
            // Doing it here caused hangs because ArriveAtDestination on cargo items
            // can trigger FindNextCargoParent/SwitchCargoParent which deadlocks
            // when called during the loading phase.
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
                UnityEngine.Debug.Log("[InstantBorderDespawn] Applying Harmony patches...");
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
