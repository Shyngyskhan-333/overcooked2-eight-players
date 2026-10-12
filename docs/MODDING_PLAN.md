# MODDING_PLAN.md — Overcooked! 2 eight-player local mod

## Verified facts
- Install: `<GameDir>` — **GOG** build (gameId 1297999995, buildId 56415526248344738), not Steam.
- Unity 2018.4.32f1, Mono scripting, 64-bit. Managed code: `Overcooked2_Data\Managed\Assembly-CSharp.dll`.
- Input: **InControl** (`InControl.dll`) wrapped by the game's `PCPadInputProvider` / `PCPlayerManager`.
- No prior mod loader. **BepInEx 5.4.23.5 (x64, Doorstop `winhttp.dll`) was verified to load on this build** in a sandbox copy (a sandbox copy of the game folder), and a preloader patcher ran against `Assembly-CSharp` successfully.
- oc2-modding (toasterparty, v1.9.9) is a BepInEx-5 randomiser/Archipelago mod; it does not touch player counts, and its README does not state GOG support. We therefore ship a **separate BepInEx plugin + preloader patcher** (compatible in architecture: same loader) instead of extending oc2-modding. Not tested alongside oc2-modding.
- universal-modder is an agent-skill toolkit; used only as method guidance (scan → loader → patch → verify).

## Why plain Harmony is not enough
The four-player limit is partly **compile-time**: `enum EngagementSlot { One..Four, Count }` (`Count`=4 is inlined as `ldc.i4.4` in ~90 methods), `UserSystemUtils.kMaxUsers` and `PlayerManagerShared.c_numEngagementSlots` are `const`, and many loops/arrays use literal 4. Harmony cannot change inlined constants.

## Architecture (chosen)
1. **Preloader patcher** `OC2.EightPlayers.Patcher.dll` (`BepInEx\patchers\`): Mono.Cecil rewrites Assembly-CSharp **in memory** at startup. Nothing on disk in `Managed\` changes; uninstalling = deleting the BepInEx files.
   - `Patches_Core`: extend `EngagementSlot` (Five..Eight, Count=8) and, via `TypedConst`, rewrite only *type-flow-confirmed* uses of the sentinel.
   - `Patches_Users / Input / Gameplay / UI`: per-subsystem literal/loop/array/bit-width fixes. Every IL helper throws if the expected match count differs, so a game update fails loudly instead of half-patching.
2. **Runtime plugin** `OC2.EightPlayers.dll` (`BepInEx\plugins\`): Harmony patches and Unity runtime code (UI cloning/layout, spawn placement, camera framing, diagnostics) and the config switch (`Enabled`).
3. `MachineID.Count` stays 4 (online machines); online play remains max 4 players and is not modified.

## Process
- Develop and test only in the sandbox copy `gamecopy\`. The original install is touched only at the final install step, after backup + hashes.
- Offline verification: `build.sh` applies the patcher to a pristine assembly copy and reloads it (catches IL/metadata errors without launching the game). Then launch the sandbox, read `BepInEx\LogOutput.log`, screenshot.

## Known technical risks
1. **Physical controllers**: XInput exposes max 4 pads; whether other InControl providers expose pads 5–8 on this build must be verified (see docs\subsystem_input.md). No 8 physical pads are available here → eight-controller independence can only be simulated, not proven.
2. **UI**: slots are serialised in scenes/prefabs; 5–8 are created at runtime → layout must be verified by screenshots.
3. **Level data**: kitchens have spawn points/scene variants per player count (1–4); extra chefs need placement logic.
4. **Save data**: high scores/progress are keyed by player count; 8-player runs must not corrupt 4-player saves.
5. **Network messages**: slot/colour/player-id bit widths affect even local play because the local game runs through the server/client message layer.
6. **Game updates / other mods** that change `Assembly-CSharp` IL will make the patcher fail loudly at the affected step.
