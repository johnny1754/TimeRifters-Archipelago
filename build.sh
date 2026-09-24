#!/usr/bin/env bash
set -euo pipefail
# Usage: build.sh DOTNET_EXECUTABLE CSC_DLL GAME_MANAGED_DIR BEPINEX_CORE_DIR OUTPUT_DLL
"$1" "$2" -nologo -target:library -optimize+ -langversion:3 -nostdlib+ -noconfig \
  -out:"$5" -r:"$3/mscorlib.dll" -r:"$3/System.dll" -r:"$3/System.Core.dll" \
  -r:"$3/UnityEngine.dll" -r:"$4/BepInEx.dll" -r:"$4/0Harmony.dll" \
  "$(dirname "$0")/Plugin.cs" "$(dirname "$0")/Logic.cs"
