# Subsystem: INPUT + ENGAGEMENT (owner: Patches_Input.cs, Runtime_Input.cs)

Status: builds; verified offline only (patched IL dumped and reloaded). NOT yet tested in game.

## 1. How a controller becomes a PadNum and then an EngagementSlot (verified in decomp)
1. InControl 1.8.9 (`InControlManager`, `InputManager.Devices`). `PCPadInputProvider..cctor` wraps every device in a
   `StandardActionSet` and puts them in `m_allDevices` (keyboard first when `GameDebugConfig.m_keyboardType == Actual`,
   then each joystick in attach order; `OnDeviceAttached/Detached` keep the list live). No size limit anywhere.
2. `PadNum` is NOT a physical index. `PCPadInputProvider.GetActionSet(pad)`: if `s_engagedPads` has `pad` it returns that set;
   otherwise PadNum n means "the (n - #engaged pads below n)-th device not yet engaged". So unengaged PadNums are a
   compacted list over unengaged devices, and engaged devices sit at PadNum == their EngagementSlot.
3. `PlayerManagerShared.Start` registers an engagement button for PadNum 0..`GetSystemControllerMaximum()` (= **12** for
   PCPadInputProvider, already enough for 8). `GetEngagementPad` returns an unengaged pad whose engagement button
   (Action1/Space) was just pressed; `StartPadEngagement` picks `GetUnengagedSlot()` (first free slot) and
   `PCPlayerManager.EngagePadToSlot` calls `PCPadInputProvider.EngagePad(oldPad, (PadNum)slot)` -> `s_engagedPads[slot]=set`,
   then `AssignProfileToSlot(slot, PCPlayerProfile)`. `PCPlayerProfile.UID` = InControl `Device.SortOrder` (unique) or "Keyboard".
4. `UserSystemUtils.BuildGameInputConfig` builds `GameInputConfig` entries (Player i, Pad = user.Engagement, side) and
   `PadSplitManager.FixupConfigList` pads missing players. `PlayerInputLookup` then resolves Player -> PadNum -> engaged
   StandardActionSet, gated on `GetUser((EngagementSlot)pad) != null`.
5. `GameInputConfig.m_playerConfigs` already has 11 entries, `PlayerInputLookup.Player` has 11, `ControlPadInput.PadNum` 15.

Keyboard: a single device (`m_allDevices[0]`) whose bindings switch between combined and split sets via
`IsKeyboardSplit()` (any config entry on the keyboard pad with Side != Both). It occupies exactly one slot/PadNum like a
pad, so there is no PadNum collision when pads 5-8 exist: keyboard + 7 pads, or 8 pads, both fit under the max of 12.

## 2. How many physical controllers can the engine distinguish on Windows? (verified)
- Provider in use: **`NativeInputDeviceManager`** (player log line `[InControl] NativeInputDeviceManager enabled.`,
  InControl 1.8.9, InControl Native 1.0.6; see %LocalAppData%Low\Team17\Overcooked2\output_log.txt). Because native is enabled,
  `InputManager.SetupInternal` skips `XInputDeviceManager` and `UnityInputDeviceManager`. The scene's `InControlManager` flags
  are not readable offline, but the log proves the outcome.
- `InControlNative.dll` imports DINPUT8.dll and HID.DLL and also uses XInput (strings `XInputGetState`, `XInput::%d`).
  So: **XInput pads (Xbox 360/One/Series; Windows hard limit 4) + any number of DirectInput/HID pads** (DS4/DualSense,
  Switch Pro, generic). All become `NativeInputDevice` (SortOrder 1000+handle) in `InputManager.Devices`; InControl has no device cap.
