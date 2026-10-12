using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Team17.Online;
using UnityEngine;
using UnityEngine.UI;

namespace OC2.EightPlayers
{
    // UI subsystem runtime patches. The plugin compiles against the UNPATCHED Assembly-CSharp: slot indices 4..7 are cast from ints.
    // Principle: every patch is wrapped in try/catch and logs with the "[UI]" prefix; a failure must never break the original flow.
    // Scene facts (verified with UnityPy on the original bundles, see docs\subsystem_ui.md):
    //   frontend: IngamePlayers/PlayerList/Holder (HorizontalLayoutGroup) holds 4 PlayerSlot; IngamePlayers/Players holds 4 FrontendChef;
    //   FullScreenPadSplitUI has Controller1..4; WorldMap PlayerSelectScreen has Panel1..4; ingamemenu CustomisationMenu/Arrows has 4 ChefArrows.
    internal static class UiU
    {
        internal const int Max = 8;
        static GameObject holder;

        internal static void Log(string s) { try { Plugin.Log.LogInfo("[UI] " + s); } catch { } }
        internal static void Warn(string s) { try { Plugin.Log.LogWarning("[UI] " + s); } catch { } }
        internal static void Err(string where, Exception e) { try { Plugin.Log.LogError("[UI] " + where + " failed: " + e); } catch { } }

        /// <summary>Instantiate under an INACTIVE holder so Awake/OnEnable of the clone do not run until <see cref="Attach"/>
        /// (lets us set serialised fields such as m_EngagementSlot first).</summary>
        internal static GameObject CloneInactive(GameObject src, string name)
        {
            if (holder == null) { holder = new GameObject("OC2-8P-CloneHolder"); holder.SetActive(false); UnityEngine.Object.DontDestroyOnLoad(holder); }
            var go = UnityEngine.Object.Instantiate(src, holder.transform, false);
            go.name = name;
            return go;
        }

        internal static void Attach(GameObject go, Transform parent) { go.transform.SetParent(parent, false); }

        internal static void SetField(object o, string name, object v)
        {
            var f = AccessTools.Field(o.GetType(), name);
            if (f == null) throw new MissingFieldException(o.GetType().Name, name);
            f.SetValue(o, v);
        }
        internal static T GetField<T>(object o, string name)
        {
            var f = AccessTools.Field(o.GetType(), name);
            if (f == null) throw new MissingFieldException(o.GetType().Name, name);
            return (T)f.GetValue(o);
        }

        internal static string Path(Transform t)
        {
            var s = t.name; for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s; return s;
        }
    }

    // ------------------------------------------------------------------------------------------------------------------
    // 1. FRONTEND LOBBY: 8 player slots (1x8 row). Slots are explicit scene objects; Start() wires listeners per slot so growth must precede it.
    // ------------------------------------------------------------------------------------------------------------------
    [HarmonyPatch(typeof(FrontendPlayerLobby), "Start")]
    static class FrontendPlayerLobby_Start_GrowSlots
    {
        static void Prefix(FrontendPlayerLobby __instance)
        {
            UiDebug.Ensure();
            try
            {
                var slots = __instance.m_PlayerSlots;
                if (slots == null || slots.Count == 0)
                {   // same fallback the original Start() does
                    slots = new List<FrontendPlayerSlot>(__instance.GetComponentsInChildren<FrontendPlayerSlot>(true));
                    __instance.m_PlayerSlots = slots;
                }
                int before = slots.Count;
                if (before == 0 || before >= UiU.Max) { UiU.Log("lobby slots: " + before + " (no growth)"); return; }
                var src = slots[before - 1];
                var parent = src.transform.parent;
                for (int k = before; k < UiU.Max; k++)
                {
                    var go = UiU.CloneInactive(src.gameObject, "PlayerSlot (" + k + ")");
                    var fps = go.GetComponent<FrontendPlayerSlot>();
                    fps.m_EngagementSlot = (EngagementSlot)k;
                    UiU.Attach(go, parent);
                    slots.Add(fps);
                }
                // Keep the authored panel size (the chalkboard panel is fixed art) and lay the 8 slots out as a compact 2x4 grid
                // inside it: disable the HorizontalLayoutGroup on Holder and position the slots by hand at reduced scale.
                var list = parent.parent as RectTransform;
                if (list != null)
                {
                    var hlg = parent.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                    if (hlg != null) hlg.enabled = false;
                    float cellW = list.sizeDelta.x / before, cellH = list.sizeDelta.y;
                    float s = Cfg.LobbySlotScale.Value;
                    int cols = before;                                  // 4 columns, rows = ceil(8/4)
                    int rows = (slots.Count + cols - 1) / cols;
                    for (int i = 0; i < slots.Count; i++)
                    {
                        var rt = slots[i].GetComponent<RectTransform>();
                        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.sizeDelta = new Vector2(cellW, cellH);
                        rt.localScale = new Vector3(s, s, 1f);
                        int col = i % cols, row = i / cols;
                        float x = (col - (cols - 1) * 0.5f) * cellW;
                        float y = ((rows - 1) * 0.5f - row) * cellH * s * 1.04f + Cfg.LobbySlotYOffset.Value;
                        rt.anchoredPosition = new Vector2(x, y);
                    }
                    UiU.Log("lobby slots laid out as " + rows + "x" + cols + " grid inside the original PlayerList (" + list.sizeDelta + "), scale " + s);
                }
                else UiU.Warn("lobby PlayerList rect not found; slots left in the layout group");
                UiU.Log("lobby slots grown " + before + " -> " + slots.Count + " under " + UiU.Path(parent));
                var chefs = __instance.m_PlayerChefs;
                UiU.Log("lobby m_PlayerChefs: " + (chefs == null ? "null" : chefs.Length + " entries, " + chefs.Count(c => c != null) + " non-null"));
            }
            catch (Exception e) { UiU.Err("FrontendPlayerLobby.Start prefix", e); }
        }
    }

