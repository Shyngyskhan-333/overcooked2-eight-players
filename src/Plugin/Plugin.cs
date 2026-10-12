using System;
using System.Collections;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace OC2.EightPlayers
{
    [BepInPlugin("local.oc2.eightplayers", "Overcooked 2 Eight Players", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> SimPadsEnabled;
        internal static BepInEx.Logging.ManualLogSource Log;
        Harmony harmony;

        void Awake()
        {
            Instance = this; Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true, "Master switch for the 8-player runtime patches (the preloader patcher has its own switch).");
            SimPadsEnabled = Config.Bind("Debug", "SimPads", false, "TEST AID: enable scripted virtual controllers driven by BepInEx/8p-sim.txt. Leave off for normal play.");
            if (!Enabled.Value) { Logger.LogInfo("disabled by config"); return; }
            harmony = new Harmony("local.oc2.eightplayers");
            // BepInEx loads plugins inside Application..cctor, before the game's managers exist. Patching some
            // game types runs their static constructors (which then NRE and stay broken), so wait until the game is ready.
            StartCoroutine(PatchWhenReady());
        }

        IEnumerator PatchWhenReady()
        {
            float waited = 0f;
            while (true)
            {
                bool ready = false;
                try { ready = GameUtils.GetDebugConfig() != null; } catch (Exception) { }
                if (ready) break;
                waited += UnityEngine.Time.unscaledDeltaTime;
                if (waited > 120f) { Logger.LogError("game never became ready; runtime patches NOT applied"); yield break; }
                yield return null;
            }
            Logger.LogInfo("game ready after " + waited.ToString("F1") + "s, applying Harmony patches");
            int ok = 0, bad = 0;
            foreach (var t in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (!t.GetCustomAttributes(typeof(HarmonyPatch), true).Any()) continue;
                try { harmony.CreateClassProcessor(t).Patch(); ok++; }
                catch (Exception e) { bad++; Logger.LogError("Harmony patch class " + t.Name + " FAILED: " + (e.InnerException ?? e).Message); }
            }
            Logger.LogInfo("Eight-player runtime plugin: patch classes ok=" + ok + " failed=" + bad);
            SimPads.Start(this);
        }
    }
}
