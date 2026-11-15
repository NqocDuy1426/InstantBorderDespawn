using System;
using System.Reflection;
using ColossalFramework;
using HarmonyLib;
using UnityEngine;

namespace InstantBorderDespawn
{
    public static class BorderDespawnPatches
    {
        public static void Apply(Harmony harmony)
        {
            if (harmony == null) return;

            // 1. TrainAI.SimulationStep
            Patch(harmony, typeof(TrainAI), "SimulationStep",
                new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vehicle.Frame).MakeByRefType(), typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(int) },
                prefix: typeof(BorderDespawnPatches).GetMethod(nameof(TrainAI_SimulationStep_Prefix), BindingFlags.Public | BindingFlags.Static));

            // 2. CargoTrainAI.ArriveAtTarget
            Patch(harmony, typeof(CargoTrainAI), "ArriveAtTarget",
                new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType() },
                prefix: typeof(BorderDespawnPatches).GetMethod(nameof(CargoTrainAI_ArriveAtTarget_Prefix), BindingFlags.Public | BindingFlags.Static));

            // 3. PassengerTrainAI.ArriveAtTarget
            Patch(harmony, typeof(PassengerTrainAI), "ArriveAtTarget",
                new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType() },
                prefix: typeof(BorderDespawnPatches).GetMethod(nameof(PassengerTrainAI_ArriveAtTarget_Prefix), BindingFlags.Public | BindingFlags.Static));

            // 4. ShipAI.SimulationStep
            Patch(harmony, typeof(ShipAI), "SimulationStep",
                new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vehicle.Frame).MakeByRefType(), typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(int) },
                prefix: typeof(BorderDespawnPatches).GetMethod(nameof(ShipAI_SimulationStep_Prefix), BindingFlags.Public | BindingFlags.Static));

            // 5. CargoShipAI.ArriveAtTarget
            Patch(harmony, typeof(CargoShipAI), "ArriveAtTarget",
                new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType() },
                prefix: typeof(BorderDespawnPatches).GetMethod(nameof(CargoShipAI_ArriveAtTarget_Prefix), BindingFlags.Public | BindingFlags.Static));

            // 6. PassengerShipAI.ArriveAtTarget
            Patch(harmony, typeof(PassengerShipAI), "ArriveAtTarget",
                new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType() },
                prefix: typeof(BorderDespawnPatches).GetMethod(nameof(PassengerShipAI_ArriveAtTarget_Prefix), BindingFlags.Public | BindingFlags.Static));

            // 7. CarAI.SimulationStep
            Patch(harmony, typeof(CarAI), "SimulationStep",
                new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vehicle.Frame).MakeByRefType(), typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(int) },
                prefix: typeof(BorderDespawnPatches).GetMethod(nameof(CarAI_SimulationStep_Prefix), BindingFlags.Public | BindingFlags.Static));
        }

        private static void Patch(Harmony harmony, Type targetType, string methodName, Type[] paramTypes, MethodInfo prefix = null, MethodInfo postfix = null)
        {
            try
            {
                if (targetType == null) return;
                MethodInfo m = targetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, paramTypes, null);
                if (m != null)
                {
                    HarmonyMethod hPre = (prefix != null) ? new HarmonyMethod(prefix) { priority = Priority.First } : null;
                    HarmonyMethod hPost = (postfix != null) ? new HarmonyMethod(postfix) : null;
                    harmony.Patch(m, prefix: hPre, postfix: hPost);
                    UnityEngine.Debug.Log(string.Format("[InstantBorderDespawn] Patched {0}.{1}", targetType.Name, methodName));
                }
                else
                {
                    UnityEngine.Debug.LogWarning(string.Format("[InstantBorderDespawn] Method {0}.{1} not found!", targetType.Name, methodName));
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(string.Format("[InstantBorderDespawn] Error patching {0}.{1}: {2}", targetType?.Name, methodName, ex.Message));
            }
        }

        public static void UnloadExportCargo(ushort leaderID, ref Vehicle leaderData)
        {
            try
            {
                VehicleManager vm = Singleton<VehicleManager>.instance;
                if (vm == null) return;
                ushort cargo = leaderData.m_firstCargo;
                leaderData.m_firstCargo = 0;
                int limit = 0;
                while (cargo != 0 && limit++ < 16384)
                {
                    ushort next = vm.m_vehicles.m_buffer[cargo].m_nextCargo;
                    vm.m_vehicles.m_buffer[cargo].m_nextCargo = 0;
                    vm.m_vehicles.m_buffer[cargo].m_cargoParent = 0;
                    VehicleInfo cInfo = vm.m_vehicles.m_buffer[cargo].Info;
                    if (cInfo != null && cInfo.m_vehicleAI != null)
                    {
                        cInfo.m_vehicleAI.ArriveAtDestination(cargo, ref vm.m_vehicles.m_buffer[cargo]);
                    }
                    vm.ReleaseVehicle(cargo);
                    cargo = next;
                }
            }
            catch
            {
            }
        }

        public static bool TrainAI_SimulationStep_Prefix(TrainAI __instance, ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
        {
            if (vehicleID != leaderID) return true;
            try
            {
                ushort target = leaderData.m_targetBuilding;
                if (target != 0)
                {
                    BuildingManager bm = Singleton<BuildingManager>.instance;
                    if (bm != null && target < bm.m_buildings.m_buffer.Length)
                    {
                        BuildingInfo bInfo = bm.m_buildings.m_buffer[target].Info;
                        if (bInfo != null && bInfo.m_buildingAI is OutsideConnectionAI)
                        {
                            Vector3 targetPos = bm.m_buildings.m_buffer[target].m_position;
                            Vector3 trainPos = frameData.m_position;
                            float dx = targetPos.x - trainPos.x;
                            float dz = targetPos.z - trainPos.z;
                            // If within 150m of outside connection:
                            if (dx * dx + dz * dz < 22500f)
                            {
                                UnloadExportCargo(leaderID, ref leaderData);
                                Singleton<VehicleManager>.instance.ReleaseVehicle(leaderID);
                                return false;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return true;
        }

        public static bool CargoTrainAI_ArriveAtTarget_Prefix(CargoTrainAI __instance, ushort vehicleID, ref Vehicle data, ref bool __result)
        {
            ref Vehicle vehicleData = ref data;
            try
            {
                BuildingManager bm = Singleton<BuildingManager>.instance;
                if (bm == null) return true;

                ushort target = vehicleData.m_targetBuilding;
                if (target != 0 && target < bm.m_buildings.m_buffer.Length)
                {
                    BuildingInfo bInfo = bm.m_buildings.m_buffer[target].Info;
                    if (bInfo != null && bInfo.m_buildingAI is OutsideConnectionAI)
                    {
                        UnloadExportCargo(vehicleID, ref vehicleData);
                        Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
                        __result = true;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[InstantBorderDespawn] CargoTrainAI_ArriveAtTarget_Prefix error: " + ex);
            }
            return true;
        }

        public static bool PassengerTrainAI_ArriveAtTarget_Prefix(PassengerTrainAI __instance, ushort vehicleID, ref Vehicle data, ref bool __result)
        {
            ref Vehicle vehicleData = ref data;
            try
            {
                BuildingManager bm = Singleton<BuildingManager>.instance;
                if (bm == null) return true;

                ushort target = vehicleData.m_targetBuilding;
                if (target != 0 && target < bm.m_buildings.m_buffer.Length)
                {
                    BuildingInfo bInfo = bm.m_buildings.m_buffer[target].Info;
                    if (bInfo == null || bInfo.m_buildingAI is OutsideConnectionAI)
                    {
                        Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
                        __result = true;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[InstantBorderDespawn] PassengerTrainAI_ArriveAtTarget_Prefix error: " + ex);
            }
            return true;
        }

        public static bool ShipAI_SimulationStep_Prefix(ShipAI __instance, ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
        {
            if (vehicleID != leaderID) return true;
            try
            {
                ushort target = leaderData.m_targetBuilding;
                if (target != 0)
                {
                    BuildingManager bm = Singleton<BuildingManager>.instance;
                    if (bm != null && target < bm.m_buildings.m_buffer.Length)
                    {
                        BuildingInfo bInfo = bm.m_buildings.m_buffer[target].Info;
                        if (bInfo != null && bInfo.m_buildingAI is OutsideConnectionAI)
                        {
                            Vector3 targetPos = bm.m_buildings.m_buffer[target].m_position;
                            Vector3 shipPos = frameData.m_position;
                            float dx = targetPos.x - shipPos.x;
                            float dz = targetPos.z - shipPos.z;
                            // If within 250m of outside connection:
                            if (dx * dx + dz * dz < 62500f)
                            {
                                UnloadExportCargo(leaderID, ref leaderData);
                                Singleton<VehicleManager>.instance.ReleaseVehicle(leaderID);
                                return false;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return true;
        }

        public static bool CargoShipAI_ArriveAtTarget_Prefix(CargoShipAI __instance, ushort vehicleID, ref Vehicle data, ref bool __result)
        {
            ref Vehicle vehicleData = ref data;
            try
            {
                BuildingManager bm = Singleton<BuildingManager>.instance;
                if (bm == null) return true;

                ushort target = vehicleData.m_targetBuilding;
                if (target != 0 && target < bm.m_buildings.m_buffer.Length)
                {
                    BuildingInfo bInfo = bm.m_buildings.m_buffer[target].Info;
                    if (bInfo != null && bInfo.m_buildingAI is OutsideConnectionAI)
                    {
                        UnloadExportCargo(vehicleID, ref vehicleData);
                        Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
                        __result = true;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[InstantBorderDespawn] CargoShipAI_ArriveAtTarget_Prefix error: " + ex);
            }
            return true;
        }

        public static bool PassengerShipAI_ArriveAtTarget_Prefix(PassengerShipAI __instance, ushort vehicleID, ref Vehicle data, ref bool __result)
        {
            ref Vehicle vehicleData = ref data;
            try
            {
                BuildingManager bm = Singleton<BuildingManager>.instance;
                if (bm == null) return true;

                ushort target = vehicleData.m_targetBuilding;
                if (target != 0 && target < bm.m_buildings.m_buffer.Length)
                {
                    BuildingInfo bInfo = bm.m_buildings.m_buffer[target].Info;
                    if (bInfo == null || bInfo.m_buildingAI is OutsideConnectionAI)
                    {
                        Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
                        __result = true;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[InstantBorderDespawn] PassengerShipAI_ArriveAtTarget_Prefix error: " + ex);
            }
            return true;
        }

        public static bool CarAI_SimulationStep_Prefix(CarAI __instance, ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
        {
            if (vehicleID != leaderID) return true;
            try
            {
                ushort target = leaderData.m_targetBuilding;
                if (target != 0)
                {
                    BuildingManager bm = Singleton<BuildingManager>.instance;
                    if (bm != null && target < bm.m_buildings.m_buffer.Length)
                    {
                        BuildingInfo bInfo = bm.m_buildings.m_buffer[target].Info;
                        if (bInfo != null && bInfo.m_buildingAI is OutsideConnectionAI)
                        {
                            Vector3 targetPos = bm.m_buildings.m_buffer[target].m_position;
                            Vector3 carPos = frameData.m_position;
                            float dx = targetPos.x - carPos.x;
                            float dz = targetPos.z - carPos.z;
                            // If within 100m of highway outside connection:
                            if (dx * dx + dz * dz < 10000f)
                            {
                                UnloadExportCargo(leaderID, ref leaderData);
                                Singleton<VehicleManager>.instance.ReleaseVehicle(leaderID);
                                return false;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return true;
        }
    }
}
