# Subsystem: GAMEPLAY (4 -> 8 local players)

Files: `src\Patcher\Patches_Gameplay.cs` (Cecil, in memory), `src\Plugin\Runtime_Gameplay.cs` (Harmony). Online stays max 4; wire formats are NOT changed.

## Key facts established (verified in decomp + scene/bundle data via UnityPy, read-only)
- Every kitchen scene (145 scene bundles scanned) contains exactly **4 pre-placed chef GameObjects** (PlayerIDProvider ids One..Four, layer 8 "Players", tag "Player"). The per-player-count `SceneVarients` of a level all point to the **same scene**; they only differ in `LevelConfig` and star boundaries. Coop directory: 45 levels have variants {1,2,3,4}, 7 have {-1}. Competitive: {2,3,4} and always loaded with playerCount=4.
- Chefs are assigned in `CampaignKitchenLoaderManager.AssignChefEntities` (providers sorted by id, `while (i < users.Count && j < providers.Length)`); surplus chefs are destroyed in `ClientKitchenLoader.AssignChefOwnershipFromNetwork`. With 8 users and 4 chefs users 5..8 would get EntityID 0 = no chef.
- Entities are registered by `MultiplayerController.AsyncScanEntities` (scene scan, later than Awake) so chefs cloned during scene Awake are registered/synchronised automatically. Entity id is 10 bits (pool 1022); 4 extra chefs cost a few ids, not a limit in practice.
- Script execution order (MonoScript.m_ExecutionOrder in the scene bundles): KitchenBootstrapManager -6, PlayerInputLookup -5, **CampaignKitchenLoaderManager -4**, PlayerControls/PlayerIDProvider -3, ChefMeshReplacer -2, PlayerSwitchingManager/Plate/... -1, MultiplayerCamera 0. -> in CampaignKitchenLoaderManager.Awake no chef script has run yet, so a clone is a pristine chef (this avoids duplicated runtime-added components such as `PlayerControlsImpl_Default`, added in PlayerControls.Awake).
- Chef positions in `s_sushi_1_1`: 6.4 x 4.1 m rectangle, y=-0.63 (floor). MultiplayerCamera there: min 16 / max 17.5, edge buffers 0.4.
- Avatars/colours: `MainAvatarDirectory` 31 avatars / 5 colours, `DLC1AvatarDirectory` 6 / 4; `MetaGameProgress.CombineAvatarDirectories` merges (colours de-duplicated). Fewer than 8 distinct colours, so colours repeat; the lobby card already does `colourSelection = player % options` (PlayerSelectCardUIController:186) and nothing enforces uniqueness in coop, so duplicates are allowed. `User.Colour == 7` is a "unset" sentinel (do not use 7 as a real colour index).

