using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InControl;
using UnityEngine;

namespace OC2.EightPlayers
{
    /// <summary>TEST AID ONLY (off by default, config Debug/SimPads). A scripted InControl device that goes through the same
    /// PCPadInputProvider -> engagement -> player pipeline as a real controller. Results obtained with it are SIMULATED input,
    /// not proof of compatibility with physical controllers.</summary>
    public class VirtualPad : InputDevice
    {
        static readonly InputControlType[] Buttons =
        {
            InputControlType.Action1, InputControlType.Action2, InputControlType.Action3, InputControlType.Action4,
            InputControlType.LeftBumper, InputControlType.RightBumper, InputControlType.DPadUp, InputControlType.DPadDown,
            InputControlType.DPadLeft, InputControlType.DPadRight, InputControlType.Back, InputControlType.Command,
            InputControlType.LeftStickButton, InputControlType.RightStickButton,
        };
        static readonly InputControlType[] Analogs =
        {
            InputControlType.LeftTrigger, InputControlType.RightTrigger,
            InputControlType.LeftStickLeft, InputControlType.LeftStickRight, InputControlType.LeftStickUp, InputControlType.LeftStickDown,
            InputControlType.RightStickLeft, InputControlType.RightStickRight, InputControlType.RightStickUp, InputControlType.RightStickDown,
        };

        public readonly int Index;
        readonly Dictionary<InputControlType, bool> held = new Dictionary<InputControlType, bool>();
        public Vector2 LeftStick;

        public VirtualPad(int index) : base("Virtual Pad " + index)
        {
            Index = index;
            Meta = "OC2 eight-player simulated device";
            SortOrder = 5000 + index;
            DeviceClass = InputDeviceClass.Controller;
            DeviceStyle = InputDeviceStyle.XboxOne;
            foreach (var b in Buttons) { AddControl(b, b.ToString()); held[b] = false; }
            foreach (var a in Analogs) AddControl(a, a.ToString());
        }

        public void Set(InputControlType b, bool down) { held[b] = down; }

        public override void Update(ulong updateTick, float deltaTime)
        {
            foreach (var b in Buttons) UpdateWithState(b, held[b], updateTick, deltaTime);
            UpdateWithValue(InputControlType.LeftTrigger, 0f, updateTick, deltaTime);
            UpdateWithValue(InputControlType.RightTrigger, 0f, updateTick, deltaTime);
            UpdateWithValue(InputControlType.LeftStickLeft, Mathf.Max(0f, -LeftStick.x), updateTick, deltaTime);
            UpdateWithValue(InputControlType.LeftStickRight, Mathf.Max(0f, LeftStick.x), updateTick, deltaTime);
            UpdateWithValue(InputControlType.LeftStickUp, Mathf.Max(0f, LeftStick.y), updateTick, deltaTime);
            UpdateWithValue(InputControlType.LeftStickDown, Mathf.Max(0f, -LeftStick.y), updateTick, deltaTime);
            UpdateWithValue(InputControlType.RightStickLeft, 0f, updateTick, deltaTime);
            UpdateWithValue(InputControlType.RightStickRight, 0f, updateTick, deltaTime);
            UpdateWithValue(InputControlType.RightStickUp, 0f, updateTick, deltaTime);
            UpdateWithValue(InputControlType.RightStickDown, 0f, updateTick, deltaTime);
        }
    }

    /// <summary>Scene helpers for the sim commands (test aid only).</summary>
    static class SimWorld
    {
        public static float F(string s) { return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture); }

        public static PlayerIDProvider Chef(int playerId)
        {
            var all = PlayerIDProvider.s_AllProviders;
            for (int i = 0; i < all.Count; i++)
                if (all._items[i] != null && (int)all._items[i].GetID() == playerId) return all._items[i];
            return null;
        }

