# Instant Border Despawn for Cities: Skylines 1

[![Steam Workshop](https://img.shields.io/badge/Steam%20Workshop-3841011562-blue?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3841011562)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**The definitive fix for border traffic jams and train gridlocks.**  
*By NgocDuy*

[👉 Subscribe on Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3841011562)

---

## 🛑 The Problem

In Cities: Skylines, vehicles exporting goods or carrying passengers across the map border (to Outside Connections) enter a forced waiting loop.
- **Trains and Ships** linger at border connection nodes for **16 to 64 simulation ticks** before despawning.
- As the city grows and export volumes increase, incoming and outgoing trains pile up at the map edge.
- This creates massive **border rail gridlocks** stretching several kilometers back into your city.
- **The consequence:** Cargo trains cannot deliver export goods. Factories across your industrial and oil districts display **"Not enough buyers"**, export income stops, and the municipal budget crashes.

---

## ⚡ The Solution: Instant Border Despawn (v2.0.0)

**Instant Border Despawn** cleanly eliminates border waiting delays using an ultra-safe, lightweight Harmony postfix architecture:

1. **Full Vanilla Export Credit (Frame 0):** When a vehicle reaches an outside connection, the vanilla game's `ArriveAtTarget` executes completely. All export revenue is credited to your city budget, material buffers are updated, and goods are officially delivered.
2. **Instant Release Acceleration:** Once the delivery completes and the vehicle enters its waiting phase (`Vehicle.Flags.WaitingLoading`), our postfix instantly advances the vehicle's wait counter to `250`.
3. **Clean Native Despawn (Frame 1):** On the very next simulation frame, the game's native engine sees the wait counter is complete and cleanly releases the locomotive, all trailing wagons, and cargo units via `VehicleManager.ReleaseVehicle`.

---

## ✨ Key Features

- **🚄 Instant Turnaround:** Trains, cargo ships, and planes disappear within 1 frame after completing border delivery.
- **💰 100% Export Revenue Guaranteed:** Because vanilla delivery logic runs first, every ton of oil, ore, agricultural, and manufactured goods is fully credited.
- **🛡️ Zero Ghost Wagons:** Never leaves orphaned wagons or broken vehicle chains blocking the tracks.
- **⚡ Zero Performance Overhead:** No per-frame grid scans, no raycasting, and no custom physics. Pure, minimal postfix hooks.
- **🔄 Universal Compatibility:** Works seamlessly alongside:
  - **Harmony 2.2+ (CitiesHarmony)**
  - **TM:PE (Traffic Manager: President Edition)**
  - **Optimised Outside Connections**
  - **Transfer Manager CE**
  - **More Nodes/Segments (More Network Capacity)**
  - **81 Tiles 2**
  - **Advanced Vehicle Options (AVO)**

---

## 🛠️ Supported Vehicle Types

- **Cargo Trains** (`CargoTrainAI`)
- **Passenger Trains** (`PassengerTrainAI`)
- **Cargo Ships** (`CargoShipAI`)
- **Passenger Ships** (`PassengerShipAI`)
- **Cargo Airplanes** (`CargoPlaneAI`)
- **Passenger Airplanes** (`PassengerPlaneAI`)

*(Road vehicles and trucks already complete deliveries immediately without delay in vanilla, so they remain untouched to preserve maximum simulation accuracy.)*

---

## 📦 Requirements

- **Cities: Skylines** (v1.17.0-f3 or newer)
- **[CitiesHarmony](https://steamcommunity.com/workshop/filedetails/?id=2040656402)** (Harmony 2.2.2-0)

---

## 🔨 Building from Source

To compile the mod on macOS or Linux with Mono (`mcs`):

```bash
git clone https://github.com/NqocDuy1426/InstantBorderDespawn.git
cd InstantBorderDespawn
chmod +x build.sh
./build.sh
```

The compiled `InstantBorderDespawn.dll` will be automatically installed into your local Cities: Skylines Addons folder.

---

## 📄 License

MIT License. Copyright (c) 2026 NgocDuy.
