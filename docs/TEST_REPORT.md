# TEST_REPORT.md — Overcooked! 2 eight-player mod v0.1.0 (prototype)

Environment: GOG build, Unity 2018.4.32 Mono x64, Windows 11, 1920x1080 (game renders 1680x1050 canvas). Tested in a separate copy of the game folder (the original install was untouched during these tests). Machine has **no gamepads attached**: keyboard = player 1; players 2–8 were **SIMULATED** InControl virtual devices (`Runtime_Sim.cs`) that go through the game's own `PCPadInputProvider -> engagement -> player` path. This is NOT proof of compatibility with physical controllers.

Legend: **V** verified in running game (log and/or screenshot) · **S** verified with simulated input only · **C** code/offline check only · **U** untested.

## Results
| Area | Status | Evidence |
|---|---|---|
| Patcher applies on GOG build (5 steps, 0 failed) | V | `patch steps ok=5 failed=0` in every run |
| Runtime plugin patches (24 classes, 0 failed) | V | `patch classes ok=24 failed=0` |
| Offline: patched Assembly-CSharp reloads (17622 bodies) | C | `build.sh` harness |
| Main menu / title with mod | V | screenshots (not included) |
| Lobby: 8 slots, 2x4 grid inside panel, device icons, "Player 5 press A" prompt | V/S | screenshots (not included) |
| 8 users join (keyboard + 7 virtual pads) -> slots One..Eight | S | `[input] EngagePad` x8 |
| Player-count matrix 1–8: level loads, users=N, chef providers=N, 0 game-log exceptions | S | test log (summary below) |
| 8 chefs visible in kitchen, camera frames all | S | screenshot (not included) |
| Independent control: pad 4 -> only chef 5 moved; pad 7 -> only chef 8 moved; other 6 stayed | S | before/after screenshots (not included) |
| Pause menu with 8 numbered chefs + device labels, no overlap | S | screenshot (not included) |
| Restart: confirm dialog, level reloads, extra chefs respawn | S | t23–t25, spawner logged twice |
| 4-player regression: N=4 loads, 4 chefs, no extra spawn, 0 exceptions | S | matrix N=4 |
| Pickup/carry/throw/chop/cook/wash/plate/serve with players 5–8 | **U** | not exercised |
| Score / results screen / stars with 8 | **U** | not reached |
| Quit to menu and start a second session | **U** | not tested (restart only) |
| Other levels, Versus/Arcade/competitive modes, DLC levels | **U** | only Story tutorial tested |
| Controller leave/reconnect, disconnect handling | **U** | not tested |
| 1280x720 / other resolutions | **U** | only 1920x1080 |
| Physical controllers 5–8 (non-XInput) | **U** | none available |
| Chef-select (Chef tab) screens with 8 | **U** | patched (C), not viewed |
| Original online play | **U**, expected broken vs unmodded peers | message layouts widened |

Matrix (from the test log): N=1..8 each: patcher ok, plugin ok, `ExtraChefSpawner: users=N sceneChefs=4`, extra chefs spawned = max(0,N-4), `DUMP users=N chefProviders=N`, `exceptions in game log: 0`. (`activeChefs=0` in dumps = taken during the intro cutscene, chefs not yet enabled; not a fault.)

## Bugs found by testing and fixed
1. Plugin threw in `Awake` (patching `PCPadInputProvider` ran its static ctor too early) -> patching deferred until game ready.
2. Horn-button patch step expected a non-existent site -> removed; harness now prints `PATCH FAILURE`.
3. Lobby 1x8 row overlapped van art and hid slot 1 -> 2x4 grid inside panel.
4. Extra-chef spawner silently did nothing (`s_AllProviders` empty at that time) -> `FindObjectsOfType`.

## Known defects / limits (not fixed)
- Chefs 5–8 show the same four team colours as 1–4 (rings repeat blue/red/green/yellow; number badges 5–8 distinguish them). Cloned "hue-shifted" colours do not visibly apply to rings.
- Lobby van window previews only 4 chefs (scene has 4 preview models).
- Players 5–8 use the 4-player level variant; extra chefs spawn beside the originals — crowded/stacked in tight kitchens; they respawn at the template chef's spot.
- XInput exposes max 4 Xbox pads on Windows; pads 5–8 must be non-XInput devices or keyboard. Not verifiable here.
- Network message layouts widened -> modded and unmodded clients cannot play online together.
- `ControllerSettingsMessage` slot width: Users agent widened to 4 bits, Input agent reported 3; not independently re-checked (local play unaffected in tests).
- Test side effects: game autosave rewrote 2 save files during testing; both were **restored from backup** (hashes match). Sandbox shares the real save folder.
- Installer round-trip tested only on a fake folder with BepInEx pre-present; the BepInEx-download branch was not executed.

## Not claimed
This is a working prototype for 5–8 *simulated* players through lobby -> level -> pause -> restart. It is **not** a complete eight-player mod: gameplay interactions, scoring/results, other modes, physical-controller independence and online compatibility remain unverified.

## How to finish manual verification (needs 5+ real non-XInput devices)
1. Install per BUILD_AND_INSTALL.md. 2. Check `BepInEx\LogOutput.log` lists one `[input] device attached` per controller. 3. Join all in main menu; start Story. 4. Each player: move, pick up, chop, cook, wash, plate, serve; finish level; check score screen. 5. Pause/resume/restart/quit; start another session. 6. Repeat in a coop level with many chefs and in Versus.

## Update after the first report (ring colours, unlock flag, install)
- **Chef ring colours (fixed, verified in game with simulated pads).** The ring under a chef is the throw-indicator texture set, a 4-entry array indexed by player number; chefs 5 to 8 had no entry and kept the default blue. `Patch_RingColourWrap` indexes it with `player % 4` (team modes untouched). In an 8-player kitchen the chefs now show four matching colour pairs (yellow, red, blue, green).
- **Unlock all chefs (not tested in game).** `Runtime_Unlocks.cs` forces the game's own `DebugManager` option "Unlock all chefs" on (config `Unlocks/UnlockAllChefs`, default true). A sandbox run meant to log the unlocked-chef count with the option off and on was stopped before it finished, so the count was never read. It also means the 26th runtime patch class has not run in the game; the last logged run had 25 classes, all applied.
- **Real-install check.** After installing into the real game folder, one launch logged `patch steps ok=5 failed=0` and `patch classes ok=24 failed=0` (the plugin of that moment, without the ring and unlock patches) and no exceptions in the game's own log. The final plugin was copied in afterwards and has not been launched there.
- **Installer.** A first install attempt failed on `Expand-Archive` under Windows PowerShell 5.1 before copying anything; the script now extracts with `System.IO.Compression.ZipFile`. Round-trips (install, simulate first-run files, uninstall) on a throwaway folder restored every file byte for byte, both with BepInEx already present and, for the final release zip, with the BepInEx download path (SHA-256 verified) and BepInEx removal. None of this was run against a real Steam install.
