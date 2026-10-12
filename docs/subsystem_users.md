# Subsystem report: Users + Session + Network message layer

Files: `src/Patcher/Patches_Users.cs` (Cecil, all sub-patches are attempted and failures aggregated into one thrown exception; failing ones print `USERS FAIL ...`), `src/Plugin/Runtime_Users.cs` (Harmony).
Offline verification: `build.sh` applies all steps to the pristine assembly, no `USERS FAIL`, reload OK. Counts are exact (ReplaceAll/ReplaceBeforeOp/ReplaceNth) so a game update fails loudly.
Policy: local up to 8, ONLINE stays 4. `OnlineMultiplayerConfig.MaxPlayers` and `MachineID.Count` are NOT changed.

## Changed (IL, Patches_Users.cs)
| Site | Change | Why |
|---|---|---|
| ServerUserSystem::AddUser | `Count >= 4` -> 8 | real user cap |
| ServerUserSystem::ResetUsersToOfflineState | slot loop `i<4` -> 8 (the two other ldc 4 are MachineID.Count defaults, left) | local users on slots 5-8 must be recreated when going offline |
| ServerUserSystem::ResetUsersToOnlineState | slot loop -> 8 | updates names of existing users on all slots (only slot One is added online) |
| ServerUserSystem::AddNewRemoteUser | `Count == 4` -> 8 | consistency; Server.cs still blocks remote joins at 4 |
| UserSystemUtils::AtMaxUserCount | `Count == 4` -> `Count > 3` | only caller is the "full lobby" check before going online (FrontendCoop/VersusTabOptions); with 5-8 local users `==4` would wrongly allow going online |
| GameUtils::GetEngagedAndInGame | loop 4 -> 8 | list of engaged pads (deeds) |
| ServerLobbyFlowController::SetState (x2), ClientLobbyFlowController::Awake (x1), ::SetState (x2) | `new ThemeChoice[MaxPlayers]` -> `[8]` (call get_MaxPlayers replaced by ldc 8 before newarr) | m_userChoices is indexed by user index |
| GameServerSessionData::.ctor | `new string[MaxPlayers]` -> 8 | indexed by EngagementSlot in SetPlayerSessionId (slot Count check already 8 via TypedConst) |
| User.kInvalidChefColour const 7 -> 15; User::.ctor (field init + default param), User::set_SelectedChefData, User::RefreshSelectedChefData, ClientUserSystem::AddUser default param, LobbyFlowController::.ctor (x3 team colour indices), ::GetColourIndex, CompetitiveKitchenLoaderManager::AssignChefEntities (x2), FrontendPlayerSlot::SetUIColourForUser | sentinel 7 -> 15 | 7 is a valid colour index for player 8. 15 fits the widened 4-bit wire field. **FrontendPlayerSlot (UI file) was touched by this subsystem; UI engineer must not also change that `7`.** |
| UserData::Serialise/Deserialise | slot 3->4 bits, colour 3->4 bits (2 each) | slot Count=8 needs 4 bits; colour sentinel 15 |
| UsersChangedMessage::Serialise/Deserialise | user count 3->4 bits | 3 bits max 7, 8 users need 4 |
| ChefAvatarMessage, ControllerSettingsMessage Serialise/Deserialise | slot (2nd 3) 3->4 bits | Count=8 |
| JoinDataProvider/GameUserData Serialize/Deserialize | slot (1st 3) 3->4 bits | Count=8 |
| LobbyServerMessage Serialise/Deserialise, LobbyClientMessage Serialise/Deserialise | chef index 2->3 bits (SelectionUpdate+FinalSelection; ThemeSelected+TeamChange) | chef index 0..7 |

## Runtime (Runtime_Users.cs, Harmony)
- `User.RefreshSelectedChefData` prefix: re-implementation, bounds-safe (`Colour % Colours.Length`, avatar index check), uses sentinel 15.
- `MetaGameProgress.CombineAvatarDirectories` postfix: inserts 4 cloned ChefColourData (hue-shifted orange/purple/cyan/pink) at colour indices 4..7, because coop colour of player i is `Colours[i]` and the shipped array holds only 4 real chef colours (rest are no-team/red/blue). Team colours are found by reference so index shift is safe. Needs in-game verification (colours of chefs 5-8 and UI pad colours).
- `LobbyFlowController.Awake` postfix: `m_themeSelections` (serialised scene array of 4 UI elements, indexed by user index) extended to 8 by cloning the last element.

