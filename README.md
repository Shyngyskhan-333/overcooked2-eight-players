# Overcooked! 2: Eight-Player Local Mod

A [BepInEx](https://github.com/BepInEx/BepInEx) mod that raises the local multiplayer limit of Overcooked! 2 from four players to eight. Same kitchens, same recipes, same shared camera. The lobby, pause menu, input and chef spawning are extended to eight players.

> **Status: prototype (v0.1.1).** Co-op (Story tutorial, Arcade) and Versus were played end to end with eight players in my tests: lobby, level, camera, pickup, chop, plate, serve, score, pause, restart, quit, second session, results screen. Those tests used *simulated* controllers on one game build. Physical controllers 5 to 8, cooking and washing, the Chef tab, DLC levels and online play are untested or not working. Read [Status](#status) and [Known issues](#known-issues) before you install.

## Contents

- [What it does](#what-it-does)
- [Status](#status)
- [Requirements](#requirements)
- [Installation walkthrough](#installation-walkthrough)
  - [Option A: installer script (recommended)](#option-a-installer-script-recommended)
  - [Option B: manual install](#option-b-manual-install)
  - [Check that it loaded](#check-that-it-loaded)
- [Playing with more than four](#playing-with-more-than-four)
- [Configuration](#configuration)
- [Uninstall and recovery](#uninstall-and-recovery)
- [Troubleshooting](#troubleshooting)
- [Known issues](#known-issues)
- [How it works](#how-it-works)
- [Build from source](#build-from-source)
- [Testing without eight controllers](#testing-without-eight-controllers)
- [Project layout](#project-layout)
- [Credits and AI disclosure](#credits-and-ai-disclosure)
- [License and disclaimer](#license-and-disclaimer)

## What it does

- Up to **8 local players**. Players 1 to 4 behave as in the base game.
- **Lobby:** the join panel shows 8 slots in a 2×4 grid. The prompt reads "Player 5 Press A / Space to Join Locally", and so on.
- **Levels:** chefs 5 to 8 are spawned next to the four chefs the kitchen already contains. All eight stay in the shared camera, which zooms out a little when more than four chefs are present.
- **Game modes:** Story, Arcade (couch) and Versus (couch). In Versus the extra chefs are spawned per team, so 4 against 4 gets four chefs on each side.
- **Pause menu:** all eight chefs are listed with their numbers and input devices.
- **Chef colours:** players 5 to 8 reuse the four team colours (blue, red, green, yellow). The number badge tells same-coloured chefs apart. The ring under each chef matches the apron colour.
- **Chef unlocks (optional):** every chef avatar can be shown as unlocked. This only changes what is displayed, never your save file. See [Configuration](#configuration).
- **Safe by default:** nothing on disk in the game's `Managed` folder is modified. The mod patches the game in memory at start-up, and the uninstaller returns the folder to its previous state.

## Status

Everything below was checked in the running game on **one** build, with the keyboard as player 1 and players 2 to 8 driven by *simulated* controllers (virtual [InControl](https://github.com/pbhogan/InControl) devices that go through the game's normal input path). Where I moved a chef next to a station to test an action, I used the test commands described in [Testing without eight controllers](#testing-without-eight-controllers); the action itself (pickup, chop, serve) was a normal button press on that player's pad.

| Area | Result |
|---|---|
| Mod loads, 5 IL patch steps apply, 28 runtime patch classes apply | Verified (`ok=5 failed=0`, `ok=28 failed=0`) |
| Lobby shows 8 slots, 8 players can join | Verified (simulated pads) |
| Player counts 1 to 8: level loads, user count and chef count match, no exceptions | Verified (simulated pads) |
| 8 chefs visible, camera frames all of them | Verified (screenshots, three different levels) |
| Moving one controller moves only its own chef (tested on players 5 and 8) | Verified (simulated pads) |
| Player 5: take lettuce from a crate, place it on a board, chop it, pick up `ChoppedLettuce` | Verified (log of the carried item after each step, screenshots) |
| Player 8: take a plate, plate the chopped lettuce, serve it, score goes from 0 to 8 | Verified (log, screenshot of the HUD) |
| Pause menu with 8 players, restart, extra chefs respawn | Verified (simulated pads) |
| Quit to the main menu, start a second session, extra chefs spawn again | Verified (spawner ran twice, 8 chefs both times) |
| Time's up and results screen with 8 players, co-op (stars, orders, total) | Verified (Arcade level "2-6") |
| Versus: team select with 8 players, 4 against 4, match, results screen | Verified. Needed a fix in 0.1.1: before it, only 4 chefs spawned. |
| Other levels | Verified: Story tutorial, Arcade "2-6" (mine), a Versus balloon level. DLC levels not tried. |
| Controller unplugged during play, then re-attached | Verified: the game shows "Active profile removed", the new pad takes the free seat and moves that chef again |
| Chef respawn after falling into a pit (players 5 to 8) | Verified after the 0.1.1 fix (see Changelog) |
| 4-player game still loads with no extra chefs | Verified |
| Chef ring colours for players 5 to 8 | Verified (screenshot) |
| "Unlock all chefs" option | Verified: 60 chefs available with it on, 38 with it off |
| Cooking and washing with players 5 to 8 | **Not tested.** I looked for a meat crate and a pan in two levels without finding a reliable setup. |
| The Chef tab (chef selection screen) with 8 players | **Not working properly**, see [Known issues](#known-issues) |
| Resolutions other than 1920×1080 | **Could not test.** The game ignored window-size arguments and registry values. Its UI is drawn on a fixed 1680×1050 canvas, so the layout should not change with resolution, but I did not see it. |
| Physical controllers 5 to 8 | **Not tested** (no hardware) |
| Online play | **Does not work** with unmodded players, see [Known issues](#known-issues). With eight local players the game itself blocks the online modes ("Too many local players to play this online mode"). |

The full test notes are in [`docs/TEST_REPORT.md`](docs/TEST_REPORT.md).

## Requirements

| Requirement | Details |
|---|---|
| OS | Windows 10 or 11, 64-bit |
| Game | Overcooked! 2, **GOG** build, Unity 2018.4.32, Mono, 64-bit. This is the only build tested (see below). |
| Loader | BepInEx **5.4.23.5 x64**. The installer downloads and verifies it for you. |
| Shell | Windows PowerShell 5.1 (already on Windows) or PowerShell 7 |
| Controllers | See [Playing with more than four](#playing-with-more-than-four) |

**Tested game files** (SHA-256):

```text
Overcooked2.exe                              26554C0460D509E99D25E11D3093DC0ADDB878E6A4A448F4DFE7ECA4C2C2D6E0
Overcooked2_Data\Managed\Assembly-CSharp.dll 79834C8F145A7F8B232FB2B985CA5578D7F8B133E9FFE2E73AB1D9899001044D
```

Compare yours with `Get-FileHash "<game folder>\Overcooked2_Data\Managed\Assembly-CSharp.dll"`.

**Steam, Epic and other builds are untested.** The game's code may differ there. If it does, the patcher does not guess: each step checks the exact number of code sites it expects and logs `step FAILED` if they do not match. In that case the game still starts, but without the eight-player changes. Please open an issue with your `BepInEx\LogOutput.log`.

## Installation walkthrough

### Option A: installer script (recommended)

1. **Close the game.**
2. **Download** `OC2-EightPlayers-0.1.1.zip` from the [Releases page](../../releases) and unzip it anywhere, for example `Downloads\OC2-EightPlayers`.
3. **Find your game folder**: the folder that contains `Overcooked2.exe`. For GOG Galaxy the default is usually `C:\Program Files (x86)\GOG Galaxy\Games\Overcooked! 2`. If you are not sure: open GOG Galaxy, pick the game, then *Manage installation → Show folder*.
4. **Open PowerShell** in the unzipped folder (in Explorer: click the address bar, type `powershell`, press Enter).
5. **Preview what the installer will do.** This changes nothing:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\install.ps1 -GameDir "C:\Program Files (x86)\GOG Galaxy\Games\Overcooked! 2" -DryRun
   ```

   Expected output:

   ```text
   Game folder : C:\Program Files (x86)\GOG Galaxy\Games\Overcooked! 2
   BepInEx     : not present (will be downloaded and installed)
   DryRun: nothing changed.
   ```

   If BepInEx is already installed, the second line says it will be reused and not modified.
6. **Install.** Run the same command without `-DryRun`:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\install.ps1 -GameDir "C:\Program Files (x86)\GOG Galaxy\Games\Overcooked! 2"
   ```

   The installer:
   - checks that the folder contains `Overcooked2.exe` and `Overcooked2_Data\Managed\Assembly-CSharp.dll`, and that the game is not running;
   - downloads BepInEx 5.4.23.5 x64 from the official GitHub release and verifies its SHA-256 (`82F98785…732C4`). If the hash does not match, it stops before copying anything;
   - copies `winhttp.dll`, `doorstop_config.ini` and the `BepInEx` folder into the game folder (skipped if BepInEx is already there);
   - copies the two mod files into `BepInEx\patchers` and `BepInEx\plugins`;
   - backs up every file it replaces, plus your save folder, into `<game folder>\_oc2_8p_backup\`, and writes `install-manifest.json` there so the uninstaller knows exactly what to remove.
7. **Start the game normally** (GOG Galaxy or `Overcooked2.exe`). The first start takes a few seconds longer.
8. **Check the log**, see [Check that it loaded](#check-that-it-loaded).

If the installer stops with an error, see [Troubleshooting](#troubleshooting). It writes the manifest last, so a run that failed before copying leaves the game folder as it was.

### Option B: manual install

Use this if you do not want to run scripts.

1. Close the game.
2. Download [BepInEx_win_x64_5.4.23.5.zip](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) (the **x64** one) and unzip it **into the game folder**, next to `Overcooked2.exe`. You should end up with `winhttp.dll`, `doorstop_config.ini` and a `BepInEx` folder beside the exe.
3. Create the folders `BepInEx\patchers` and `BepInEx\plugins` if they do not exist.
4. From this project's release zip, copy
   - `BepInEx\patchers\OC2.EightPlayers.Patcher.dll` to `<game folder>\BepInEx\patchers\`
   - `BepInEx\plugins\OC2.EightPlayers.dll` to `<game folder>\BepInEx\plugins\`
5. Start the game.

Back up your save folder first if you want a manual safety net: `%USERPROFILE%\AppData\LocalLow\Team17\Overcooked2\Saves`.

### Check that it loaded

Open `<game folder>\BepInEx\LogOutput.log` after the game reached the main menu. You should see these lines:

```text
[Info   :OC2-8P-Patcher] EngagementSlot.Count sentinel: 33 sites rewritten, 61 unresolved
[Info   :OC2-8P-Patcher] patch steps ok=5 failed=0
[Info   :Overcooked 2 Eight Players] game ready after 2,8s, applying Harmony patches
[Info   :Overcooked 2 Eight Players] Eight-player runtime plugin: patch classes ok=26 failed=0
```

The "61 unresolved" number is expected: those are places where a literal 4 is not a player count, and the patcher leaves them alone on purpose (the list is written to `BepInEx\8P-unresolved.txt`).

`ok=5 failed=0` and `failed=0` on the plugin line mean everything applied. This release has 26 patch classes; my last in-game run, before the unlock patch was added, logged `ok=25`. Anything with `FAILED` in it means a patch did not match your build.

## Playing with more than four

1. Start the game and stay on the main menu. The join panel on the chalkboard shows 8 slots.
2. Player 1 is the keyboard (or the first controller). Each further player presses **A** on their own controller. The prompt on the panel counts up: "Player 2 Press A / Space to Join Locally", then "Player 3", and so on.
3. Start a level as usual. Chefs 5 to 8 appear next to the original four.
4. The pause menu (Esc or Start) lists all players with their device names.

**Controllers beyond four.** Windows' XInput API exposes at most four Xbox-style controllers. The game reads input through InControl with its native plugin, which handles XInput pads plus DirectInput and HID devices. So the realistic setups are:

- up to four Xbox controllers, plus
- PlayStation, Switch Pro or generic HID controllers for the remaining seats, plus
- the keyboard.

**I could not test physical controllers.** Whether a particular mix of devices shows up as separate players on your machine is something you can check in the log: each controller produces a line like `[input] device attached: '<name>'`.

Players 5 to 8 play the 4-player version of each level (same layout and star thresholds).

## Configuration

The config file is created on first start: `BepInEx\config\local.oc2.eightplayers.cfg`. Edit it while the game is closed.

| Section | Key | Default | What it does |
|---|---|---|---|
| General | `Enabled` | `true` | Master switch for the runtime plugin. The in-memory patcher is separate: delete `BepInEx\patchers\OC2.EightPlayers.Patcher.dll` to disable it. |
| Gameplay | `SpawnExtraChefs` | `true` | Spawn chefs 5 to 8 when there are more than four players. |
| Gameplay | `CameraMaxDistanceScale` | `1.25` | Extra zoom-out allowed when more than four chefs are present. The 4-player camera is not changed. |
| Unlocks | `UnlockAllChefs` | `true` | Show every chef as unlocked, including chefs from DLC you may not own. Display only, the save file is not changed. Set to `false` to see only what your save unlocked. |
| UI | `LobbySlotScale` | `0.62` | Size of each lobby slot. |
| UI | `LobbySlotYOffset` | `-22` | Vertical offset of the lobby grid, in canvas units. Negative is down. |
| UI | `ChefX0`, `ChefDX`, `ChefScale` | `-1.9`, `0.65`, `0.7` | Placement and size of the chefs on the Chef customisation screen. |
| UI | `PadSplitScale` | `0.2` | Size of the controller panels on the pad-split screen. |
| UI | `DebugOverlay` | `true` | Enables a small debug overlay. F9 toggles it, F10 writes the UI state to the log, F11 saves a screenshot to `BepInEx\ui-shots\`. |
| Debug | `SimPads` | `false` | Test aid: scripted virtual controllers. Leave it off for normal play. See [Testing without eight controllers](#testing-without-eight-controllers). |

## Uninstall and recovery

**Fastest way back to a vanilla game, no scripts:** rename `winhttp.dll` in the game folder to `winhttp.dll.off`. BepInEx no longer loads and the game starts exactly as before. Rename it back to re-enable the mod.

**Full uninstall:** from the unzipped release folder run

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1 -GameDir "C:\Program Files (x86)\GOG Galaxy\Games\Overcooked! 2"
```

It reads `_oc2_8p_backup\install-manifest.json` and removes the mod DLLs, the mod config, the files the mod generated (`BepInEx\8P-*.txt`, `BepInEx\ui-shots`), and the files the installer created, including BepInEx if the installer put it there. Files it replaced are restored from the backup. Files you added yourself are left alone, and a `BepInEx` folder that still contains them is kept.

Options: `-KeepBepInEx` removes only this mod, `-RestoreSaves` copies the save backup from install time back over your live saves.

The mod never changes your save format. Still, the game saves on its own while you play (including while testing), so if you want your saves exactly as they were at install time, use `-RestoreSaves` or copy `_oc2_8p_backup\Saves` back by hand.

## Troubleshooting

| Symptom | What to check |
|---|---|
| Game starts but behaves like vanilla, and `BepInEx\LogOutput.log` does not exist | `winhttp.dll` must be in the same folder as `Overcooked2.exe`, and it must not be renamed. Some antivirus programs quarantine it (it is BepInEx's loader). Add an exception and reinstall. |
| `step FAILED` or `patch steps ok=<5` in the log | The patcher found different game code than it expects. Your build is not supported yet. Open an issue and attach `BepInEx\LogOutput.log` and the SHA-256 of `Assembly-CSharp.dll`. |
| `game never became ready; runtime patches NOT applied` | Another mod or tool is blocking start-up. Remove other BepInEx plugins and retry. |
| The fifth player cannot join | Look for `[input] device attached` lines in the log. If the fifth controller has no line, Windows or the game does not expose it as a separate device (see the XInput note above). |
| Lobby slots overlap or sit in the wrong place | Adjust `LobbySlotScale` and `LobbySlotYOffset` in the config. Press F10 in the lobby to write the layout state to the log. |
| Installer says "Close Overcooked! 2 first" | Quit the game and check Task Manager for `Overcooked2.exe`. |
| Installer says the BepInEx hash does not match | The download was corrupted or replaced. Nothing was installed. Try again, or use [Option B](#option-b-manual-install). |
| PowerShell says running scripts is disabled | Use the `-ExecutionPolicy Bypass` form from this README. It applies to that one command only. |
| The game's own log | `%USERPROFILE%\AppData\LocalLow\Team17\Overcooked2\output_log.txt` shows exceptions from the game itself. |

## Known issues

- **Online play is not supported.** Several network message layouts were widened (slot, colour and user-count bit widths) because even local play goes through the game's client/server message layer. A modded client cannot play online with an unmodded one. Online player limit stays at four. To play online, disable the mod (rename `winhttp.dll`).
- **The Chef tab does not work for eight players.** On the main menu, Chef → Select Chef shows only about five chefs, with arrow pairs for four of them, so players 5 to 8 cannot pick their chef there. The positions of those 3D chefs are driven by the game's own animation, and my layout settings (`ChefX0`, `ChefDX`) have no effect on them (`ChefScale` does). Players 5 to 8 get a chef automatically instead, and the 8-player Versus and Arcade lobbies show every player's chef.
- **Chef colours repeat.** Players 5 to 8 share colours with 1 to 4. The shipped game has four player colours. The plugin also tries to add four more hue-shifted colours, but I never saw them take effect in game.
- **The van window shows four chef previews.** The lobby scene contains four preview models. Players 5 to 8 show in the slot grid but not as 3D previews.
- **Versus was only played 4 against 4.** The extra chefs are spawned to match each team's size, but uneven teams (for example 5 against 3) were not tried.
- **Extra chefs spawn beside the originals.** In tight kitchens they can overlap at the start and push each other apart. They respawn at the spot of the chef they were cloned from.
- **4-player level data is reused.** Star thresholds and layouts are those of the 4-player version.
- **Save data.** Eight-player runs write into the normal level record using the 4-player thresholds. No format change was made.
- **Unlock all chefs** also shows DLC chefs, even if you do not own that DLC. If you do not want that, set `UnlockAllChefs = false`.
- **Windows XInput limit.** See [Playing with more than four](#playing-with-more-than-four).
- **One message width is unconfirmed.** Two of the development notes disagree on whether the slot in `ControllerSettingsMessage` is 3 or 4 bits wide after patching. Local play worked in every test. It would matter only for online play.

## How it works

The four-player limit is partly compiled into the game, so a normal Harmony plugin is not enough:

- `EngagementSlot` is an enum that ends at `Count = 4`. `Count` is inlined as the literal `4` in dozens of methods.
- `UserSystemUtils.kMaxUsers` and `PlayerManagerShared.c_numEngagementSlots` are `const` values, and many loops and arrays use a literal `4`.
- Network messages encode slot, colour and user count in fixed bit widths that cannot represent eight players.

The mod therefore has two parts.

**1. Preloader patcher** (`OC2.EightPlayers.Patcher.dll`, in `BepInEx\patchers`). It runs before the game loads `Assembly-CSharp` and rewrites it **in memory** with [Mono.Cecil](https://github.com/jbevain/cecil). The file on disk is never changed. Steps:

| Step | What it changes |
|---|---|
| Core | Adds `Five` to `Eight` to `EngagementSlot` and sets `Count` to 8. A small type-tracking pass (`TypedConst`) finds only the constants that are typed as the slot sentinel and rewrites those 33 sites, so unrelated 4s such as `MachineID.Count` stay untouched. |
| Users | User cap, slot loops, array sizes, the invalid-colour sentinel (7 to 15) and the bit widths of the user, lobby and join messages. |
| Input | Engagement and pad loops, cancel-button arrays, the "any player pressed" input combinations, world-map horn buttons. |
| Gameplay | Player-count lookups for level data (5 to 8 players use the 4-player variant), chef avatar initialisation. |
| UI | Lobby "full" checks, pause menu and multi-chef panels, chef animation set index. |

Every helper checks the exact number of sites it expects and throws on a mismatch, so a game update fails loudly instead of half-patching. Each step is isolated, and one failing step does not stop the others.

**2. Runtime plugin** (`OC2.EightPlayers.dll`, in `BepInEx\plugins`). Harmony patches for things that need game objects: cloning the 4th lobby slot into slots 5 to 8 and laying them out as a 2×4 grid, growing the chef-select screens to 8, spawning chefs 5 to 8, extra camera headroom, hue-shifted colour entries, ring colours, and the unlock flag. BepInEx loads plugins very early, before the game's managers exist, so the plugin waits until `GameUtils.GetDebugConfig()` returns a value (about 2 seconds) and then applies its patches class by class. A failure in one class is logged and does not affect the others.

More detail, including every changed site and the sites that were examined and deliberately left alone, is in [`docs/`](docs/):

| File | Contents |
|---|---|
| [`docs/RECON_REPORT.md`](docs/RECON_REPORT.md) | Environment and the four-player assumptions found in the game |
| [`docs/MODDING_PLAN.md`](docs/MODDING_PLAN.md) | Why a patcher plus plugin, and the risks |
| [`docs/subsystem_users.md`](docs/subsystem_users.md) | Users, session and network message layer |
| [`docs/subsystem_input.md`](docs/subsystem_input.md) | Controller discovery, engagement and input routing |
| [`docs/subsystem_gameplay.md`](docs/subsystem_gameplay.md) | Level data, chef spawning, camera, saves |
| [`docs/subsystem_ui.md`](docs/subsystem_ui.md) | Lobby, chef screens, pause menu, HUD |
| [`docs/TEST_REPORT.md`](docs/TEST_REPORT.md) | What was tested and what was not |

The subsystem notes were written during development. A few statements in them were corrected by later in-game testing, see [`docs/README.md`](docs/README.md).

## Build from source

You need the .NET 8 SDK and your own game install. Nothing from the game is stored in this repository.

1. Install BepInEx 5.4.23.5 x64 into the game folder (run `scripts\install.ps1` once, or unzip BepInEx there yourself). The build reads `BepInEx.dll`, `0Harmony.dll` and `Mono.Cecil.dll` from `<game folder>\BepInEx\core`, and the game's own `Assembly-CSharp.dll` and Unity DLLs from `Overcooked2_Data\Managed`.
2. Build, then check the patcher offline:

   ```powershell
   git clone https://github.com/Shyngyskhan-333/overcooked2-eight-players
   cd overcooked2-eight-players
   powershell -ExecutionPolicy Bypass -File scripts\build.ps1 -GameDir "C:\Program Files (x86)\GOG Galaxy\Games\Overcooked! 2"
   ```

   The offline check applies every IL patch to a copy of your `Assembly-CSharp.dll` in memory, reloads the result and prints any `PATCH FAILURE`. Nothing is written to the game folder. Output: `dist\bin\OC2.EightPlayers.Patcher.dll` and `dist\bin\OC2.EightPlayers.dll`.
3. Make the release zip:

   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts\package.ps1 -GameDir "<game folder>" -Version 0.1.1
   ```

You can also call `dotnet build -c Release src\Plugin /p:GameDir="<game folder>"` yourself. Set the `OC2_GAMEDIR` environment variable to avoid passing `GameDir` each time.

## Testing without eight controllers

`Debug/SimPads = true` enables scripted virtual controllers. They are InControl devices that the game treats like real ones: they attach through `PCPadInputProvider`, engage into slots and drive chefs. Results from this are **simulated** and say nothing about physical hardware.

With the option on, the plugin watches `BepInEx\8p-sim.txt` (a quarter-second poll). Append one command per line; the plugin runs them and empties the file.

| Command | Effect |
|---|---|
| `attach N` | Attach N virtual pads, numbered after the ones already attached |
| `detach I` or `detach all` | Detach pad I (1-based) or all |
| `press I BTN [s]` | Hold button `BTN` on pad I for `s` seconds (default 0.15). Buttons: `A B X Y LB RB UP DOWN LEFT RIGHT START BACK` |
| `all press BTN [s]` | The same for every attached pad |
| `move I X Y [s]` | Push the left stick of pad I to (X, Y), each in -1..1, for `s` seconds (default 0.5) |
| `log TEXT` | Write a marker line to the log |
| `dump` | Log the user count and every chef (id, active state, position) |
| `avatars` | Log how many chef avatars are currently unlocked |
| `uistate` | Write the mod's UI state (lobby slots, chef menu, pause UI) to the log |
| `find TEXT [n]` | List up to n active scene objects whose name contains TEXT, with world positions |
| `state P` | Log position, facing and carried item of the chef of player P (0 is the keyboard player, 4 is player 5) |
| `tp P X Y Z` | Teleport that chef. This is a test shortcut, not something the game offers. |
| `tpface P X Y Z TX TZ` | Teleport the chef and turn it to look at the point (TX, TZ) |
| `tpnear P TEXT [d]` | Put the chef at distance d (default 1.0) from the first object whose name contains TEXT, facing it |

How the action tests worked: `tpface` placed a chef in front of a crate or board, then `press P A` (pick up or put down) and `press P X <seconds>` (chop) were normal button presses on that player's virtual pad, and `state` read what the chef was carrying afterwards. The tests that used `tp`, `tpface` or `tpnear` therefore prove that the buttons work for players 5 to 8, not that the player could walk there.

Example: in the main menu, join seven virtual pads:

```text
attach 7
press 1 A 0.3
press 2 A 0.3
press 3 A 0.3
press 4 A 0.3
press 5 A 0.3
press 6 A 0.3
press 7 A 0.3
```

Then start a level and run `dump` to see the chefs in the log. Pad 1 is player 2, pad 7 is player 8 (the keyboard is player 1).

## Project layout

```text
src/
  Directory.Build.props   build inputs (GameDir, BepInExCore, Managed)
  Patcher/                Mono.Cecil preloader patcher (net472)
    Patcher.cs              entry point, runs the steps and isolates failures
    Il.cs                   IL helpers that throw on a count mismatch
    TypedConst.cs           finds constants typed as EngagementSlot.Count
    Patches_Core / Users / Input / Gameplay / UI.cs
  Plugin/                 Harmony runtime plugin (net472)
    Plugin.cs               waits for the game, applies patch classes one by one
    Runtime_Users / Input / Gameplay / UI / UI_Debug / Unlocks.cs
    Runtime_Sim.cs          virtual controllers (off by default)
  Harness/                offline check: applies the patcher to Assembly-CSharp.dll
scripts/                  install, uninstall, build, package (PowerShell)
docs/                     reconnaissance, plan, per-subsystem notes, test report
```

## Credits and AI disclosure

- **How it was built.** The code, docs and tests were produced with [Claude Code](https://claude.com/claude-code) (model Claude Sonnet 5.5). Four parallel Claude subagents each took one subsystem (users and network, input, gameplay, UI), and I ran and checked the in-game tests myself in the session. I reviewed the results. Treat the code as AI-written and the test report as the honest limit of what was verified.
- [BepInEx](https://github.com/BepInEx/BepInEx) (LGPL-2.1): the mod loader. Downloaded by the installer, not redistributed here.
- [Harmony](https://github.com/pardeike/Harmony) and [MonoMod](https://github.com/MonoMod/MonoMod): runtime patching.
- [Mono.Cecil](https://github.com/jbevain/cecil): IL rewriting.
- [ILSpy](https://github.com/icsharpcode/ILSpy): reading the game's managed code during research.
- [UnityPy](https://github.com/K0lb3/UnityPy): inspecting scene and prefab layout during research.
- [oc2-modding](https://github.com/toasterparty/oc2-modding) by toasterparty: read for how BepInEx mods for this game are structured. This mod is independent and was not tested together with it.

## License and disclaimer

Code in this repository is under the [MIT license](LICENSE).

This is an unofficial fan mod. It is not affiliated with or endorsed by Team17 or Ghost Town Games. Overcooked! is their trademark. The repository and the release zip contain **no game files**: no assemblies, no assets, no BepInEx binaries. Use it only with a copy of the game you own. Back up your saves before installing, and use the mod at your own risk.
