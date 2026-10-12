# RECON_REPORT.md (in progress)

Status legend: **[V]** verified by inspecting files/decompile, **[U]** unverified.

## Environment [V]
- Game dir: `<GameDir>` — **GOG** build (goggame-1297999995.info, gameId 1297999995, buildId 56415526248344738), *not* Steam.
- Exe: `Overcooked2.exe` (x64), Unity **2018.4.32f1**, Mono backend (`MonoBleedingEdge`, managed `Overcooked2_Data\Managed\Assembly-CSharp.dll`, 2.8 MB).
- Input library: **InControl** (`InControl.dll`) — game uses its own `XInputProvider`, `PCPlayerManager`, `GamepadUser`.
- No BepInEx / Doorstop / Harmony / other loader present in the game dir [V].
- No .NET SDK installed system-wide (runtimes 6.0/8.0 only). Local SDK 8.0.425 installed to `<workspace>\tools\dotnet` (workspace only). `ilspycmd 8.2.0.7535` installed to `tools\ilspy`.
- Disk: game 8.0 GB, C: has ~39 GB free. Game copied to a sandbox copy of the game folder as test sandbox so the original install is untouched.
- Controllers: none verified attached [U].

## Frameworks
- oc2-modding v1.9.9 (toasterparty): BepInEx 5 based, randomiser/Archipelago oriented; README says "PC version", doesn't state GOG support [U]. Source cloned at `dl\src-oc2`.
- BepInEx 5.4.23.5 x64 downloaded (`dl\BepInEx_win_x64_5.4.23.5.zip`).
- universal-modder: agent-skill toolkit (um scan/win/backup); no Overcooked-specific content.

## Four-player assumptions found in decompiled Assembly-CSharp (decomp at `decomp\acs`, 2366 files) [V]
Hard constants that Harmony cannot patch (compile-time inlined):
- `enum EngagementSlot { One, Two, Three, Four, Count }` (89 files reference it)
- `User.MachineID { One..Four, Count }`
- `Team17.Online.UserSystemUtils.kMaxUsers = 4` (const)
- `PlayerManagerShared.c_numEngagementSlots = 4`
Patchable members:
- `OnlineMultiplayerConfig.MaxPlayers => 4u` (property, patchable) — also used for bit widths in `OnlineMultiplayerSessionJoinUserDataHelper` and array sizes.
Literal 4s in player logic: `ServerUserSystem` (lines 157, 352, 383, 436), `UserSystemUtils.cs:244`, `FrontendPlayerLobby` (424, 1405, 1415, 1780, 1811, `m_animSets[4]`), `GamepadEngagementManager` (63, 96, 106, `m_cancelButtons[4]`), `OvercookedEngagementController:109`, `PadSplitManager` (62,124,183,189), `PCPlayerManager:175`, `PlayerManagerShared:290`, `PlayerSwitchingManager:141`, `PlayerControls:244`, `MultiChefUIController.m_padUI[4]`, `PlayerLobbyFlowroutine` (74,101,110), `ServerLobbyFlowController:262`, `Server.cs:245`, `XInputProvider.s_stateCache[4]`, message classes (`MapAvatarControlsMessage.m_bHorns[4]`, `PhysicsObjectMessage.Contacts[4]`, `ClientPhysicsObjectSynchroniser.m_ContactingChefs[4]`).
- `PlayerInputLookup.Player` enum already has 11 entries (One..Eleven, Count) — useful.
- Local play still flows through `ServerUserSystem`/`ClientUserSystem` + serialised message classes, so bit-width/array limits matter even offline.

## Key architectural consequence
Because the limits are in enums/consts/inline literals, a plain Harmony plugin is not sufficient. Chosen route: **BepInEx preloader patcher (Mono.Cecil) that rewrites Assembly-CSharp in memory at load time** (no on-disk modification), plus a normal Harmony plugin for runtime logic. Details in MODDING_PLAN.md.

## Hard constraint: XInput supports max 4 controllers
`XInputProvider` caches 4 `GamePadState`s; the XInput API itself exposes 4 slots. Eight independent pads need another InControl provider (UnityInputDeviceProvider/DirectInput) and/or keyboard + split pads. No 8 physical controllers are available for testing; this will be stated in TEST_REPORT.md.
