using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Team17.Online;
using UnityEngine;

namespace OC2.EightPlayers
{
    // USERS subsystem runtime patches (the plugin compiles against the UNPATCHED Assembly-CSharp, so no EngagementSlot.Five..Eight here).
    // Kept in sync with Patches_Users.cs: User colour sentinel is 15 at runtime (was 7).
    static class UsersRuntime
    {
        internal const uint InvalidColour = 15u;
        internal const int LocalPlayers = 8;
    }

    /// <summary>User.Colour indexes AvatarDirectoryData.Colours; with 8 players the index can exceed the array. Bounds-safe replacement
    /// of User.RefreshSelectedChefData (original also used the old sentinel 7, patched to 15 by the Cecil patch).</summary>
    [HarmonyPatch(typeof(User), "RefreshSelectedChefData")]
    static class User_RefreshSelectedChefData_Patch
    {
        static readonly AccessTools.FieldRef<User, GameSession.SelectedChefData> Data = AccessTools.FieldRefAccess<User, GameSession.SelectedChefData>("m_SelectedChefData");

        static bool Prefix(User __instance)
        {
            try
            {
                if (127u != __instance.SelectedChefAvatar && UsersRuntime.InvalidColour != __instance.Colour)
                {
                    var dir = GameUtils.GetAvatarDirectoryData();
                    if (dir == null) return false;                       // original: leave unchanged
                    if (dir.Avatars == null || dir.Colours == null || dir.Colours.Length == 0 || __instance.SelectedChefAvatar >= (uint)dir.Avatars.Length)
                    { Data(__instance) = null; return false; }
                    var avatar = dir.Avatars[__instance.SelectedChefAvatar];
                    var colour = dir.Colours[__instance.Colour % (uint)dir.Colours.Length];   // wrap instead of IndexOutOfRange
                    var cur = Data(__instance);
                    if (cur == null) Data(__instance) = new GameSession.SelectedChefData(avatar, colour);
                    else { cur.Character = avatar; cur.Colour = colour; }
                }
                else Data(__instance) = null;
                return false;
            }
            catch (Exception e) { Plugin.Log.LogWarning("RefreshSelectedChefData fallback to original: " + e.Message); return true; }
        }
    }

    /// <summary>Coop colour of player i is Colours[i] (UpdatePlayersColourAssignments / RefreshUserColour). The shipped directory only has
    /// enough distinct chef colours for 4 players (the rest of the array is no-team/red/blue etc). Insert 4 extra player colours at
    /// indices 4..7 (clones of 0..3 with the hue replaced). Team colours are looked up by reference (LobbyFlowController.GetColourIndex,
    /// CompetitiveKitchenLoaderManager) so shifting them is safe.</summary>
    [HarmonyPatch(typeof(MetaGameProgress), "CombineAvatarDirectories")]
    static class MetaGameProgress_CombineAvatarDirectories_Patch
    {
        static readonly float[] Hues = { 0.08f, 0.78f, 0.5f, 0.92f };   // orange, purple, cyan, pink
        static readonly string[] Names = { "Orange", "Purple", "Cyan", "Pink" };

        static void Postfix(AvatarDirectoryData __result)
        {
            try
            {
                var list = __result.Colours.ToList();
                if (list.Count < 4) { Plugin.Log.LogWarning("avatar directory has <4 colours (" + list.Count + "); not extending"); return; }
                var extra = new List<ChefColourData>();
                for (int i = 0; i < 4; i++)
                {
                    var c = UnityEngine.Object.Instantiate(list[i]);
                    c.name = list[i].name + "_8P_" + Names[i];
                    c.MaskColour = Shift(c.MaskColour, Hues[i]);
                    c.UIColour = Shift(c.UIColour, Hues[i]);
                    c.DarkUIColour = Shift(c.DarkUIColour, Hues[i]);
                    c.PadUIColour = Shift(c.PadUIColour, Hues[i]);
                    c.PadBarColour = Shift(c.PadBarColour, Hues[i]);
                    extra.Add(c);
                }
                list.InsertRange(4, extra);
                __result.Colours = list.ToArray();
                Plugin.Log.LogInfo("avatar directory colours extended to " + list.Count + " (extra player colours at 4..7)");
            }
            catch (Exception e) { Plugin.Log.LogError("extend colours failed: " + e); }
        }

        static Color Shift(Color c, float hue)
        {
            float h, s, v; Color.RGBToHSV(c, out h, out s, out v);
            if (s < 0.25f) s = 0.6f;     // grey-ish source would stay grey; force some saturation
            var o = Color.HSVToRGB(hue, s, v); o.a = c.a; return o;
        }
    }

    /// <summary>LobbyFlowController.m_themeSelections is a serialised scene array of 4 UI elements, indexed by chef/user index.
    /// ClientLobbyFlowController indexes it by user index (0..7) so it must have 8 entries: clone the last element.</summary>
    [HarmonyPatch(typeof(LobbyFlowController), "Awake")]
    static class LobbyFlowController_Awake_Patch
    {
        static void Postfix(LobbyFlowController __instance)
        {
            try
            {
                var a = __instance.m_themeSelections;
                if (a == null || a.Length == 0 || a.Length >= UsersRuntime.LocalPlayers) return;
                var src = a[a.Length - 1];
                if (src == null) return;
                var n = new ThemeChoiceElement[UsersRuntime.LocalPlayers];
                Array.Copy(a, n, a.Length);
                for (int i = a.Length; i < n.Length; i++)
                {
                    var go = UnityEngine.Object.Instantiate(src.gameObject, src.transform.parent, false);
                    go.name = src.gameObject.name + "_8P_" + i;
                    n[i] = go.GetComponent<ThemeChoiceElement>();
                }
                __instance.m_themeSelections = n;
                Plugin.Log.LogInfo("LobbyFlowController.m_themeSelections extended to " + n.Length);
            }
            catch (Exception e) { Plugin.Log.LogError("extend m_themeSelections failed: " + e); }
        }
    }
}