- Honest answer: genuine Xbox pads max 4. Pads 5-8 must be non-XInput (PlayStation, Switch Pro, generic HID, or virtual DInput
  devices) or the keyboard. Not verifiable offline: whether InControlNative's native table has its own limit, and whether an
  XInput pad is also enumerated through DirectInput (InControl's Windows native profiles normally hide the duplicate).
  These are in-game checklist items.
- `XInputProvider` (4-entry `s_stateCache`) and `DirectInputProvider` are legacy/debug: only `DebugInputDisplay` reads them;
  `PlayerInputLookup.Update` merely polls `XInputProvider.Update()` for 4 PlayerIndex values. Not in the gameplay path: left alone.

## 3. Changed sites (Patches_Input.cs unless noted; every Il.* call has an exact expected count and throws on mismatch)
| Site | Change | Why |
|---|---|---|
| PCPlayerManager::Update | loop `i<4` -> 8 | detach detection: disengage slots 4-7 whose pad vanished |
| PlayerManagerShared`1::GetUnengagedSlot | loop `i<4` -> 8 (the `return Count` sentinel was TypedConst's) | first free slot now 0..7 |
| OvercookedEngagementController::SetUsersSticky | loop -> 8 | slots 4-7 become sticky so lost-pad rejoin (LostEngagedPlayerRoutine) works |
| DialogueAnimationState::HasDifferentControllers | loop -> 8 | scans engaged users for mixed keyboard/pad |
| GameUtils::GetEngagedAndInGame | loop -> 8 (tolerant: skipped if already 8) | deeds fired for every engaged chef |
| FrontendPlayerLobby::BuildInputConfig | loop -> 8 (tolerant; method appears unused) | consistency |
| GamepadEngagementManager::.ctor | `m_cancelButtons` new[4] -> new[8] | `OnEngagementChanged` writes `m_cancelButtons[(int)slot]` for ANY slot: IndexOutOfRange at slot 4 otherwise |
| GamepadEngagementManager::GatherButtons | loop -> 8 | fills that array |
| PadSplitManager::FixupConfigList | 3 loops (players, pads, handedness) -> 8 | every Player 0..7 gets a config entry / free pad |
| PlayerInputLookup::GetAnyButton (2 overloads), GetAnyValue | array literal 4 -> 8 elements (4th element block cloned for Player Five..Eight, `ExtendPlayerArray`) | "any player pressed" combos (menus, skip, pause) must include players 5-8 |
| MapAvatarControlsMessage ctor/Serialise/Deserialise | `bool[4]` + 4-bit loop -> 8 | world-map horns |
| ClientMapAvatarControls::BuildHornButtons | array + loop -> 8 | horn button per local player |
| Plugin Runtime_Input.cs `Patch_MapHorns58` | postfix on HandleMapAvatarMessage plays VanHorn tags for horns 4-7 (idx%4) | only 4 horn audio tags exist |
| Plugin Runtime_Input.cs `Patch_Log*` | logs device attach/detach and `EngagePad` mapping | verification aid, no behaviour change |
| Plugin csproj | + `UnityEngine.AudioModule` reference | needed for `GameUtils.TriggerAudio` return type |

## 4. Sites examined and deliberately left alone
- `PCPadInputProvider` (all), `StandardActionSet`, `PlayerManagerShared.Start/GetEngagementPad/LostEngagedPlayerRoutine`,
  `PCPlayerManager.BootstrapAwake/EngagePadToSlot`, `GalaxyPlayerManager`: already generic (12 pads); sentinel compares done by TypedConst.
  The `ldc.i4.4` in `GalaxyPlayerManager/<PadEngageRoutine>` and `LostEngagedPlayerRoutine` MoveNext are iterator state numbers.
- `PlayerInputLookup.GetSystemControllerMaximum` already 12; `GameInputConfig` 11 entries; `Player` enum 11.
- `GamepadEngagementManager::Update` (`Users.Count < 4`, cancel loop): runs only when `ClientGameSetup.Mode == OnlineKitchen`, so online stays 4.
- `PadSplitManager::Start/AnyOpenPressed`: `CanOpenPadSplit()` is `return false` in this build (dead feature);
  `MultiChefUIController.m_padUI[4]` (scene-serialised UI for it) likewise.
- `PlayerControls/ThrowIndicators::UpdateCosmetics` (`num < 4` indexes a Unity-serialised array of 4 textures): raising it would
  throw. Players 5-8 keep the default throw indicator. (Gameplay/UI owner could map `num & 3`.)
- `ControllerStateMessage` (`ReadByte(4)` = 4 button bits, not players), `ClientInputTransmitter`, `ServerInputReceiver`,
  `ClientPlayerControlsImpl_Default` (4 = PauseLayer/enum values), `EmoteWheel*` (4 = PauseLayer/string-array sizes; `EmoteWheelMessage` player is 3 bits, ok for 0..7),
  `KeyboardRebind*` (string arrays), `DebugInputDisplay`, `RemapPad`, `PCPadInputProvider.GetPadType` (enum), `MapAvatarHornMessage` (3 bits, ok).
- `ControllerSettingsMessage` serialises EngagementSlot in 3 bits: slots 0-7 fit, but the default sentinel `Count` (=8) wraps to 0. Users owner should be aware.
- `PlayerSwitchingManager::InitialiseAvatars` (loop over Player ids), `LobbyUIController.Awake` (empty loop),
  `CheckPrivilegesAndDropInAllLocalUsersTask`, `RichPresence`, invite handlers: Gameplay/Users/UI/online, not input.
- `PlayerLobbyFlowroutine::ConstructDebugInputConfig` (entries[4], debug skip-lobby config only): left at 4 players.
- Event systems: `T17EventSystemsManager.AssignFreeEventSystemToGamepadUser` is only called for the primary user (slot One) in
  the code paths found, so the scene's pool of T17EventSystem objects is not a per-player cap. UI owner should confirm.

## 5. Residual risks
- Genuine Xbox/XInput pads are capped at 4 by Windows; 8 real pads needs 4+ non-XInput devices. Unverified: InControlNative's own table size and XInput/DInput duplicate handling.
- Wire-format change (`MapAvatarControlsMessage` 4 -> 8 bits): a modded client cannot interoperate with an unmodded online peer (world-map message only). Fine if the mod is required on every online peer.
- `ExtendPlayerArray` clones IL; verified by dumping patched.dll (8 elements, indices 4-7 correct).
- Detach of a pad in slots 4-7 now disengages it (as vanilla for 0-3); a flapping USB pad drops that chef.
- Depends on `User.Engagement` / `ServerUserSystem` (Users subsystem) accepting slots 4-7.

## 6. In-game verification checklist (BepInEx/LogOutput.log shows `[input] device attached`, `[input] EngagePad`)
1. Start with >4 devices (keyboard + 4 XInput + 3 DS4/generic). Log lists every device on attach; `total InControl devices` > 4.
2. At the engagement screen press A/Space on each: `EngagePad: unengaged PadNum X -> slot/PadNum Y` for Y=0..7 in order.
3. Local lobby: 8 player cards, each controlled by its own pad; start a level; all 8 chefs move with their own pad.
4. Pull a pad of slot 5+ mid-level: rejoin overlay appears; replug/press A: slot re-engages.
5. Pause / any-player skip (GetAnyButton) works from pads 5-8.
6. World map: honk with pads 5-8 (reuses van horn sounds), no exceptions in log.
7. Keyboard combined and split with 7 pads: keyboard takes one slot; other pads keep their numbering.
8. Check the log for IndexOutOfRange / "Could not assign free event system" when pads 5-8 press UI buttons.
