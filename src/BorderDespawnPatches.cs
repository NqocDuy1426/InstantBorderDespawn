using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using HarmonyLib;
using UnityEngine;

namespace InstantBorderDespawn
{
    public static class BorderDespawnPatches
    {
        private static int s_despawnedTrains = 0;
        private static int s_despawnedTrucks = 0;
        private static int s_despawnedShips = 0;

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

            // 8. CargoTruckAI.ArriveAtTarget
            Patch(harmony, typeof(CargoTruckAI), "ArriveAtTarget",
                new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType() },
                prefix: typeof(BorderDespawnPatches).GetMethod(nameof(CargoTruckAI_ArriveAtTarget_Prefix), BindingFlags.Public | BindingFlags.Static));
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

        /// <summary>
        /// Check if vehicle is near its target OC building. Used by ArriveAtTarget prefixes.
        /// </summary>
        public static bool IsAtOutsideConnection(ushort targetBuilding, Vector3 vehiclePos, float maxDistance = 150f)
        {
            if (targetBuilding == 0) return false;
            BuildingManager bm = Singleton<BuildingManager>.instance;
            if (bm == null || targetBuilding >= bm.m_buildings.m_buffer.Length) return false;

            BuildingInfo bInfo = bm.m_buildings.m_buffer[targetBuilding].Info;
            if (bInfo == null || !(bInfo.m_buildingAI is OutsideConnectionAI)) return false;

            Vector3 targetPos = bm.m_buildings.m_buffer[targetBuilding].m_position;
            float dx = targetPos.x - vehiclePos.x;
            float dz = targetPos.z - vehiclePos.z;
            float maxDistSqr = maxDistance * maxDistance;

            if (dx * dx + dz * dz <= maxDistSqr) return true;

            return false;
        }

        /// <summary>
        /// Check if vehicle is at map edge AND heading to an outside connection.
        /// Used by SimulationStep prefixes as a safety net to catch stuck vehicles near borders.
        /// Only triggers at the absolute map edge to avoid despawning vehicles mid-journey.
        /// </summary>
        public static bool IsAtMapEdgeHeadingOut(ushort targetBuilding, Vector3 vehiclePos)
        {
            // Must be heading to an outside connection
            if (targetBuilding == 0) return false;
            BuildingManager bm = Singleton<BuildingManager>.instance;
            if (bm == null || targetBuilding >= bm.m_buildings.m_buffer.Length) return false;

            BuildingInfo bInfo = bm.m_buildings.m_buffer[targetBuilding].Info;
            if (bInfo == null || !(bInfo.m_buildingAI is OutsideConnectionAI)) return false;

            // Only despawn at absolute map edge (25-tile: ±4800m)
            float absX = Mathf.Abs(vehiclePos.x);
            float absZ = Mathf.Abs(vehiclePos.z);
            if (absX >= 4700f || absZ >= 4700f) return true;

            return false;
        }

        public static void DespawnTrain(ushort leaderID, ref Vehicle leaderData)
        {
            try
            {
                VehicleManager vm = Singleton<VehicleManager>.instance;
                if (vm == null) return;

                // 1. Collect all vehicles in this train chain (locomotive + wagons)
                List<ushort> chain = new List<ushort>();
                ushort current = leaderID;
                int guard = 0;
                while (current != 0 && guard++ < 100)
                {
                    chain.Add(current);
                    current = vm.m_vehicles.m_buffer[current].m_trailingVehicle;
                }

                // 2. Unload cargo on EVERY vehicle in the chain
                int totalCargoCount = 0;
                for (int i = 0; i < chain.Count; i++)
                {
                    ushort vID = chain[i];
                    ushort cargo = vm.m_vehicles.m_buffer[vID].m_firstCargo;
                    vm.m_vehicles.m_buffer[vID].m_firstCargo = 0;
                    int cGuard = 0;
                    while (cargo != 0 && cGuard++ < 16384)
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
                        totalCargoCount++;
                    }
                }

                // 3. Release wagons and locomotive in reverse order
                for (int i = chain.Count - 1; i >= 0; i--)
                {
                    ushort vID = chain[i];
                    vm.m_vehicles.m_buffer[vID].m_trailingVehicle = 0;
                    vm.m_vehicles.m_buffer[vID].m_leadingVehicle = 0;
                    vm.ReleaseVehicle(vID);
                }

                s_despawnedTrains++;
                if (s_despawnedTrains % 25 == 0 || totalCargoCount > 0)
                {
                    UnityEngine.Debug.Log(string.Format("[InstantBorderDespawn] Cleared train #{0} (wagons: {1}, cargo: {2})", s_despawnedTrains, chain.Count - 1, totalCargoCount));
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[InstantBorderDespawn] DespawnTrain error: " + ex);
            }
        }