        public static void Place(PlayerIDProvider chef, Vector3 pos)
        {
            chef.transform.position = pos;
            var rb = chef.GetComponent<Rigidbody>();
            if (rb != null) { rb.position = pos; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            Physics.SyncTransforms();
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++) { var r = FindDeep(root.GetChild(i), name); if (r != null) return r; }
            return null;
        }
    }

    /// <summary>Polls BepInEx\8p-sim.txt (relative to the game folder) once a quarter second; each line is a command, then the file is emptied.
    ///   attach N            attach N virtual pads (numbered after the ones already attached)
    ///   detach I|all        detach pad I (1-based) or all
    ///   press I BTN [s]     hold button BTN (A B X Y LB RB UP DOWN LEFT RIGHT START BACK) for s seconds (default 0.15)
    ///   move I X Y [s]      push left stick (X,Y in -1..1) for s seconds (default 0.5), then release
    ///   all press BTN [s]   same for every attached pad
    ///   log TEXT            write TEXT to the BepInEx log (markers for test reports)</summary>
    static class SimPads
    {
        static readonly List<VirtualPad> pads = new List<VirtualPad>();
        static readonly string cmdFile = Path.Combine("BepInEx", "8p-sim.txt");

        static readonly Dictionary<string, InputControlType> btn = new Dictionary<string, InputControlType>(StringComparer.OrdinalIgnoreCase)
        {
            {"A", InputControlType.Action1}, {"B", InputControlType.Action2}, {"X", InputControlType.Action3}, {"Y", InputControlType.Action4},
            {"LB", InputControlType.LeftBumper}, {"RB", InputControlType.RightBumper}, {"UP", InputControlType.DPadUp},
            {"DOWN", InputControlType.DPadDown}, {"LEFT", InputControlType.DPadLeft}, {"RIGHT", InputControlType.DPadRight},
            {"START", InputControlType.Command}, {"BACK", InputControlType.Back},
        };

        public static void Start(Plugin host)
        {
            if (!Plugin.SimPadsEnabled.Value) return;
            Plugin.Log.LogInfo("[sim] virtual pad test aid ENABLED; commands via " + Path.GetFullPath(cmdFile));
            host.StartCoroutine(Loop(host));
        }

        static IEnumerator Loop(Plugin host)
        {
            var wait = new WaitForSecondsRealtime(0.25f);
            while (true)
            {
                yield return wait;
                string[] lines = null;
                try
                {
                    if (File.Exists(cmdFile))
                    {
                        var text = File.ReadAllText(cmdFile);
                        if (text.Length > 0) { File.WriteAllText(cmdFile, ""); lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries); }
                    }
                }
                catch (Exception e) { Plugin.Log.LogWarning("[sim] read failed: " + e.Message); }
                if (lines == null) continue;
                foreach (var l in lines)
                {
                    try { Run(host, l.Trim()); }
                    catch (Exception e) { Plugin.Log.LogError("[sim] command '" + l + "' failed: " + e); }
                }
            }
        }

        static void Run(Plugin host, string line)
        {
            if (line.Length == 0 || line.StartsWith("#")) return;
            var t = line.Split(' ');
            string cmd = t[0].ToLowerInvariant();
            Plugin.Log.LogInfo("[sim] > " + line);
            switch (cmd)
            {
                case "attach":
                {
                    int n = int.Parse(t[1]);
                    for (int i = 0; i < n; i++)
                    {
                        var p = new VirtualPad(pads.Count + 1); pads.Add(p); InputManager.AttachDevice(p);
                        Plugin.Log.LogInfo("[sim] attached " + p.Name + " attached=" + p.IsAttached + " total InControl devices=" + InputManager.Devices.Count);
                    }
                    break;
                }
                case "detach":
                    if (t[1] == "all") { foreach (var p in pads.ToArray()) { InputManager.DetachDevice(p); pads.Remove(p); } }
                    else { var p = pads[int.Parse(t[1]) - 1]; InputManager.DetachDevice(p); pads.Remove(p); }
                    break;
                case "press":
                    host.StartCoroutine(Press(pads[int.Parse(t[1]) - 1], btn[t[2]], t.Length > 3 ? float.Parse(t[3], System.Globalization.CultureInfo.InvariantCulture) : 0.15f));
                    break;
                case "uistate": // write the mod's UI state dump (slots, chef menu, pause UI) to the log
                    UiDebug.LogState("sim");
                    break;
                case "all":
                    if (t[1].ToLowerInvariant() == "press")
                        foreach (var p in pads.ToArray()) host.StartCoroutine(Press(p, btn[t[2]], t.Length > 3 ? float.Parse(t[3], System.Globalization.CultureInfo.InvariantCulture) : 0.15f));
                    break;
                case "move":
                    host.StartCoroutine(Move(pads[int.Parse(t[1]) - 1],
                        new Vector2(float.Parse(t[2], System.Globalization.CultureInfo.InvariantCulture), float.Parse(t[3], System.Globalization.CultureInfo.InvariantCulture)),
                        t.Length > 4 ? float.Parse(t[4], System.Globalization.CultureInfo.InvariantCulture) : 0.5f));
                    break;
                case "log":
                    Plugin.Log.LogInfo("[sim] MARK " + line.Substring(3).Trim());
                    break;
                case "find":    // find SUBSTR [max]  : list active scene objects whose name contains SUBSTR, with world position
                {
                    string sub = t[1].ToLowerInvariant(); int max = t.Length > 2 ? int.Parse(t[2]) : 25, n = 0;
                    foreach (var tr in UnityEngine.Object.FindObjectsOfType<Transform>())
                    {
                        if (!tr.gameObject.activeInHierarchy || !tr.name.ToLowerInvariant().Contains(sub)) continue;
                        Plugin.Log.LogInfo("[sim] FIND '" + tr.name + "' pos=" + tr.position.ToString("F1") + " parent=" + (tr.parent != null ? tr.parent.name : "-"));
                        if (++n >= max) break;
                    }
                    Plugin.Log.LogInfo("[sim] FIND done, " + n + " shown for '" + sub + "'");
                    break;
                }
                case "tp":      // tp P X Y Z : teleport the chef of player id P (0 = keyboard player)
                {
                    var chef = SimWorld.Chef(int.Parse(t[1]));
                    if (chef == null) { Plugin.Log.LogWarning("[sim] tp: no chef for player " + t[1]); break; }
                    SimWorld.Place(chef, new Vector3(SimWorld.F(t[2]), SimWorld.F(t[3]), SimWorld.F(t[4])));
                    break;
                }
                case "tpface":  // tpface P X Y Z TX TZ : place chef P at (X,Y,Z) looking toward (TX,TZ)
                {
                    var chef = SimWorld.Chef(int.Parse(t[1]));
                    if (chef == null) { Plugin.Log.LogWarning("[sim] tpface: no chef for player " + t[1]); break; }
                    var pos = new Vector3(SimWorld.F(t[2]), SimWorld.F(t[3]), SimWorld.F(t[4]));
                    SimWorld.Place(chef, pos);
                    chef.transform.rotation = Quaternion.LookRotation(new Vector3(SimWorld.F(t[5]) - pos.x, 0f, SimWorld.F(t[6]) - pos.z));
                    break;
                }
                case "tpnear":  // tpnear P SUBSTR [dist] : put chef P next to the first object matching SUBSTR, facing it
                {
                    var chef = SimWorld.Chef(int.Parse(t[1]));
                    if (chef == null) { Plugin.Log.LogWarning("[sim] tpnear: no chef for player " + t[1]); break; }
                    string sub = t[2].ToLowerInvariant(); float dist = t.Length > 3 ? SimWorld.F(t[3]) : 1.0f;
                    Transform target = null;
                    foreach (var tr in UnityEngine.Object.FindObjectsOfType<Transform>())
                        if (tr.gameObject.activeInHierarchy && tr.name.ToLowerInvariant().Contains(sub)) { target = tr; break; }
                    if (target == null) { Plugin.Log.LogWarning("[sim] tpnear: nothing matches '" + sub + "'"); break; }
                    Vector3 from = chef.transform.position - target.position; from.y = 0f;
                    if (from.sqrMagnitude < 0.01f) from = Vector3.back;
                    Vector3 pos = target.position + from.normalized * dist; pos.y = chef.transform.position.y;
                    SimWorld.Place(chef, pos);
                    chef.transform.rotation = Quaternion.LookRotation(new Vector3(target.position.x - pos.x, 0f, target.position.z - pos.z));
                    Plugin.Log.LogInfo("[sim] tpnear: chef " + t[1] + " -> " + pos.ToString("F1") + " facing '" + target.name + "' at " + target.position.ToString("F1"));
                    break;
                }
                case "state":   // state P : position, facing and carried item of chef P
                {
                    var chef = SimWorld.Chef(int.Parse(t[1]));
                    if (chef == null) { Plugin.Log.LogWarning("[sim] state: no chef for player " + t[1]); break; }
                    var hand = SimWorld.FindDeep(chef.transform, "Attachment");
                    string carried = hand != null && hand.childCount > 0 ? hand.GetChild(0).name : "nothing";
                    Plugin.Log.LogInfo("[sim] STATE chef " + t[1] + " pos=" + chef.transform.position.ToString("F1") + " fwd=" + chef.transform.forward.ToString("F1") + " carrying=" + carried);
                    break;
                }
                case "avatars":
                {
                    var meta = UnityEngine.Object.FindObjectOfType<MetaGameProgress>();
                    Plugin.Log.LogInfo("[sim] AVATARS unlocked=" + (meta == null ? "n/a (no MetaGameProgress)" : meta.GetUnlockedAvatars().Length.ToString()));
                    break;
                }
                case "dump":
                {
                    var all = PlayerIDProvider.s_AllProviders;
                    int active = 0;
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < all.Count; i++)
                    {
                        var c = all._items[i]; if (c == null) continue;
                        if (c.isActiveAndEnabled) active++;
                        sb.Append("\n    ").Append(c.name).Append(" id=").Append((int)c.GetID()).Append(" active=").Append(c.isActiveAndEnabled)
                          .Append(" pos=").Append(c.transform.position.ToString("F1"));
                    }
                    Plugin.Log.LogInfo("[sim] DUMP users=" + Team17.Online.ClientUserSystem.m_Users.Count + " chefProviders=" + all.Count + " activeChefs=" + active + sb);
                    break;
                }
                default: Plugin.Log.LogWarning("[sim] unknown command " + line); break;
            }
        }

        static IEnumerator Press(VirtualPad p, InputControlType b, float seconds)
        {
            p.Set(b, true); yield return new WaitForSecondsRealtime(seconds); p.Set(b, false);
        }

        static IEnumerator Move(VirtualPad p, Vector2 v, float seconds)
        {
            p.LeftStick = v; yield return new WaitForSecondsRealtime(seconds); p.LeftStick = Vector2.zero;
        }
    }
}
