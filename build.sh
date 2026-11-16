#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

GAME_DIR="$HOME/Library/Application Support/Steam/steamapps/common/Cities_Skylines/Cities.app/Contents/Resources/Data/Managed"
MOD_NAME="InstantBorderDespawn"
MOD_DIR="$HOME/Library/Application Support/Colossal Order/Cities_Skylines/Addons/Mods/$MOD_NAME"
HARMONY_DLL="$SCRIPT_DIR/Libs/CitiesHarmony.Harmony.dll"
CITIES_HARMONY_API_DLL="$SCRIPT_DIR/Libs/CitiesHarmony.API.dll"

echo "=========================================================="
echo "🚀 Building $MOD_NAME (Standalone Mod by NgocDuy)..."
echo "=========================================================="

echo "1️⃣ Compiling $MOD_NAME.dll with mcs..."
mcs -sdk:2 -target:library \
    -r:"$GAME_DIR/Assembly-CSharp.dll" \
    -r:"$GAME_DIR/ICities.dll" \
    -r:"$GAME_DIR/ColossalManaged.dll" \
    -r:"$GAME_DIR/UnityEngine.dll" \
    -r:"$HARMONY_DLL" \
    -r:"$CITIES_HARMONY_API_DLL" \
    -out:"$MOD_NAME.dll" \
    src/*.cs

echo "✅ Compiled $MOD_NAME.dll successfully."

echo "2️⃣ Deploying to Cities: Skylines Addons/Mods/$MOD_NAME and content/..."
mkdir -p "$MOD_DIR"
cp "$MOD_NAME.dll" "$MOD_DIR/"
cp "$CITIES_HARMONY_API_DLL" "$MOD_DIR/"

mkdir -p "$SCRIPT_DIR/content"
cp "$MOD_NAME.dll" "$SCRIPT_DIR/content/"
cp "$CITIES_HARMONY_API_DLL" "$SCRIPT_DIR/content/"

echo "=========================================================="
echo "🎉 DEPLOYMENT COMPLETE!"
echo "Files installed in $MOD_DIR:"
ls -lh "$MOD_DIR"
echo "=========================================================="