        public static void DespawnCarOrTruck(ushort leaderID, ref Vehicle leaderData)
        {
            try
            {
                VehicleManager vm = Singleton<VehicleManager>.instance;
                if (vm == null) return;

                ushort trailer = leaderData.m_trailingVehicle;
                if (trailer != 0)
                {
                    leaderData.m_trailingVehicle = 0;
                    vm.m_vehicles.m_buffer[trailer].m_leadingVehicle = 0;
                    UnloadVehicleCargo(trailer, ref vm.m_vehicles.m_buffer[trailer]);
                    vm.ReleaseVehicle(trailer);
                }

                UnloadVehicleCargo(leaderID, ref leaderData);
                vm.ReleaseVehicle(leaderID);

                s_despawnedTrucks++;
                if (s_despawnedTrucks % 100 == 0)
                {
                    UnityEngine.Debug.Log(string.Format("[InstantBorderDespawn] Cleared road vehicle #{0}", s_despawnedTrucks));
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[InstantBorderDespawn] DespawnCarOrTruck error: " + ex);
            }
        }

        public static void DespawnShip(ushort leaderID, ref Vehicle leaderData)
        {
            try
            {
                VehicleManager vm = Singleton<VehicleManager>.instance;
                if (vm == null) return;

                UnloadVehicleCargo(leaderID, ref leaderData);
                vm.ReleaseVehicle(leaderID);

                s_despawnedShips++;
                UnityEngine.Debug.Log(string.Format("[InstantBorderDespawn] Cleared ship #{0}", s_despawnedShips));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[InstantBorderDespawn] DespawnShip error: " + ex);
            }
        }

        private static void UnloadVehicleCargo(ushort vID, ref Vehicle vData)
        {
            VehicleManager vm = Singleton<VehicleManager>.instance;
            if (vm == null) return;
            ushort cargo = vData.m_firstCargo;
            vData.m_firstCargo = 0;
            int cGuard = 0;
            while (cargo != 0 && cGuard++ < 16384)
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

        public static bool TrainAI_SimulationStep_Prefix(TrainAI __instance, ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
        {
            if (vehicleID != leaderID) return true;
            try
            {
                // Safety net: only despawn at absolute map edge, not mid-journey
                if (IsAtMapEdgeHeadingOut(leaderData.m_targetBuilding, frameData.m_position))
                {
                    DespawnTrain(leaderID, ref leaderData);
                    return false;
                }
            }
            catch
            {
            }
            return true;
        }

        public static bool CargoTrainAI_ArriveAtTarget_Prefix(CargoTrainAI __instance, ushort vehicleID, ref Vehicle data, ref bool __result)
        {
            try
            {
                BuildingManager bm = Singleton<BuildingManager>.instance;
                ushort target = data.m_targetBuilding;
                if (bm != null && target != 0 && target < bm.m_buildings.m_buffer.Length)
                {
                    BuildingInfo bInfo = bm.m_buildings.m_buffer[target].Info;
                    if (bInfo != null && bInfo.m_buildingAI is OutsideConnectionAI)
                    {
                        DespawnTrain(vehicleID, ref data);
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
            try
            {
                BuildingManager bm = Singleton<BuildingManager>.instance;
                ushort target = data.m_targetBuilding;
                if (bm != null && target != 0 && target < bm.m_buildings.m_buffer.Length)
                {
                    BuildingInfo bInfo = bm.m_buildings.m_buffer[target].Info;
                    if (bInfo != null && bInfo.m_buildingAI is OutsideConnectionAI)
                    {
                        DespawnTrain(vehicleID, ref data);
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
                // Safety net: only despawn at absolute map edge
                if (IsAtMapEdgeHeadingOut(leaderData.m_targetBuilding, frameData.m_position))
                {
                    DespawnShip(leaderID, ref leaderData);
                    return false;
                }
            }
            catch
            {
            }
            return true;
        }

        public static bool CargoShipAI_ArriveAtTarget_Prefix(CargoShipAI __instance, ushort vehicleID, ref Vehicle data, ref bool __result)
        {
            try
            {
                BuildingManager bm = Singleton<BuildingManager>.instance;
                ushort target = data.m_targetBuilding;
                if (bm != null && target != 0 && target < bm.m_buildings.m_buffer.Length)
                {
                    BuildingInfo bInfo = bm.m_buildings.m_buffer[target].Info;
                    if (bInfo != null && bInfo.m_buildingAI is OutsideConnectionAI)
                    {
                        DespawnShip(vehicleID, ref data);
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
            try
            {
                BuildingManager bm = Singleton<BuildingManager>.instance;
                ushort target = data.m_targetBuilding;
                if (bm != null && target != 0 && target < bm.m_buildings.m_buffer.Length)
                {
                    BuildingInfo bInfo = bm.m_buildings.m_buffer[target].Info;
                    if (bInfo != null && bInfo.m_buildingAI is OutsideConnectionAI)
                    {
                        DespawnShip(vehicleID, ref data);
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
                // Safety net: only despawn at absolute map edge
                if (IsAtMapEdgeHeadingOut(leaderData.m_targetBuilding, frameData.m_position))
                {
                    DespawnCarOrTruck(leaderID, ref leaderData);
                    return false;
                }
            }
            catch
            {
            }
            return true;
        }

        public static bool CargoTruckAI_ArriveAtTarget_Prefix(CargoTruckAI __instance, ushort vehicleID, ref Vehicle data, ref bool __result)
        {
            try
            {
                BuildingManager bm = Singleton<BuildingManager>.instance;
                ushort target = data.m_targetBuilding;
                if (bm != null && target != 0 && target < bm.m_buildings.m_buffer.Length)
                {
                    BuildingInfo bInfo = bm.m_buildings.m_buffer[target].Info;
                    if (bInfo != null && bInfo.m_buildingAI is OutsideConnectionAI)
                    {
                        DespawnCarOrTruck(vehicleID, ref data);
                        __result = true;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[InstantBorderDespawn] CargoTruckAI_ArriveAtTarget_Prefix error: " + ex);
            }
            return true;
        }
    }
}
