using System;
using System.Reflection;
using ColossalFramework;
using HarmonyLib;

namespace InstantBorderDespawn
{
    /// <summary>
    /// Instant Border Despawn v2.0.0
    /// 
    /// Instead of forcefully deleting vehicles (which causes infinite loops or missed export revenue),
    /// we attach a postfix to ArriveAtDestination for trains, ships, and planes.
    /// 
    /// Vanilla Flow:
    /// 1. Vehicle reaches border outside connection.
    /// 2. Frame 0: Vanilla ArriveAtDestination calls ArriveAtTarget, crediting export revenue
    ///    and unloading goods, then sets Vehicle.Flags.WaitingLoading (0x02000000) and waitCounter = 0.
    /// 3. Our Postfix: Detects WaitingLoading flag at Outside Connection, sets waitCounter = 250.
    /// 4. Frame 1: Vanilla ArriveAtDestination sees waitCounter >= 16, cleanly releases vehicle
    ///    (locomotive, all trailing wagons, cargo items) via VehicleManager.ReleaseVehicle.
    /// 
    /// Result: Instant despawn at border with 100% vanilla safety and accurate export revenue!
    /// </summary>
    public static class BorderDespawnPatches
    {
        private static int s_acceleratedCount = 0;

        public static void Apply(Harmony harmony)
        {
            if (harmony == null) return;

            string[] vehicleClasses = {
                "CargoTrainAI",
                "PassengerTrainAI",
                "CargoShipAI",
                "PassengerShipAI",
                "CargoPlaneAI",
                "PassengerPlaneAI"
            };

            MethodInfo postfix = typeof(BorderDespawnPatches).GetMethod(
                nameof(Generic_ArriveAtDestination_Postfix),
                BindingFlags.Public | BindingFlags.Static
            );

            foreach (string cls in vehicleClasses)
            {
                try
                {
                    Type t = typeof(VehicleAI).Assembly.GetType(cls);
                    if (t == null)
                    {
                        Log("Class not found: " + cls);
                        continue;
                    }

                    MethodInfo original = t.GetMethod(
                        "ArriveAtDestination",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                        null,
                        new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType() },
                        null
                    );

                    if (original == null)
                    {
                        Log("Method not found: " + cls + ".ArriveAtDestination");
                        continue;
                    }

                    harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                    Log("Successfully patched " + cls + ".ArriveAtDestination (postfix)");
                }
                catch (Exception ex)
                {
                    Log("Error patching " + cls + ": " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Postfix on ArriveAtDestination(ushort vehicleID, ref Vehicle vehicleData).
        /// Parameter names MUST match the target method's parameters for Harmony binding.
        /// </summary>
        public static void Generic_ArriveAtDestination_Postfix(ushort vehicleID, ref Vehicle vehicleData)
        {
            try
            {
                // Skip if vehicle has already been released/unspawned
                if ((vehicleData.m_flags & Vehicle.Flags.Created) == 0) return;

                // Only accelerate at outside connections
                if (!IsTargetOutsideConnection(vehicleData.m_targetBuilding)) return;

                // WaitingLoading flag (0x02000000) indicates ArriveAtTarget has completed
                // delivery, credited revenue, and set the vehicle into waiting state.
                if ((vehicleData.m_flags & Vehicle.Flags.WaitingLoading) != 0)
                {
                    // Max out wait counter so vanilla will release on the next simulation frame
                    vehicleData.m_waitCounter = 250;
                    Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_waitCounter = 250;

                    s_acceleratedCount++;
                    if (s_acceleratedCount <= 10 || s_acceleratedCount % 50 == 0)
                    {
                        VehicleInfo info = vehicleData.Info;
                        string aiName = info?.m_vehicleAI?.GetType().Name ?? "UnknownAI";
                        Log(string.Format("Accelerated border despawn #{0}: vID={1} ({2}) at OC={3}",
                            s_acceleratedCount, vehicleID, aiName, vehicleData.m_targetBuilding));
                    }
                }
            }
            catch
            {
                // Never disrupt vanilla simulation
            }
        }

        private static bool IsTargetOutsideConnection(ushort targetBuilding)
        {
            if (targetBuilding == 0) return false;
            BuildingManager bm = Singleton<BuildingManager>.instance;
            if (bm == null || targetBuilding >= bm.m_buildings.m_buffer.Length) return false;

            BuildingInfo bInfo = bm.m_buildings.m_buffer[targetBuilding].Info;
            return bInfo != null && bInfo.m_buildingAI is OutsideConnectionAI;
        }

        private static void Log(string msg)
        {
            UnityEngine.Debug.Log("[InstantBorderDespawn] " + msg);
        }
    }
}
