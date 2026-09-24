# Time Rifters Archipelago

A work-in-progress Archipelago integration for the Steam game **Time Rifters**.

## Current prototype

* 15 playable arenas
* 60 Archipelago checks total
* Each arena sends checks at **25%, 50%, 75%, and 100%** destruction
* Checks send immediately when the milestone is reached
* Pistol is available from the start
* Flak, Lightning, Alien Disk, Rocket Launcher, and Rifle are randomized Archipelago items
* Reconnect-safe local progress tracking
* Enable the mod at the title screen with **Home** or **F8**

## Requirements

* A legal copy of Time Rifters for Windows
* BepInEx 5.4.23.2 **Windows x86** installed beside `TimeRifters.exe`
* Archipelago Launcher
* The Time Rifters `.apworld` and game plugin from a compatible release

This project does not include Time Rifters game files.

## Installation

1. Install the compatible `timerifters_prototype.apworld` by double-clicking it.

2. Copy `TimeRiftersAPPrototype.dll` into:

   `TimeRifters\BepInEx\plugins\`

3. Start Archipelago Launcher and generate or join a room containing Time Rifters.

4. Start the Time Rifters Prototype Client and connect to the room.

5. Launch Time Rifters.

6. At the title screen, press **Home** or **F8** to enable the Archipelago mod.

A fresh seed is required when the world’s location count changes.

## Source files

* `Plugin.cs` — Time Rifters BepInEx plugin and in-game integration
* `Logic.cs` — milestone, weapon, and progress logic
* `build.sh` — build helper script

## Status

This is an active development prototype. The 60-check version has been tested with cross-game item sending and reconnecting, but it is not yet a finished, official Archipelago world.
