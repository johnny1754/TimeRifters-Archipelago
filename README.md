# Time Rifters Archipelago

An in-development Archipelago integration for the Windows Steam version of **Time Rifters**.

It adds item randomization, arena destruction checks, episode keys, reusable Time Echo upgrade credits, optional arena shuffling, configurable goals, hidden-escape checks, and optional DeathLink.

> This is a community project. It does not include Time Rifters, BepInEx, or Archipelago itself.

## Download

Download the newest package from the [GitHub Releases page](https://github.com/johnny1754/TimeRifters-Archipelago/releases/latest).

Each release ZIP contains the game plugin, the `.apworld`, the default YAML, this guide, and a `source/` folder with the complete C# and Python source used for that release.

## Requirements

* A legal Windows copy of **Time Rifters**
* [Archipelago for Windows](https://github.com/ArchipelagoMW/Archipelago/releases/latest)
* [BepInEx 5.4.23.2](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2), using **`BepInEx_win_x86_5.4.23.2.zip`**
* The newest Time Rifters Archipelago release ZIP

Time Rifters is a 32-bit game. **Do not use BepInEx x64 or BepInEx 6** for this mod.

## Installation

### 1. Install Archipelago

1. Download and run the current Windows Archipelago installer.
2. Start `ArchipelagoLauncher` once after installation.

See Archipelago's official [setup guide](https://archipelago.gg/tutorial/Archipelago/setup_en) for its launcher, Generator, server, and clients.

### 2. Install BepInEx into Time Rifters

1. In Steam, open **Library → Time Rifters → Manage → Browse local files**.
2. Download **`BepInEx_win_x86_5.4.23.2.zip`** from the BepInEx link above.
3. Extract the **contents** of that ZIP directly beside `TimeRifters.exe`. Do not leave BepInEx inside an extra nested folder.
4. Start Time Rifters once, then close it. This creates `BepInEx\plugins`.
5. Copy `TimeRiftersArchipelago.dll` from this release into:

   `Time Rifters\BepInEx\plugins\`

### 3. Install the Archipelago world

1. Extract the release ZIP somewhere convenient.
2. Open `ArchipelagoLauncher`.
3. Choose **Install APWorld** and select `timerifters.apworld` from the extracted release.
4. Restart the launcher if **Time Rifters Client** does not appear in its client list.

### 4. Configure and generate

1. Copy `Time-Rifters.yaml` to your Archipelago `Players` folder.
2. Change `name: Time` to your desired player name if needed.
3. Keep the defaults or edit the `Time Rifters:` section below.
4. Generate a **fresh seed** with this APWorld and YAML.

The included default is a tested standard setup: 99% average goal, 50 required Time Echoes, hidden escapes and episode keys enabled, with arena shuffle and DeathLink disabled.

### 5. Play

1. Host or join the generated Archipelago room.
2. In `ArchipelagoLauncher`, open **Time Rifters Client** and connect to the room using your YAML player name.
3. Start Time Rifters.
4. At the title screen, press **F8** or **Home** to enable Archipelago mode.
5. Press **F7** to hide or show the status panel.

## How it plays

* Pistol is available from the start.
* Flak Cannon, Plasma Beam, Particle Ball, Rocket Launcher, and Spread Rifle are randomized progression items.
* Every arena has checks at **25%, 50%, 75%, and 100%** destruction, plus episode completion checks.
* Time Echoes are reusable shop-upgrade credits for every new episode or replay.
* Arena Boss always remains Episode 3 Arena 5 when arena shuffle is enabled.

## YAML options

```yaml
Time Rifters:
  escape_checks: true
  episode_keys: true
  arena_shuffle: false
  required_time_echoes: 50
  goal: 99_percent
  death_link: false
  death_link_percent: 50
```

| Option | Default | What it does |
| --- | --- | --- |
| `escape_checks` | `true` | Adds 15 arena escapes and the title-screen escape: 79 checks total instead of 63. |
| `episode_keys` | `true` | Requires the randomized Episode 2 and Episode 3 Keys before those episodes can start. |
| `arena_shuffle` | `false` | Shuffles the other 14 arenas between episode groups; Arena Boss stays Episode 3 Arena 5. |
| `required_time_echoes` | `50` | Progression Time Echo count. Slider range: 25–84. Higher values may need escape checks or future denser check settings. |
| `goal` | `99_percent` | `95_percent`, `99_percent`, and `100_percent` use the average best destruction across all 15 arenas. `final_boss_100_percent` requires Arena Boss at 100%. |
| `death_link` | `false` | Enables DeathLink participation. |
| `death_link_percent` | `50` | With DeathLink enabled, completing an arena below this value sends a DeathLink. Slider range: 1–100. A received link disables firing for one minute during an active arena. |

## Updating an existing install

When a release changes the bridge protocol, update **both** files, then make a new seed:

1. Install the new `timerifters.apworld` using the launcher.
2. Replace `TimeRiftersArchipelago.dll` in `BepInEx\plugins`.
3. Generate a fresh seed with the new YAML/APWorld.

Do not mix a DLL, APWorld, and seed from different protocol versions.

## Troubleshooting

### Time Rifters stops launching after BepInEx is installed

Confirm that you used **BepInEx 5.4.23.2 Windows x86** and extracted its contents directly beside `TimeRifters.exe`. x64 and BepInEx 6 are not compatible with this plugin.

### Time Rifters Client is missing from the launcher

Use **Install APWorld** in `ArchipelagoLauncher`, select `timerifters.apworld`, then restart the launcher.

### The game is waiting for the AP client

Start Time Rifters Client first, connect it with the exact YAML player name, wait a few seconds, then return to Time Rifters' title screen and press **F8** or **Home**.

### The client reports wrong slot data or will not connect

Your DLL, APWorld, and generated seed do not match. Reinstall the files from one release and generate a new seed.

### I see 63 checks instead of 79

Set `escape_checks: true` before generating the seed. Existing seeds keep the check count they were generated with.

## Source and development

The release ZIP includes its exact source in `source/`. The repository is at [github.com/johnny1754/TimeRifters-Archipelago](https://github.com/johnny1754/TimeRifters-Archipelago).

The source package intentionally excludes Time Rifters and BepInEx files. `build.sh` rebuilds the DLL when given a local Time Rifters `Managed` folder and BepInEx core DLLs.

## Status

This project is actively tested but not yet a 1.0 release. Report reproducible issues with your Time Rifters version, Archipelago version, selected YAML options, and `BepInEx\LogOutput.log`.
