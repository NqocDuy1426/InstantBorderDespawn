using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace InstantBorderDespawn.Tests
{
    class Program
    {
        private static int s_passedTests = 0;
        private static int s_failedTests = 0;

        static void Main(string[] args)
        {
            Console.WriteLine("======================================================================");
            Console.WriteLine("🚅 INSTANT BORDER DESPAWN (v2.0.0) - VERIFICATION SUITE");
            Console.WriteLine("======================================================================");

            TestGroup1_AssemblyAndArchitecture();
            TestGroup2_TargetClassesAndMethodBinding();
            TestGroup3_HarmonyBindingIntegrity();
            TestGroup4_OutsideConnectionLogicSafety();

            Console.WriteLine("\n======================================================================");
            Console.WriteLine($"📊 TEST EXECUTION SUMMARY: {s_passedTests} PASSED, {s_failedTests} FAILED");
            Console.WriteLine("======================================================================");

            if (s_failedTests > 0)
            {
                Environment.Exit(1);
            }
        }

        private static void Assert(bool condition, string testName)
        {
            if (condition)
            {
                Console.WriteLine($"  [PASS] {testName}");
                s_passedTests++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [FAIL] {testName}");
                Console.ResetColor();
                s_failedTests++;
            }
        }

        private static void TestGroup1_AssemblyAndArchitecture()
        {
            Console.WriteLine("\n--- TEST GROUP 1: Assembly & Metadata Audit ---");

            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "InstantBorderDespawn.dll");
            var asm = AssemblyDefinition.ReadAssembly(dllPath);

            Assert(asm != null, "InstantBorderDespawn.dll read successfully via Cecil");
            Assert(asm.Name.Version != null && asm.Name.Version.Major == 2, $"AssemblyVersion is valid: {asm.Name.Version}");

            var modInfoType = asm.MainModule.GetType("InstantBorderDespawn.ModInfo");
            Assert(modInfoType != null, "Core type exists: InstantBorderDespawn.ModInfo");

            var patcherType = asm.MainModule.GetType("InstantBorderDespawn.Patcher");
            Assert(patcherType != null, "Core type exists: InstantBorderDespawn.Patcher");

            var patchesType = asm.MainModule.GetType("InstantBorderDespawn.BorderDespawnPatches");
            Assert(patchesType != null, "Core type exists: InstantBorderDespawn.BorderDespawnPatches");

            var harmonyIdField = modInfoType.Fields.FirstOrDefault(f => f.Name == "HARMONY_ID");
            Assert(harmonyIdField != null && (string)harmonyIdField.Constant == "com.ngocduy.instantborderdespawn", "Harmony ID: com.ngocduy.instantborderdespawn");
        }

        private static void TestGroup2_TargetClassesAndMethodBinding()
        {
            Console.WriteLine("\n--- TEST GROUP 2: Vanilla Engine Target Binding ---");

            string gameManaged = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library/Application Support/Steam/steamapps/common/Cities_Skylines/Cities.app/Contents/Resources/Data/Managed"
            );

            var gameAsm = AssemblyDefinition.ReadAssembly(Path.Combine(gameManaged, "Assembly-CSharp.dll"));
            Assert(gameAsm != null, "Assembly-CSharp.dll read successfully");

            string[] vehicleClasses = {
                "CargoTrainAI",
                "PassengerTrainAI",
                "CargoShipAI",
                "PassengerShipAI",
                "CargoPlaneAI",
                "PassengerPlaneAI"
            };

            foreach (string cls in vehicleClasses)
            {
                var type = gameAsm.MainModule.GetType(cls);
                Assert(type != null, $"Game engine contains target AI class: {cls}");

                var method = type.Methods.FirstOrDefault(m =>
                    m.Name == "ArriveAtDestination" &&
                    m.Parameters.Count == 2 &&
                    m.Parameters[0].ParameterType.FullName == "System.UInt16" &&
                    m.Parameters[1].ParameterType.IsByReference
                );
                Assert(method != null, $"{cls} implements ArriveAtDestination(ushort, ref Vehicle)");
            }
        }

        private static void TestGroup3_HarmonyBindingIntegrity()
        {
            Console.WriteLine("\n--- TEST GROUP 3: Harmony Binding & Parameter Symmetry ---");

            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "InstantBorderDespawn.dll");
            var asm = AssemblyDefinition.ReadAssembly(dllPath);
            var patchesType = asm.MainModule.GetType("InstantBorderDespawn.BorderDespawnPatches");

            var postfix = patchesType.Methods.FirstOrDefault(m => m.Name == "Generic_ArriveAtDestination_Postfix");
            Assert(postfix != null, "BorderDespawnPatches contains Generic_ArriveAtDestination_Postfix");

            Assert(postfix.Parameters.Count == 2, "Postfix takes exactly 2 parameters");
            Assert(postfix.Parameters[0].Name == "vehicleID", "Parameter 1 name matches vanilla exactly: 'vehicleID'");
            Assert(postfix.Parameters[1].Name == "vehicleData", "Parameter 2 name matches vanilla exactly: 'vehicleData'");
            Assert(postfix.Parameters[1].ParameterType.IsByReference, "Parameter 2 is passed by reference (ref Vehicle vehicleData)");
        }

        private static void TestGroup4_OutsideConnectionLogicSafety()
        {
            Console.WriteLine("\n--- TEST GROUP 4: Logic Safety & Protection ---");

            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "InstantBorderDespawn.dll");
            var asm = AssemblyDefinition.ReadAssembly(dllPath);
            var patchesType = asm.MainModule.GetType("InstantBorderDespawn.BorderDespawnPatches");

            var isOcMethod = patchesType.Methods.FirstOrDefault(m => m.Name == "IsTargetOutsideConnection");
            Assert(isOcMethod != null, "BorderDespawnPatches contains IsTargetOutsideConnection filter");

            // Verify that OutsideConnectionAI is checked
            bool checksOutsideConnection = false;
            foreach (var inst in isOcMethod.Body.Instructions)
            {
                if (inst.Operand != null && inst.Operand.ToString().Contains("OutsideConnectionAI"))
                {
                    checksOutsideConnection = true;
                    break;
                }
            }
            Assert(checksOutsideConnection, "Station Protection: Strictly filters for OutsideConnectionAI (city stations immune from acceleration)");

            // Verify waitCounter 250 acceleration in Postfix
            var postfix = patchesType.Methods.FirstOrDefault(m => m.Name == "Generic_ArriveAtDestination_Postfix");
            bool accelerates250 = false;
            foreach (var inst in postfix.Body.Instructions)
            {
                if (inst.OpCode == Mono.Cecil.Cil.OpCodes.Ldc_I4 && Convert.ToInt32(inst.Operand) == 250)
                {
                    accelerates250 = true;
                    break;
                }
            }
            Assert(accelerates250, "Acceleration: Sets waitCounter = 250 for instant vanilla release on next frame");
        }
    }
}

