# Time Rifters Archipelago

Play the Windows Steam version of **Time Rifters** as an Archipelago world. Weapons, Time Echo credits, episode keys, arena destruction checks, hidden escapes, goals, and optional DeathLink are randomized into your Archipelago room.

## Read this first

**Play Campaign Episodes only.** At Time Rifters' title screen, choose the normal campaign and its Episodes 1–3.

**Do not use Experiment Arenas for Archipelago.** They are not part of this integration: they do not send Archipelago checks and do not use Archipelago weapons, keys, progression, or tracking.

The release ZIP is designed to work without editing code, BepInEx files, or the included YAML.

## Quick setup (already familiar with Archipelago)

1. Copy the release's `game_files` **contents** into Time Rifters' Steam folder.
2. In ArchipelagoLauncher, use **Install APWorld** and select `timerifters.apworld`; restart the launcher.
3. Copy `Time-Rifters.yaml` to `C:\ProgramData\Archipelago\Players`.
4. Use **Generate** in ArchipelagoLauncher.
5. Upload the generated `AP_....zip` from `C:\ProgramData\Archipelago\output` to the [Archipelago Host Game page](https://archipelago.gg/uploads), then click **Create New Room**.
6. Open Time Rifters Client, connect to the `archipelago.gg:PORT` address shown on the room page as `Time`, then start a Campaign Episode and press **F8** or **Home** at the title screen.

Need help or want to share feedback? [Time Rifters Archipelago / #apworld-new on Discord](https://discord.com/channels/731205301247803413/1552755726118686841).

## What the words mean

| Thing | What it is | What you do with it |
| --- | --- | --- |
| `timerifters.apworld` | The file that teaches Archipelago about Time Rifters. | Install it once in ArchipelagoLauncher. |
| `Time-Rifters.yaml` | Your ready-made game settings. | Copy it unchanged into Archipelago's `Players` folder. |
| `AP_....zip` | Your newly generated Archipelago game file. | Upload it to Archipelago WebHost to create your room. |
| Time Rifters Client | The small Archipelago program that connects your game to the room. | Leave it open while you play. |

## What you need

* A legal Windows Steam copy of **Time Rifters**.
* [Archipelago for Windows](https://github.com/ArchipelagoMW/Archipelago/releases/latest).
* The newest [Time Rifters Archipelago release](https://github.com/johnny1754/TimeRifters-Archipelago/releases/latest).

The release already contains the correct **BepInEx 5.4.23.2 Windows x86** files and the Time Rifters compatibility configuration. Do not download a separate BepInEx version. Time Rifters is 32-bit, so BepInEx x64 and BepInEx 6 are not compatible.

## First game: follow these steps exactly

This first-game route uses the included tested settings. **Do not edit the YAML file** for a one-player game.

### 1. Install Archipelago

1. Download and install the current Windows version of Archipelago from the link above.
2. Open `ArchipelagoLauncher` once, then close it.

Archipelago normally installs in `C:\ProgramData\Archipelago`. `ProgramData` is a hidden Windows folder; the steps below open it for you.

### 2. Put the mod files in your Steam game folder

1. Open **Steam**.
2. Go to **Library**.
3. Right-click **Time Rifters**.
4. Choose **Manage → Browse local files**. A File Explorer window opens: this is your Time Rifters game folder.
5. Extract the Time Rifters Archipelago release ZIP somewhere easy to find, such as Downloads.
6. Open the extracted release folder, then open **`game_files`**.
7. Copy **everything inside `game_files`** into the Steam folder from step 4.
8. If Windows asks, choose **Replace the files in the destination** and allow folders to merge.
9. Start Time Rifters normally from Steam once, then close it.

Do **not** launch `TimeRifters_DirectToRift.exe`. Start the game through Steam as usual.

### 3. Install the Time Rifters APWorld

1. Open **ArchipelagoLauncher**.
2. Click **Install APWorld**.
3. Select `timerifters.apworld` from the extracted release folder.
4. Close and reopen ArchipelagoLauncher.
5. Confirm that **Time Rifters Client** appears in the launcher.

### 4. Set up your YAML without editing it

1. Press **Windows + R**.
2. Paste this, then press Enter:

   ```text
   %ProgramData%\Archipelago
   ```

3. Open the **`Players`** folder. If it does not exist, create a folder named exactly `Players`.
4. Copy the release's **`Time-Rifters.yaml`** into that `Players` folder.
5. Leave it alone. Do not open it, rename it, or change its settings for your first game.

The included YAML already uses the recommended standard settings: 95% average goal, 50 Time Echoes, episode keys on, hidden escape checks off, five percentage checks per arena, arena shuffle off, and DeathLink off. Its player name is **Time**; use `Time` when the client asks for your slot name.

> ArchipelagoLauncher does the generation; the YAML is simply the settings card it reads. The Launcher can create template YAMLs, but it does not provide a separate settings screen for a custom APWorld. Using the included file unchanged is the intended beginner setup.

### 5. Generate your game

1. Open **ArchipelagoLauncher**.
2. Click **Generate**.
3. Wait for generation to finish. Do not select the `.apworld` or YAML manually at this step; the launcher reads the YAML from the `Players` folder.
4. Your generated room file is in:

   ```text
   C:\ProgramData\Archipelago\output
   ```

   It will have a name similar to `AP_123456789.zip`.

### 6. Host the room on the Archipelago website

1. Open the [Archipelago Host Game page](https://archipelago.gg/uploads).
2. Click **Upload File** and select the `AP_....zip` file you made in `C:\ProgramData\Archipelago\output`.
3. Wait for the Seed Info page, then click **Create New Room**.
4. Keep the new room page open. It shows the connection address in this form:

   ```text
   archipelago.gg:PORT
   ```

5. Copy that whole address. This is the address you will use in Time Rifters Client.

WebHost is the recommended route for this mod: it avoids local-server and port-forwarding problems, keeps the room available for the group, and provides the tracker link.

### 7. Connect and play

1. In ArchipelagoLauncher, open **Time Rifters Client**.
2. Connect it to the `archipelago.gg:PORT` address from your room page and enter the slot name **Time**.
3. Leave Time Rifters Client open.
4. Start Time Rifters through Steam.
5. At the title screen, press **F8** or **Home** once. The top-left panel should say **CONNECTED**.
6. Start a **Campaign Episode**. Do not use Experiment Arenas.

While playing:

* **F7** hides or shows the top-left Archipelago panel.
* **F8** or **Home** enables/disables Archipelago mode at the title screen only.
* Episode 1 starts available. Episode 2 and Episode 3 may be locked until you receive their Archipelago keys. This is normal.
* The panel's **Status** line shows the latest received item, sent check, locked weapon, locked episode, or other update.

## If you are playing with other people

Every game slot needs its own YAML. For a multiworld, copy `Time-Rifters.yaml` once for each Time Rifters player, then give every copy a different `name:` on its first line (for example, `name: Johnny`). This is the one time you should edit the included YAML. Gather every player's YAML in the host's `C:\ProgramData\Archipelago\Players` folder before clicking **Generate**. Each player then connects their own game client with the player name from their own YAML.

After generation, the host uploads the single `AP_....zip` file to the [Archipelago Host Game page](https://archipelago.gg/uploads), clicks **Create New Room**, and sends the resulting room-page link to the group. Every player uses the same `archipelago.gg:PORT` address shown there, but enters their own slot name. Do not use `localhost` for a WebHost room.

## Default settings and optional changes

You do not need to change anything for the standard experience. The YAML options below are only for players who intentionally want to customize a later game. After changing options, generate a **new** seed.

```yaml
Time Rifters:
  escape_checks: false
  episode_keys: true
  arena_shuffle: false
  arena_percentage_checks: 5
  required_time_echoes: 50
  goal: 95_percent
  death_link: false
  death_link_percent: 50
  death_link_duration: 60
```

| Option | Standard setting | What it changes |
| --- | --- | --- |
| `escape_checks` | `false` | Adds 15 arena escapes and one title-screen escape: 16 extra checks. Enable this when you want more checks. |
| `episode_keys` | `true` | Episode 2 and 3 need their randomized keys. |
| `arena_shuffle` | `false` | Moves 14 arenas between the three Campaign Episode groups. Arena Boss remains Episode 3 Arena 5. |
| `arena_percentage_checks` | `5` | Checks per arena. Five means 20%, 40%, 60%, 80%, and 100%. Higher values (up to 20) add checks and room for Time Echoes. |
| `required_time_echoes` | `50` | Time Echoes required for the goal. Range: 25–84. Higher values need enough checks. |
| `goal` | `95_percent` | `95_percent`, `99_percent`, and `100_percent` use the average best destruction across all 15 arenas. `final_boss_100_percent` requires Arena Boss at 100%. |
| `death_link` | `false` | Lets this game send and receive DeathLink. |
| `death_link_percent` | `50` | Finishing an arena below this percentage sends DeathLink when DeathLink is enabled. |
| `death_link_duration` | `60` | Seconds firing is disabled after receiving DeathLink. Range: 30–120. |

## Updating an existing install

For a normal UI-only update, close Time Rifters and replace `BepInEx\plugins\TimeRiftersArchipelago.dll` with the new DLL.

If the release says that its APWorld or protocol changed:

1. Install the new `timerifters.apworld` through **Install APWorld**.
2. Replace the DLL.
3. Use the included YAML defaults or new options file.
4. Generate a fresh seed.

Never use a new DLL with an old seed when the release notes say a new seed is required.

## Troubleshooting

### Time Rifters crashes or will not open after installation

Open the Steam Time Rifters folder again: **Steam → Library → right-click Time Rifters → Manage → Browse local files**.

The folder must contain `winhttp.dll`, `doorstop_config.ini`, and a `BepInEx` folder directly beside `TimeRifters.exe`. If you see an extra folder layer such as `Time Rifters\game_files\BepInEx`, copy the *contents* of `game_files` up one level instead.

### Time Rifters Client is not in ArchipelagoLauncher

Run **Install APWorld** again, choose `timerifters.apworld`, then close and reopen the launcher.

### The game says it is waiting for the AP client

Open Time Rifters Client first, connect as **Time**, wait a few seconds, return to Time Rifters' title screen, and press **F8** or **Home**.

### I clicked Episode 2 or 3 and it will not start

That episode is key-locked. Play available Campaign Episodes and checks in the other games in your room until Archipelago sends its Episode Key. The top-left **Status** line explains the lock.

### I am in an Experiment Arena and nothing is happening

Experiment Arenas are unsupported. Return to the title screen and start a normal **Campaign Episode** instead.

### I see 63 checks instead of 79

That seed was generated with escape checks off. Enable `escape_checks` before generating a new seed. Existing seeds never change their check count.

## Source, releases, and help

Each release ZIP includes the exact complete source in `source/`, the APWorld, the default YAML, a ready-to-copy BepInEx setup, and the DLL used for that release.

* [GitHub repository](https://github.com/johnny1754/TimeRifters-Archipelago)
* [Latest release](https://github.com/johnny1754/TimeRifters-Archipelago/releases/latest)
* [Archipelago setup and hosting guide](https://archipelago.gg/tutorial/Archipelago/setup_en)
* [Time Rifters Archipelago community / Archipelago Discord #apworld-new](https://discord.com/channels/731205301247803413/1552755726118686841)

When reporting a bug, include your Time Rifters version, Archipelago version, the selected YAML options, and `BepInEx\LogOutput.log`.

## Legal and third-party software

Time Rifters Archipelago is an unofficial fan-made integration and is not affiliated with or endorsed by Proton Studio Inc. **Time Rifters** and related names, trademarks, and game assets belong to their respective owners. This project does not distribute Time Rifters itself; a legal copy of the game is required.

The complete setup package includes the unmodified **BepInEx 5.4.23.2 Windows x86** runtime for convenience. Third-party copyright and licensing information is documented in `THIRD_PARTY_NOTICES.txt`, with the applicable BepInEx license text in `LICENSES/BepInEx-MIT.txt`.

## AI Disclosure

AI (ChatGPT) was used extensively during development of this project, including code generation, debugging, refactoring, and documentation. Development decisions, testing, and validation were performed by the project author.