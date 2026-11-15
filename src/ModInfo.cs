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
            if (mode == LoadMode.LoadGame || mode == LoadMode.NewGame)
            {
                CleanDeadVehiclesAtBorder();
            }
        }

        private static void CleanDeadVehiclesAtBorder()
        {
            try
            {
                VehicleManager vm = Singleton<VehicleManager>.instance;
                BuildingManager bm = Singleton<BuildingManager>.instance;
                if (vm == null || bm == null) return;

                int cleaned = 0;
                for (ushort i = 1; i < vm.m_vehicles.m_buffer.Length; i++)
                {
                    if ((vm.m_vehicles.m_buffer[i].m_flags & Vehicle.Flags.Created) == 0) continue;

                    ushort target = vm.m_vehicles.m_buffer[i].m_targetBuilding;
                    if (target != 0 && target < bm.m_buildings.m_buffer.Length)
                    {
                        BuildingInfo bInfo = bm.m_buildings.m_buffer[target].Info;
                        if (bInfo != null && bInfo.m_buildingAI is OutsideConnectionAI)
                        {
                            Vector3 pos = vm.m_vehicles.m_buffer[i].GetLastFramePosition();
                            if (BorderDespawnPatches.IsNearOwnTargetOC(target, pos, 300f))
                            {
                                if (vm.m_vehicles.m_buffer[i].Info != null && vm.m_vehicles.m_buffer[i].Info.m_vehicleAI is TrainAI)
                                {
                                    if (vm.m_vehicles.m_buffer[i].m_leadingVehicle == 0)
                                    {
                                        BorderDespawnPatches.DespawnTrain(i, ref vm.m_vehicles.m_buffer[i]);
                                        cleaned++;
                                    }
                                }
                                else if (vm.m_vehicles.m_buffer[i].Info != null && vm.m_vehicles.m_buffer[i].Info.m_vehicleAI is CarAI)
                                {
                                    if (vm.m_vehicles.m_buffer[i].m_leadingVehicle == 0)
                                    {
                                        BorderDespawnPatches.DespawnCarOrTruck(i, ref vm.m_vehicles.m_buffer[i]);
                                        cleaned++;
                                    }
                                }
                            }
                        }
                    }
                }
                if (cleaned > 0)
                {
                    UnityEngine.Debug.Log(string.Format("[InstantBorderDespawn] LevelLoaded: Cleaned up {0} stranded/dead vehicles at borders.", cleaned));
                }
                else
                {
                    UnityEngine.Debug.Log("[InstantBorderDespawn] LevelLoaded: No stranded vehicles found at borders.");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[InstantBorderDespawn] CleanDeadVehiclesAtBorder error: " + ex);
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