## Per-site change list
### Patcher (Cecil)
| Site | Change | Why |
|---|---|---|
| `SceneDirectoryData/SceneDirectoryEntry::GetSceneVarient(int)` | prepend `if (_playerCount > 4) _playerCount = 4;` | No variant for 5..8 and no `-1` fallback -> returned null -> every caller (ServerWorldMapFlowController, LevelLoadByIndex client side NetworkUtils, ServerMessenger.LoadLevel, SaveSlotElement, FlagHandler, LobbyUIController.IsValid.., WorldMapKitchenLevelIconUI, ServerLobbyFlowController) refuses/NREs. One central fix, uses the 4-player scene/LevelConfig/star boundaries. |
| `PlayerSwitchingManager::InitialiseAvatars` | loop bound 4 -> 8 (1 site, tolerant if already 8) | AvatarSet + PlayerSwitch button per player id; chef 5..8 otherwise absent from `m_avatarSets`. |
| `ClientCannonCosmeticDecisions::Load` | `m_colours[i]` -> `m_colours[i % m_colours.Length]` (1 site) | `i` = user index, array = distinct colours (<8) -> IndexOutOfRange for user 5+ in cannon levels. |
### Plugin (Harmony)
| Patch | What / why |
|---|---|
| `CampaignKitchenLoaderManager.Awake` postfix -> `ExtraChefSpawner` | If `ClientUserSystem.m_Users.Count` > 4 and fewer providers than users: clone chef (k % 4) (same parent, rotation), `OverridePlayerId(Player.Five..Eight)`, rename "Player N". Coop only (competitive loader untouched: 2v2). Config `Gameplay.SpawnExtraChefs`. |
| `KitchenBootstrapManager.GetDefaultChef` postfix | index >= 4 returned null (ServerKitchenLoader default chefs) -> wrap `index % 4`. |
| `PlayerbasedActivation.IsActiveForPlayerCount` prefix | count > 4 -> 4. The Mask enum has only One..Four, so 5+ would deactivate objects authored for specific counts. |
| `MultiplayerCamera.Awake` prefix | only if > 4 providers: `m_maxDistance *= CameraMaxDistanceScale` (default 1.25, config `Gameplay.CameraMaxDistanceScale`). |
| `ServerPhysicsObjectSynchroniser.StoreContactInformation` prefix | trims `m_CollidingChefs` to `PhysicsObjectMessage.kTrackedChefCount` (4). Arrays are [4], ContactCount is 3 bits: 5+ chefs touching one crate threw IndexOutOfRange. Wire format unchanged. `ClientPhysicsObjectSynchroniser.m_ContactingChefs[4]` therefore stays 4 (never gets > 4). |
| `ServerTriggerMoveSpawnPoints.Move` prefix | When players > 4 or > spawn points: batches of min(spawnPoints,4) with a fresh `MoveSpawnMessage` each. Vanilla `CalculateBestSpawns` does `list[0]` on an emptied list (throws) and `MoveSpawnMessage` count field is 3 bits (max 7). Vanilla path untouched for <= 4. |

## Spawn-position strategy
Chef k (k = 0..3 -> players 5..8) clones template chef k at its own spawn and is then moved to the first valid candidate on rings of radius 1.1/1.8/2.5/3.2 m (12 angles, rotated by k): (1) downward raycast (4 m, ignoring Players layer & triggers) must hit ground within 0.35 m of the template chef's y; (2) >= 0.9 m (XZ) from every existing/placed chef; (3) `Physics.CheckCapsule` (radius 0.3, y+0.45..y+1.1) free of non-trigger colliders. If nothing fits it stacks at +0.4*(k+1) offset (physics separates). No navmesh is used (Overcooked uses its own grid). Respawns reuse the template's `m_startLocation` (clone copies serialized refs), i.e. extra chefs respawn at the original chef's spawn.

## Camera findings
`MultiplayerCamera` already iterates all `PlayerIDProvider.s_AllProviders` (centroid + per-avatar distance solve, clamped to [m_minDistance, m_maxDistance]); it rebuilds the list on `OnPlayerIDProviderDestroyed`. No code change needed to include 8 chefs; since our clones exist before its Awake they are tracked from frame 1. In kitchens the range is tiny (16..17.5), so extra headroom (x1.25 only when > 4 chefs) was added. Tune in-game; set the config to 1 to disable.

## Scoring / save-data safety
- `GameProgress` stores progress by **level index** (`LevelProgress` stars/high score); there is no player-count key in the save (grep `GameProgress.cs`, `GameSession.cs`: PlayerCount only appears via `LevelSettings.SceneDirectoryVarientEntry`). `HighScoreRepository` is keyed by `MachineID` (4 machines, unchanged).
- Chosen option: **an 8-player run maps onto the 4-player variant** (GetSceneVarient clamp): same LevelConfig, same star boundaries (`CoopStarRatingUIController.SetScoreData` reads `LevelSettings.SceneDirectoryVarientEntry`), result recorded in the normal level record. No new keys/array lengths -> no save-format change, nothing to corrupt; high scores earned with 8 chefs share the 4-player record (tell testers: thresholds are not rescaled).
- `CoopStarRatingUIController` `i <= 4` loops are star counts, not players: left alone.
- `LevelLoadByIndexMessage.Players` is 8 bits (8 fits). `GameUtils.GetWorkableChopsPerSlice`/`WorkableItem.GetChopTimeMultiplier` use `count >= 4`/`!= 1`: correct for 8.

