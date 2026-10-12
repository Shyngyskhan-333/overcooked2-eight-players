# Development notes

These files are the notes written while the mod was being built: reconnaissance of the game, the plan, one report per subsystem and the test report. They record *why* each patch exists and which game code was examined and deliberately left alone.

They were written before and during in-game testing, so a few statements were overtaken by later results. The code and the top-level README are the source of truth. Known corrections:

| Where | Note says | What turned out to be true |
|---|---|---|
| `subsystem_input.md` | "NOT yet tested in game"; `GamepadEngagementManager::Update` is online only and was left alone | It is also the path that lets extra **local** pads join in the main-menu lobby. It is patched (`GamepadEngagementManager::Update`, both the `Count < 4` check and the cancel loop). |
| `subsystem_input.md` | Players 5 to 8 keep the default throw indicator | Fixed: ring colour is chosen with `player % 4` (`Patch_RingColourWrap`). |
| `subsystem_gameplay.md` | The extra-chef spawner reads `PlayerIDProvider.s_AllProviders` in `CampaignKitchenLoaderManager.Awake` | That list is still empty at that point (each provider fills it in its own `Awake`). The spawner now uses `FindObjectsOfType<PlayerIDProvider>()`. |
| `subsystem_ui.md` | Lobby slots in a 1×8 row | Replaced by a 2×4 grid inside the chalkboard panel (`LobbySlotScale`, `LobbySlotYOffset`). |
| All subsystem notes | Harmony patches applied in `Plugin.Awake` | BepInEx loads plugins before the game's managers exist, so patching is deferred until `GameUtils.GetDebugConfig()` is available and done class by class. |
| `MODDING_PLAN.md`, `RECON_REPORT.md` | Mention a sandbox copy and local tool folders | These were development-machine details and are not part of the repository. |

`TEST_REPORT.md` lists exactly what was verified, and how.

| Where | Note says | What turned out to be true (0.1.1 test pass) |
|---|---|---|
| `TEST_REPORT.md` first report | Pickup/chop/serve, score screen, quit/second session, other modes and controller reconnect "Untested" | Tested in the second pass, see the end of `TEST_REPORT.md`. Versus needed a fix; the respawn bubble crashed for players 5 to 8 until fixed. |
| `subsystem_gameplay.md` | The extra-chef spawner covers every kitchen loader | Only the co-op loader. Versus has its own loader; it now has a per-team spawner. |
