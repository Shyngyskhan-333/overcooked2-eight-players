using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Mono.Cecil;

namespace OC2.EightPlayers
{
    /// <summary>BepInEx preloader patcher: rewrites Assembly-CSharp IN MEMORY (nothing on disk changes).
    /// Each subsystem lives in its own Patches_*.cs and registers a step in <see cref="Steps"/>.</summary>
    public static class Patcher
    {
        public const int Players = 8;
        public static readonly List<string> Failures = new List<string>();
        internal static ManualLogSource Log = Logger.CreateLogSource("OC2-8P-Patcher");
        public static IEnumerable<string> TargetDLLs { get { yield return "Assembly-CSharp.dll"; } }

        static readonly List<KeyValuePair<string, Action<AssemblyDefinition>>> Steps = new List<KeyValuePair<string, Action<AssemblyDefinition>>>
        {
            Step("Core", Patches_Core.Run),
            Step("Users", Patches_Users.Run),
            Step("Input", Patches_Input.Run),
            Step("Gameplay", Patches_Gameplay.Run),
            Step("UI", Patches_UI.Run),
        };
        static KeyValuePair<string, Action<AssemblyDefinition>> Step(string n, Action<AssemblyDefinition> a) { return new KeyValuePair<string, Action<AssemblyDefinition>>(n, a); }

        public static void Patch(AssemblyDefinition asm)
        {
            Log.LogInfo("Patching Assembly-CSharp for " + Players + " players");
            int ok = 0, bad = 0; Failures.Clear();
            foreach (var s in Steps)
            {
                try { s.Value(asm); ok++; Log.LogInfo("step OK: " + s.Key); }
                catch (Exception e) { bad++; Failures.Add(s.Key + ": " + e.Message); Log.LogError("step FAILED: " + s.Key + ": " + e.Message); }
            }
            Log.LogInfo($"patch steps ok={ok} failed={bad}");
        }
    }
}