## Deliberately left (reasons)
- `PhysicsObjectMessage` arrays/3-bit count, `ClientPhysicsObjectSynchroniser.m_ContactingChefs`, `MapAvatarControlsMessage.m_bHorns[4]` (server guards `m_playerIdx < Length`; horns of users 5+ are simply ignored), `MapAvatarHornMessage` (3 bits covers 0..7): wire formats kept.
- `PlayerControls.ThrowIndicators.UpdateCosmetics` guards `num < 4` (throw-aim textures for chefs 5+ keep the default texture).
- `GameUtils.GetEngagedAndInGame` / `FireDeedOnAllChefs` and `DialogueAnimationState.HasDifferentControllers` (loops of 4 over `EngagementSlot`): Core flagged them unresolved; they only affect deeds/achievements and a cosmetic dialogue-text variant for pads 5..8. Left to avoid depending on the PlayerManager array resize order; can be bumped 4->8 once Users/Input confirm `PlayerManager.GetUser` handles slots 4..7.
- `CompetitiveKitchenLoaderManager` (2v2, always 4 chefs), Analytics player count labels, `GriddlePan/RoastingTray` loops (4 burnt prefabs, not players), `LevelBounds` (4 corners), Task[4] arrays in privilege checks (Users).
- ChefMeshReplacer/ChefAvatarData/AvatarDirectoryData: no change. Avatar/colour message widths (7 bits avatar, `User.Colour` etc.) are Users-subsystem.

## Dependencies on other subsystems (not done here)
- UI: `MultiChefPadUIController:184` and `PadEngagementUIController:101` index `AvatarDirectory.Colours[(int)player]` directly -> need `% Colours.Length` for players 5..8 (< 8 distinct colours). `LobbyUIController.IsValid..` (coop) relies on GetSceneVarient clamp (done).
- Users/Input: `SelectedChefData` per user (7 bit avatar, colour index), `PlayerInputLookup.GetButton/GetValue` for Player.Five..Eight (GameInputConfig already has 11 entries), `ClientKitchenLoader` assigns local users to `Player (count of locals)`, `UserSystemUtils.BuildGameInputConfig`.
- `PauseMenuManager:109` and `ClientMapAvatarControls:186` build `ILogicalButton[4]` start/horn buttons (pause/horn only for the first 4 users) - UI/Input decision.

## Residual risks
1. Cloning at `CampaignKitchenLoaderManager.Awake`: relies on the execution-order analysis; if some chef child component awakens earlier than assumed, duplicated runtime children (throw indicators, hover icons) could appear on extra chefs.
2. Entity id budget (1022) on large levels (+ ~dozens ids for 4 chefs).
3. Extra chefs share template spawn/respawn points; moving-platform (`TriggerMoveSpawnPoints`) levels stack chefs on shared spawn points.
4. Tight kitchens: no free ring slot -> stack fallback (chefs overlap, physics shoves them).
5. Cannon (DLC) cosmetics colour reuse; star boundaries not rescaled for 8 players; surplus ring positions may sit outside a tight camera frame (raise `CameraMaxDistanceScale`).
6. Dual-chef/"switch" levels (Entity2ID, 1 player) unaffected (only count > 4 triggers cloning).

## In-game verification checklist
1. 4 players, coop level: no behaviour change, log has no "Spawned extra chef", camera identical.
2. 5, 6, 8 players, `s_sushi_1_1` (Level 1-1): log "Spawned extra chef Player 5.." with sane positions; all 8 chefs visible, move, dash, pick up/throw/chop/plate/serve; each controlled by the right pad; distinct/duplicated skins acceptable.
3. Camera frames all 8 at start and when spread out; tune `CameraMaxDistanceScale`.
4. Finish a level with 8: score screen shows 4-player star thresholds, stars/high score saved in the normal slot; reload save, 4-player load still fine.
5. World map -> enter level with 8 users (GetSceneVarient clamp), also from save slot start (SaveSlotElement).
6. Switch-chef (PlayerSwitchingManager) levels, moving-platform level (spawn move with 8), cannon level (colour index), crowd 5+ chefs against one crate (no exception in BepInEx log).
7. Pause/restart/quit from the level and return to map with 8 users; competitive mode still 4 chefs.
