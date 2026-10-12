# Subsystem report: Frontend + HUD UI

Files: `src/Patcher/Patches_UI.cs` (Cecil, independent steps, failures aggregated), `src/Plugin/Runtime_UI.cs` (Harmony, all try/catch + `[UI]` log prefix), `src/Plugin/Runtime_UI_Debug.cs` (overlay).
Offline verification done: build.sh clean; patched.dll IL inspected (all sites below show 8). Runtime code is UNTESTED in game (needs the lead's test slot).
Scene data was read with UnityPy from the original bundles (read-only): `frontend`, `lobbies`, `ingamemenu`, `bundle47` (prefabs). Helper scripts: `work\uiscripts\*.py`, IL viewer `work\iltool` (`ILASM=<dll> iltool.exe Type::Method`).

## UI hierarchy findings (how slots/previews are built)
- **Frontend lobby** (`frontend` scene): `WorldSpaceCanvas/.../IngamePlayers` (FrontendPlayerLobby, panel 368x333, scale 0.9, world-space canvas 1749x984 units). `PlayerList` (368x140, centre anchor) / `Holder` (HorizontalLayoutGroup, control+force-expand width) holds 4 `PlayerSlot` (FrontendPlayerSlot + SlotButton, Frame, ProfilePicture, Highlight 92x112, TypeImage, SlotDisabled, AddPlayer). Slot colour = `AvatarDirectory.Colours[user.Colour]` (PadUIColour). `m_PlayerSlots` serialised with 4 refs; `m_EngagementSlot` per slot = list index. SlotButton navigation = Automatic.
- `FrontendPlayerLobby.m_PlayerChefs` is **4 null refs in the scene** (RecalculateChefAvatars is a no-op). The 3D chefs `IngamePlayers/Players/FrontendChef..(3)` (ChefMeshReplacer, FrontendChef, FrontendChefCustomisation, PlayerIDProvider, child `ChefArrows` world-space canvas) are driven through `FrontendChefMenu.m_PlayerChefs` (FrontendChefCustomisation[4]) + `ChefCustomiser` (indexes by user index). Chef local x = -1.34/-0.41/0.48/1.62, y -0.94, z 0.82..1.07, scale 0.7 inside `Players` (scale 378.7, local pos -619,585,32).
- **Pre-level / pause player row** (`UIPlayerRootMenu`, scenes `lobbies` and `ingamemenu`): creates one `UIPlayer` prefab per user (any count, `IsMatchmaking` forces 4) in `Players` (HorizontalLayoutGroup+ContentSizeFitter); chefs are rendered by an ortho chef camera into a 1920x450 render texture, so scaling the row scales chefs too. `m_playerRotations` is bounds-checked.
- **Pad-split UI** (`FullScreenPadSplitUI` / `MultiChefConfigurator`, bundle47): `MultiChefUIController.m_padUI` = Controller1..4 (MultiChefPadUIController, `m_padNum`), 2x2 in LeftPanel/RightPanel (RightPanel has a Mask). `Awake` iterates m_padUI. Pad colours = `Colours[(int)player]`.
- **Pre-level cards** (`PlayerSelectScreen`, bundle47): Panel1..4 = PlayerSelectCardUIController (`m_actualPlayer`) laid out by `RectTransformExtension.m_fixedRatioAnchorMin/Max` (2x2, left x 0..0.34, right 0.73..1.07, middle panel = level info). `LobbyUIController` finds them with RequestComponentsRecursive and sorts by m_actualPlayer.
- **In-game customisation** (`ingamemenu` `CustomisationMenu/Arrows`): HorizontalLayoutGroup with 4 `ChefArrows` (FrontendChefCustomisation), array `InGameCustomisation.m_chefCustomisation`, mapped to `UIPlayers[i]`.
- `AvatarDirectoryData.Colours` = 5 entries (Blue, Red, Green, Yellow, No_Team) in every DLC directory; colour work is done by the Users subsystem (clones at 4..7). Sentinel is now 15 (do not touch).
- Not changed because they are not player-count dependent: `CoopStarRatingUIController.SetScoreData` (`i<=4`, `StarRating<4` are star indices), `LobbyUIController.Awake` empty `for(i<4)` loop, `InGamePauseMenu.OnEngagementChanged` (`ldc 4` is a string-array index), `UIPlayerRootMenu.ReCreateUIPlayers` matchmaking `4` (online), `FrontendInviteHandler` loop (online). ScoreScreen/competitive scoreboard, tutorial icons, emote wheel, ButtonHoverIcon, VoiceChatUserUI, PadEngagementUIController only use `EngagementSlot.One` or per-instance/user-index data: no 4-player arrays found (not verified visually; emote/dialogue anchors come from each UIPlayer's `EmoteWheelAnchor`/`DialogAnchor`, so they follow the row).

## Per-site change list
IL (Patches_UI.cs; `Patcher.Players`=8):
| type::method | change | why |
|---|---|---|
| FrontendPlayerLobby::OnUsersChangedEvent, FocusMenu, ShowCorrectPrompt, UpdatePromptPlayerCount | `Count != / == 4` -> 8 | "lobby full" tests (add-player prompt, focus on next free slot) |
| FrontendPlayerLobby::CanAddSplitPadGuest | `>= 4` -> 8 | split-pad guest cap |
| FrontendPlayerLobby::BuildInputConfig | loop 4 -> 8 (soft: Input subsystem also patches it) | engaged player inputs |
| UIPlayerMenuBehaviour::set_UserInfo | `EngagementSlot.Count` sentinel 4 -> 8 (TypedConst unresolved) | voice-chat slot "none"; 4 would now mean slot Five |
| PauseMenuManager::BuildStartButtons | `new ILogicalButton[4]` -> 8 | one Pause button per user index |
| MultiChefUIController::.ctor | `new MultiChefPadUIController[4]` -> 8 | initialiser (scene array overrides it; runtime grows it) |
| FrontendChef::SetAnimationSet | prepended `_animSet &= 3` | animator has 4 AnimationSets; UIPlayer passes the user index 0..7 |

Runtime (Runtime_UI.cs), all clones are made under an inactive holder so serialised fields are set before Awake runs:
| patch | what |
|---|---|
| FrontendPlayerLobby.Start prefix | clone PlayerSlot 4 -> slots 5..8 (`m_EngagementSlot`=4..7), add to `m_PlayerSlots`; widen `PlayerList` to 8x92 growing leftwards (right edge fixed) => 1x8 row |
| FrontendPlayerLobby.TriggerUserJoinEffect prefix | skip when idx >= m_PlayerChefs.Length (would throw) |
| FrontendChefMenu.Start/Show prefix | clone FrontendChef x4, `m_actualPlayer`/PlayerIDProvider = 4..7, re-spread all 8 (cfg ChefX0=-1.9, ChefDX=0.65, ChefScale=0.7x) |
| InGameCustomisation.SingleTimeInitialize prefix | clone ChefArrows x4 (layout group re-divides width) |
| UIPlayerRootMenu.Start postfix | `UiRowFitter` scales Players/Backgrounds by 4/count when >4 players |
| MultiChefUIController.Awake prefix | clone Controller4 x4 (`m_padNum`=4..7), 4x2 grid under the controller root (anchors x=(c+.5)/4, y=.68/.30, scale cfg PadSplitScale=0.2) |
| LobbyUIController.Awake prefix | clone Panel4 x4 (`m_actualPlayer`=4..7), re-set RectTransformExtension anchors: each side a 2x2 of half-size cards |
| LobbyUIController.IsReadyForStart prefix | competitive: even count >=2 with equal colour split; coop: variant for n players |
| SceneDirectoryEntry.GetSceneVarient postfix | n in 5..8 and no variant -> use the 4-player variant (Gameplay subsystem may handle the level side; this is idempotent) |

## Layout design (16:9, 1920x1080 and 1280x720)
Everything is anchored/proportional, so 1280x720 is the same layout scaled: lobby 1x8 row = 736 units (0.9 scale => 662) ending at the original right edge; chefs 8 across the same world span at 0.65 spacing; pad-split 4x2 grid (480x540 cells); cards 2x2 half-size per side; UIPlayer row shrinks to 4-player width (each player half width).

## Debug overlay
`F9` overlay (users, slots with screen rects, chefs, cards, pad UIs), `F10` dump to log as `[UI-STATE]`, `F11` screenshot + state txt to `BepInEx\ui-shots\`. Auto dump on users-changed and scene load. Disable via cfg `UI/DebugOverlay`. Uses UnityEngine.Input (legacy); if the game disables it the keys just do nothing (auto dumps still work).

## Risks
1. Chef world positions/scales are derived from numbers, not seen: tune `ChefX0/ChefDX/ChefScale` in BepInEx cfg after the first screenshot. Customisation chef visibility depends on `FrontendChefCustomisation` activation; a chef with no model stays invisible by design.
2. Lobby 1x8 row extends left; may overlap other panel art on the van scene (check). Slot art stays 92 wide.
3. `m_PlayerChefs` null in the scene means the lobby never shows idle chefs by itself; unverified whether another component fills it at runtime (log line `lobby m_PlayerChefs:` reports it).
4. Cloning with an inactive holder assumes no script depends on sibling index/names; `AssignChildRecursive` fields are serialised so they remap on Instantiate.
5. Card/pad layouts rely on private fields (`m_fixedRatioAnchorMin/Max`, `m_padNum`, `m_actualPlayer`, `m_padUI`, `m_chefCustomisation`) via reflection; failures are caught and logged, leaving 4 elements.
6. Animation sets for chefs 5-8 repeat sets 1-4 (visual only). Colour tint comes from Users' cloned colours.
7. Pause button / input for players 5-8 depends on Input subsystem giving valid `PlayerInputLookup.Player` 4..7 buttons.

## In-game verification checklist (screenshot each at 1920x1080 and 1280x720, F10 dump with each)
1. Main menu with 1 pad: lobby slots row (should show 1 filled + 1 "+" + 6 disabled); `[UI] lobby slots grown 4 -> 8` in log.
2. Add players one by one to 4, 5, 6, 7, 8: slot colours, names, controller icons, no overlap, navigation left/right across all 8, options panel opens below, prompt text count ("Add player N").
3. Remove a middle player, check slot compaction; at 8 the "+" disappears and focus goes to the border selectable.
4. Chef customisation (Chef tab): 8 chefs visible/non-overlapping, arrows work for chef 5-8, chosen avatars persist; animation sets.
5. Pad-split UI (split pad option): 8 controller panels in grid, numbers/colours 1..8.
6. World map -> level -> player select screen with 5-8 players: card grid, Ready button at 6/8 competitive, coop ready at 5-8.
7. Lobbies scene (UIPlayers row): 5, 8 users: row width, chefs, names, emote/dialog anchors.
8. In-game pause menu with 8 players: UIPlayers row, Customisation submenu arrows (8), pause by player 5-8.
9. Score screen (coop star board / versus scoreboard) after a match with 8: no exception, results visible.
10. Grep BepInEx log for `[UI]` errors and `UI FAIL` (patcher).