    /// <summary>OnUserAdded -> m_PlayerChefs[idx] would be IndexOutOfRange for idx >= 4 (the array has 4 entries).</summary>
    [HarmonyPatch(typeof(FrontendPlayerLobby), "TriggerUserJoinEffect")]
    static class FrontendPlayerLobby_TriggerUserJoinEffect_Guard
    {
        static bool Prefix(FrontendPlayerLobby __instance, int _userIdx)
        {
            var c = __instance.m_PlayerChefs;
            return c != null && _userIdx >= 0 && _userIdx < c.Length;
        }
    }

    [HarmonyPatch(typeof(FrontendPlayerLobby), "OnUsersChangedEvent")]
    static class FrontendPlayerLobby_OnUsersChanged_Log
    {
        static void Postfix() { UiDebug.LogState("usersChanged"); }
    }

    // ------------------------------------------------------------------------------------------------------------------
    // 2. FRONTEND CHEF CUSTOMISATION: 3D chefs (FrontendChef + FrontendChefCustomisation) 4 -> 8 and re-spread.
    // ------------------------------------------------------------------------------------------------------------------
    static class UiChefs
    {
        internal static void Grow(FrontendChefMenu menu)
        {
            var arr = menu.m_PlayerChefs;
            if (arr == null || arr.Length == 0 || arr.Length >= UiU.Max) return;
            int before = arr.Length;
            var parent = arr[before - 1].transform.parent;
            // remember the authored pose of the originals (position/rotation cycle for the new ones)
            var pos = arr.Select(c => c.transform.localPosition).ToArray();
            var rot = arr.Select(c => c.transform.localRotation).ToArray();
            var scl = arr[0].transform.localScale;
            var list = new List<FrontendChefCustomisation>(arr);
            for (int k = before; k < UiU.Max; k++)
            {
                var go = UiU.CloneInactive(arr[before - 1].gameObject, "FrontendChef (" + k + ")");
                var cc = go.GetComponent<FrontendChefCustomisation>();
                cc.m_actualPlayer = (PlayerInputLookup.Player)k;
                var pid = go.GetComponent<PlayerIDProvider>(); if (pid != null) pid.OverridePlayerId((PlayerInputLookup.Player)k);
                UiU.Attach(go, parent);
                list.Add(cc);
            }
            menu.m_PlayerChefs = list.ToArray();
            // spread: evenly over the span that fits the world-space canvas (tunable)
            float x0 = Cfg.ChefX0.Value, dx = Cfg.ChefDX.Value, s = Cfg.ChefScale.Value;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i].transform;
                var p = pos[i % before];
                t.localPosition = new Vector3(x0 + dx * i, p.y, p.z);
                t.localRotation = rot[i % before];
                t.localScale = scl * s;
            }
            UiU.Log("chef menu chefs " + before + " -> " + list.Count + " under " + UiU.Path(parent) + " x0=" + x0 + " dx=" + dx + " scale=" + s);
        }
    }

    [HarmonyPatch(typeof(FrontendChefMenu), "Start")]
    static class FrontendChefMenu_Start_Grow { static void Prefix(FrontendChefMenu __instance) { try { UiChefs.Grow(__instance); } catch (Exception e) { UiU.Err("FrontendChefMenu.Start", e); } } }

    [HarmonyPatch(typeof(FrontendChefMenu), "Show")]
    static class FrontendChefMenu_Show_Grow { static void Prefix(FrontendChefMenu __instance) { try { UiChefs.Grow(__instance); } catch (Exception e) { UiU.Err("FrontendChefMenu.Show", e); } } }

    // ------------------------------------------------------------------------------------------------------------------
    // 3. IN-GAME PAUSE: customisation arrows 4 -> 8 (HorizontalLayoutGroup re-divides width), UIPlayers row fitted to the screen.
    // ------------------------------------------------------------------------------------------------------------------
    static class UiInGameCust
    {
        internal static void Grow(InGameCustomisation menu)
        {
            var arr = UiU.GetField<FrontendChefCustomisation[]>(menu, "m_chefCustomisation");
            if (arr == null || arr.Length == 0 || arr.Length >= UiU.Max) return;
            int before = arr.Length;
            var parent = arr[before - 1].transform.parent;
            var list = new List<FrontendChefCustomisation>(arr);
            for (int k = before; k < UiU.Max; k++)
            {
                var go = UiU.CloneInactive(arr[before - 1].gameObject, "ChefArrows (" + k + ")");
                go.GetComponent<FrontendChefCustomisation>().m_actualPlayer = (PlayerInputLookup.Player)k;
                UiU.Attach(go, parent);
                list.Add(go.GetComponent<FrontendChefCustomisation>());
            }
            UiU.SetField(menu, "m_chefCustomisation", list.ToArray());
            UiU.Log("in-game customisation arrows " + before + " -> " + list.Count);
        }
    }

    [HarmonyPatch(typeof(InGameCustomisation), "SingleTimeInitialize")]
    static class InGameCustomisation_Init_Grow { static void Prefix(InGameCustomisation __instance) { try { UiInGameCust.Grow(__instance); } catch (Exception e) { UiU.Err("InGameCustomisation.SingleTimeInitialize", e); } } }

    /// <summary>UIPlayerRootMenu (lobbies + pause menu) instantiates one UIPlayer per user into a HorizontalLayoutGroup+ContentSizeFitter row
    /// sized for 4. With more users, scale the Players/Backgrounds rows (centre pivot) so the row keeps its 4-player width.</summary>
    class UiRowFitter : MonoBehaviour
    {
        UIPlayerRootMenu root; Transform players; Transform backs; float last = -1f;
        internal void Init(UIPlayerRootMenu r)
        {
            root = r; players = UiU.GetField<Transform>(r, "m_playersContainer"); backs = r.m_backgroundsContainer;
        }
        void LateUpdate()
        {
            if (root == null || players == null) return;
            int n = root.UIPlayers.Count;
            float s = n > 4 ? 4f / n : 1f;
            if (Mathf.Approximately(s, last)) return;
            last = s;
            players.localScale = new Vector3(s, s, 1f);
            if (backs != null && backs != players) backs.localScale = new Vector3(s, s, 1f);
            UiU.Log("UIPlayerRootMenu row scale " + s + " for " + n + " players (" + UiU.Path(players) + ")");
        }
    }

    [HarmonyPatch(typeof(UIPlayerRootMenu), "Start")]
    static class UIPlayerRootMenu_Start_Fit
    {
        static void Postfix(UIPlayerRootMenu __instance)
        {
            UiDebug.Ensure();
            try { if (__instance.GetComponent<UiRowFitter>() == null) __instance.gameObject.AddComponent<UiRowFitter>().Init(__instance); }
            catch (Exception e) { UiU.Err("UIPlayerRootMenu.Start", e); }
        }
    }

    // ------------------------------------------------------------------------------------------------------------------
    // 4. PAD-SPLIT UI (MultiChefUIController): Controller1..4 -> 8 in a 4x2 grid. m_padUI is filled in Awake() so grow in a prefix.
    // ------------------------------------------------------------------------------------------------------------------
    [HarmonyPatch(typeof(MultiChefUIController), "Awake")]
    static class MultiChefUIController_Awake_Grow
    {
        static void Prefix(MultiChefUIController __instance)
        {
            try
            {
                var arr = UiU.GetField<MultiChefPadUIController[]>(__instance, "m_padUI");
                // The patched ctor allocates 8, but the serialised scene array overrides it with 4 (or fewer non-null).
                var list = (arr ?? new MultiChefPadUIController[0]).Where(p => p != null).ToList();
                int before = list.Count;
                if (before == 0 || before >= UiU.Max) { UiU.Log("pad UI count " + before + " (no growth)"); return; }
                var src = list[before - 1];
                for (int k = before; k < UiU.Max; k++)
                {
                    var go = UiU.CloneInactive(src.gameObject, "Controller" + (k + 1));
                    var pad = go.GetComponent<MultiChefPadUIController>();
                    UiU.SetField(pad, "m_padNum", (ControlPadInput.PadNum)k);
                    UiU.Attach(go, __instance.transform);
                    list.Add(pad);
                }
                UiU.SetField(__instance, "m_padUI", list.ToArray());
                // 4x2 grid directly under the controller root (the stock ones sit in half-screen panels, one with a Mask).
                for (int i = 0; i < list.Count; i++)
                {
                    var rt = list[i].transform as RectTransform;
                    rt.SetParent(__instance.transform, false);
                    int col = i % 4, row = i / 4;
                    var a = new Vector2((col + 0.5f) / 4f, row == 0 ? 0.68f : 0.30f);
                    rt.anchorMin = a; rt.anchorMax = a; rt.anchoredPosition = Vector2.zero;
                    rt.localScale = new Vector3(Cfg.PadScale.Value, Cfg.PadScale.Value, 0f);
                }
                UiU.Log("pad-split UI controllers " + before + " -> " + list.Count + " (4x2 grid, scale " + Cfg.PadScale.Value + ")");
            }
            catch (Exception e) { UiU.Err("MultiChefUIController.Awake", e); }
        }
    }

    // ------------------------------------------------------------------------------------------------------------------
    // 5. PRE-LEVEL PLAYER CARDS (LobbyUIController): Panel1..4 (RectTransformExtension fixed-ratio anchors) -> 8 half-size cards.
    // ------------------------------------------------------------------------------------------------------------------
    [HarmonyPatch(typeof(LobbyUIController), "Awake")]
    static class LobbyUIController_Awake_Grow
    {
        static void Prefix(LobbyUIController __instance)
        {
            try
            {
                var cards = __instance.GetComponentsInChildren<PlayerSelectCardUIController>(true).OrderBy(c => (int)c.GetActualPlayer()).ToList();
                int before = cards.Count;
                if (before == 0 || before >= UiU.Max) { UiU.Log("select cards: " + before + " (no growth)"); return; }
                var src = cards[before - 1];
                for (int k = before; k < UiU.Max; k++)
                {
                    var go = UiU.CloneInactive(src.gameObject, "Panel" + (k + 1));
                    var c = go.GetComponent<PlayerSelectCardUIController>();
                    UiU.SetField(c, "m_actualPlayer", (PlayerInputLookup.Player)k);
                    UiU.Attach(go, src.transform.parent);
                    cards.Add(c);
                }
                // fixed-ratio grid: left half = even players, right half = odd; each side a 2x2 of half-size cards (aspect preserved).
                float[] sideX = { 0.00f, 0.73f }; float cw = 0.17f, ch = 0.25f; float[] rowY = { 0.45f, 0.20f };
                for (int i = 0; i < cards.Count; i++)
                {
                    var ext = cards[i].GetComponent<RectTransformExtension>();
                    if (ext == null) { UiU.Warn("card " + i + " has no RectTransformExtension"); continue; }
                    int side = i % 2, j = i / 2, col = j % 2, row = j / 2;
                    float x = sideX[side] + col * cw, y = rowY[row];
                    UiU.SetField(ext, "m_fixedRatioAnchorMin", new Vector2(x, y));
                    UiU.SetField(ext, "m_fixedRatioAnchorMax", new Vector2(x + cw, y + ch));
                    ext.AnchorOffset = ext.AnchorOffset;   // setter re-runs UpdateAspectRatio
                }
                UiU.Log("select cards " + before + " -> " + cards.Count);
            }
            catch (Exception e) { UiU.Err("LobbyUIController.Awake", e); }
        }
    }

    /// <summary>LobbyUIController.IsReadyForStart: competitive allowed only 2 or 4 chefs; coop needs a level variant for the player count.</summary>
    [HarmonyPatch(typeof(LobbyUIController), "IsReadyForStart")]
    static class LobbyUIController_IsReadyForStart_Fix
    {
        static bool Prefix(LobbyUIController __instance, ref bool __result)
        {
            try
            {
                var mi = AccessTools.Method(typeof(LobbyUIController), "AllCardsReady");
                var args = new object[] { null };
                if (!(bool)mi.Invoke(__instance, args)) { __result = false; return false; }
                var chef = (GameSession.SelectedChefData[])args[0];
                if (GameUtils.GetGameSession().TypeSettings.Type == GameSession.GameType.Competitive)
                {
                    int n = chef.Length;
                    if (n < 2 || n % 2 != 0) { __result = false; return false; }
                    __result = chef.GroupBy(c => c.Colour).All(g => g.Count() == n / 2);
                    return false;
                }
                var entry = UiU.GetField<SceneDirectoryData.SceneDirectoryEntry>(__instance, "m_sceneDirectoryEntry");
                __result = entry.GetSceneVarient(chef.Length) != null;
                return false;
            }
            catch (Exception e) { UiU.Err("LobbyUIController.IsReadyForStart (falling back to original)", e); return true; }
        }
    }

    /// <summary>Levels ship variants for 1..4 players (+ optional -1 wildcard); reuse the 4-player variant for 5..8.</summary>
    [HarmonyPatch(typeof(SceneDirectoryData.SceneDirectoryEntry), "GetSceneVarient")]
    static class SceneDirectoryEntry_GetSceneVarient_Fallback
    {
        static void Postfix(SceneDirectoryData.SceneDirectoryEntry __instance, int _playerCount, ref SceneDirectoryData.PerPlayerCountDirectoryEntry __result)
        {
            if (__result == null && _playerCount > 4 && _playerCount <= UiU.Max) __result = __instance.GetSceneVarient(4);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------
    // Config
    // ------------------------------------------------------------------------------------------------------------------
    static class Cfg
    {
        static BepInEx.Configuration.ConfigFile C { get { return Plugin.Instance.Config; } }
        static BepInEx.Configuration.ConfigEntry<float> chefX0, chefDX, chefScale, padScale;
        static BepInEx.Configuration.ConfigEntry<bool> debugOverlay;
        internal static BepInEx.Configuration.ConfigEntry<float> ChefX0 { get { return chefX0 ?? (chefX0 = C.Bind("UI", "ChefX0", -1.9f, "Frontend chef customisation: local x of the first of 8 chefs (original 4 chefs span -1.34..1.62)")); } }
        internal static BepInEx.Configuration.ConfigEntry<float> ChefDX { get { return chefDX ?? (chefDX = C.Bind("UI", "ChefDX", 0.65f, "Frontend chefs: local x spacing (original ~0.99)")); } }
        internal static BepInEx.Configuration.ConfigEntry<float> ChefScale { get { return chefScale ?? (chefScale = C.Bind("UI", "ChefScale", 0.7f, "Frontend chefs: scale multiplier applied to the original 0.7 chef scale when 8 are shown")); } }
        static BepInEx.Configuration.ConfigEntry<float> lobbySlotScale;
        internal static BepInEx.Configuration.ConfigEntry<float> LobbySlotScale { get { return lobbySlotScale ?? (lobbySlotScale = C.Bind("UI", "LobbySlotScale", 0.62f, "Frontend lobby: scale of each of the 8 player slots (laid out 2 rows x 4 inside the original panel)")); } }
        static BepInEx.Configuration.ConfigEntry<float> lobbySlotY;
        internal static BepInEx.Configuration.ConfigEntry<float> LobbySlotYOffset { get { return lobbySlotY ?? (lobbySlotY = C.Bind("UI", "LobbySlotYOffset", -22f, "Frontend lobby: vertical offset of the 8-slot grid (canvas units, negative = down)")); } }
        internal static BepInEx.Configuration.ConfigEntry<float> PadScale { get { return padScale ?? (padScale = C.Bind("UI", "PadSplitScale", 0.2f, "Pad-split UI: scale of each controller panel")); } }
        internal static BepInEx.Configuration.ConfigEntry<bool> DebugOverlay { get { return debugOverlay ?? (debugOverlay = C.Bind("UI", "DebugOverlay", true, "Create the UI debug overlay (F9 toggle, F10 dump UI state to log, F11 screenshot)")); } }
    }
}