## Examined and deliberately left
- OnlineMultiplayerConfig.MaxPlayers (4): all other uses are online (list capacities, GameLift/Galaxy session handlers' `< MaxPlayers`, FrontendInviteHandler, CreateLobby max, OnlineAWSHelper loop, OnlineMultiplayerSessionJoinUserDataHelper bit width, privilege tasks, FlexMatch). Online max stays 4.
- Server.cs::HandleJoinRequest (`4 == Count`, `Count + remote > 4`): remote join gating; with 5+ local users it correctly rejects.
- ServerLobbyFlowController line 262 `Count == 4`: only evaluated when remote users exist (online).
- HighScoreRepository::LevelProgress/OnUsersChanged loops: iterate MachineID (4), all local users are machine One. HighScoresMessage machine bits / UserAddedMessage index bits: `GetRequiredBitCount(4)=3` bits, index 0..7 fits.
- CheckPrivilegesAndDropInAllLocalUsersTask loop 1..3, ServerDropInUserMonitor: drop-in only runs when going online.
- DisconnectionHandler/Server HandleConnectionLost/HandleDisconnectMessage: the 2 ldc 4: MachineID.Count default + slot default (slot already rewritten by TypedConst).
- FastList<User>(4) capacities (ServerUserSystem, ClientUserSystem, UsersChangedMessage, ResetUsersToOffline): initial capacity only (FastList grows).
- UserData.machine 2 bits (MachineID.Count=4 can't be encoded, original behaviour; values 0..3 fine).
- UserSystemUtils.SetupUserConfig / UserSystemDebugDisplay ldc 4 and 7: string-format array indices.
- RemoveMatchmadeUsers FastList(4), JoinData bits for JoinMethod (4 bits, unrelated).
- Not mine (other subsystems): FrontendPlayerLobby (424,1405,1780,1811, m_animSets[4]), GamepadEngagementManager, PadSplitManager, PCPlayerManager, MultiChefUIController.m_padUI[4], MapAvatarControlsMessage.m_bHorns[4], PhysicsObjectMessage.Contacts[4], ClientPhysicsObjectSynchroniser.m_ContactingChefs[4], PlayerLobbyFlowroutine, PrivilegeCheckAllUsers*Task.m_Tasks[4] (online).

## Residual risks
1. **Wire incompatibility with unmodded peers**: UserData/UsersChanged/ChefAvatar/ControllerSettings/Join/Lobby message layouts changed. Local play (loopback) is symmetrical. Online with a vanilla client will break; if needed, gate widths on a flag.
2. Online with >4 local users: AddUser now allows 8 users; nothing forces trimming to 4 when switching online (`DisengageNonRequiredUsersForOnline` keeps only 1 or all). Online with 5+ local users is unsupported; AtMaxUserCount blocks the online buttons when >=4 users.
3. Coop colours 5-8 come from runtime-cloned ChefColourData; ChefMaterial/Background sprite are shared with clones of 0-3. Other code using `Colours[(int)player]` (UI) benefits, but any UI that cached the old directory before the postfix would not.
4. Cloned theme-selection UI elements may overlap visually in the lobby layout.
5. Unity-serialised data (LobbyFlowController `m_*TeamColourIndex`) is recomputed in Awake so the sentinel change is safe.

## Verify in game
- Join 5..8 pads: no exception in ServerUserSystem.AddUser, UsersChanged message round-trip (8 users, colours 0..7 distinct, slots 4..7).
- Remove a middle user: colours reassigned, lobby m_userChoices shifts correctly.
- Lobby theme selection with 8 users (ThemeChoice array, m_themeSelections clones, chef index up to 7 in lobby messages).
- Versus: team colours still resolve (no-team/red/blue indices after the colour insert) and chef colours for teams.
- Offline<->online switch with 1-4 users still behaves; online buttons blocked when >=4 users.
- FrontendPlayerSlot colour for user with invalid colour (sentinel 15).
