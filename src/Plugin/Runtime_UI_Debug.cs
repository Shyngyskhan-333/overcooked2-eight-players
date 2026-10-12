using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Team17.Online;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OC2.EightPlayers
{
    /// <summary>UI debug aid. F9 = toggle on-screen overlay, F10 = dump UI state to the BepInEx log ("[UI-STATE]"), F11 = screenshot to
    /// BepInEx\ui-shots\ (with the state dump written next to it). State is also dumped automatically on users-changed and scene load.
    /// Everything is try/catch'd; the overlay never throws into the game.</summary>
    class UiDebug : MonoBehaviour
    {
        static UiDebug inst;
        bool show; string text = "";
        float nextRefresh;
        GUIStyle style;

        internal static void Ensure()
        {
            if (inst != null) return;
            try
            {
                if (!Cfg.DebugOverlay.Value) return;
                var go = new GameObject("OC2-8P-UiDebug"); DontDestroyOnLoad(go);
                inst = go.AddComponent<UiDebug>();
                SceneManager.sceneLoaded += (s, m) => LogState("sceneLoaded " + s.name);
                UiU.Log("debug overlay ready (F9 overlay, F10 dump, F11 screenshot)");
            }
            catch (Exception e) { UiU.Err("UiDebug.Ensure", e); }
        }

        void Update()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.F9)) show = !show;
                if (Input.GetKeyDown(KeyCode.F10)) LogState("F10");
                if (Input.GetKeyDown(KeyCode.F11)) Shot();
                if (show && Time.unscaledTime > nextRefresh) { nextRefresh = Time.unscaledTime + 0.5f; text = Build(false); }
            }
            catch (Exception e) { UiU.Err("UiDebug.Update", e); enabled = false; }
        }

        void OnGUI()
        {
            if (!show) return;
            try
            {
                if (style == null) { style = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = false }; style.normal.textColor = Color.yellow; }
                GUI.Box(new Rect(4, 4, 620, Mathf.Min(Screen.height - 8, 20 + 15 * (text.Split('\n').Length))), "");
                GUI.Label(new Rect(8, 6, 612, Screen.height - 12), text, style);
            }
            catch { }
        }

        void Shot()
        {
            try
            {
                string dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "ui-shots"); Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("HHmmss") + "_" + SceneManager.GetActiveScene().name + "_" + Screen.width + "x" + Screen.height;
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, stamp + ".png"));
                File.WriteAllText(Path.Combine(dir, stamp + ".txt"), Build(true));
                UiU.Log("screenshot " + stamp);
            }
            catch (Exception e) { UiU.Err("UiDebug.Shot", e); }
        }

        internal static void LogState(string why)
        {
            try { Plugin.Log.LogInfo("[UI-STATE] (" + why + ")\n" + Build(true)); } catch { }
        }

        static string R(RectTransform rt)
        {
            if (rt == null) return "-";
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            // screen space px of the bottom-left / top-right corner; overlay canvases are already in px, world-space ones need a camera.
            Camera cam = Camera.main;
            Func<Vector3, Vector2> sp = v => { var canvas = rt.GetComponentInParent<Canvas>(); var root = canvas != null ? canvas.rootCanvas : null;
                if (root != null && root.renderMode == RenderMode.ScreenSpaceOverlay) return new Vector2(v.x, v.y);
                return cam != null ? (Vector2)cam.WorldToScreenPoint(v) : new Vector2(float.NaN, float.NaN); };
            var a = sp(c[0]); var b = sp(c[2]);
            return string.Format("[{0:0},{1:0} .. {2:0},{3:0}]", a.x, a.y, b.x, b.y);
        }

        static string Build(bool full)
        {
            var sb = new StringBuilder();
            sb.AppendLine("scene=" + SceneManager.GetActiveScene().name + " screen=" + Screen.width + "x" + Screen.height +
                          " users=" + ClientUserSystem.m_Users.Count + " server=" + ServerUserSystem.m_Users.Count);
            for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
            {
                var u = ClientUserSystem.m_Users._items[i];
                sb.AppendLine("  user" + i + " eng=" + (int)u.Engagement + " colour=" + u.Colour + " avatar=" + u.SelectedChefAvatar + " local=" + u.IsLocal + " split=" + u.Split + " team=" + u.Team);
            }
            foreach (var lobby in UnityEngine.Object.FindObjectsOfType<FrontendPlayerLobby>())
            {
                sb.AppendLine("FrontendPlayerLobby slots=" + lobby.m_PlayerSlots.Count + " chefs=" + (lobby.m_PlayerChefs == null ? -1 : lobby.m_PlayerChefs.Length));
                for (int i = 0; i < lobby.m_PlayerSlots.Count; i++)
                {
                    var s = lobby.m_PlayerSlots[i]; if (s == null) continue;
                    sb.AppendLine("  slot" + i + " eng=" + (int)s.m_EngagementSlot + " active=" + s.gameObject.activeInHierarchy + " interactable=" + (s.m_SlotButton != null && s.m_SlotButton.interactable) + " rect=" + R(s.transform as RectTransform));
                }
            }
            foreach (var m in UnityEngine.Object.FindObjectsOfType<FrontendChefMenu>())
                for (int i = 0; i < m.m_PlayerChefs.Length; i++)
                    sb.AppendLine("  chef" + i + " activeSelf=" + m.m_PlayerChefs[i].gameObject.activeSelf + " localPos=" + m.m_PlayerChefs[i].transform.localPosition + " active=" + m.m_PlayerChefs[i].IsActive());
            foreach (var r in UnityEngine.Object.FindObjectsOfType<UIPlayerRootMenu>())
            {
                sb.AppendLine("UIPlayerRootMenu(" + r.name + ") players=" + r.UIPlayers.Count);
                if (full) for (int i = 0; i < r.UIPlayers.Count; i++) sb.AppendLine("  uiplayer" + i + " rect=" + R(r.UIPlayers[i].transform as RectTransform) + " user=" + (r.UIPlayers[i].UserInfo != null));
            }
            foreach (var c in UnityEngine.Object.FindObjectsOfType<PlayerSelectCardUIController>())
                sb.AppendLine("  card player=" + (int)c.GetActualPlayer() + " active=" + c.IsActive() + " rect=" + R(c.transform as RectTransform));
            foreach (var c in UnityEngine.Object.FindObjectsOfType<MultiChefPadUIController>())
                sb.AppendLine("  padUI pad=" + (int)c.PadNum + " padActive=" + c.IsPadActive + " rect=" + R(c.transform as RectTransform));
            return sb.ToString();
        }
    }
}
