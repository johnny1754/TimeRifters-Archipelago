# Time Rifters Archipelago

A work-in-progress Archipelago integration for the Steam game **Time Rifters**.

## Download

Get the newest test build from the [Releases page](../../releases).

The release includes:

- `TimeRiftersArchipelago.dll` — the BepInEx mod
- `timerifters.apworld` — installs Time Rifters in Archipelago Launcher
- `Time-Rifters.yaml` — example player file
- A ZIP containing everything above

## Current features

- 15 campaign arenas
- Arena progress checks at 25%, 50%, 75%, and 100%
- Episode completion checks
- Optional hidden platforming escape checks, including the Title Screen escape
- Randomized weapon unlocks: Flak Cannon, Plasma Beam, Particle Ball, Rocket Launcher, and Spread Rifle
- Reusable Time Echo upgrade credits
- In-game overlay showing weapon status and check totals
- Escape checks can be enabled or disabled in the YAML

## Installation

### 1. Install BepInEx

1. Download **BepInEx 5.4.23.2 — Windows x86** from the [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases).
2. Open your Time Rifters Steam folder:
   - Steam Library → right-click **Time Rifters** → **Manage** → **Browse local files**
3. Extract the BepInEx ZIP directly into that folder—the same folder containing `TimeRifters.exe`.
4. Start Time Rifters once, then close it. This creates the `BepInEx` folder.

### 2. Install Time Rifters Archipelago

1. Copy `TimeRiftersArchipelago.dll` into:

   `TimeRifters\BepInEx\plugins\`

2. Double-click `timerifters.apworld`, then restart Archipelago Launcher.
3. Open `Time-Rifters.yaml` and change `name: Time` to your player name.
4. Generate a room, start **Time Rifters Client**, and connect.
5. Start Time Rifters and press **Home** or **F8** at the title screen.

## YAML option

```yaml
Time Rifters:
  escape_checks: true
