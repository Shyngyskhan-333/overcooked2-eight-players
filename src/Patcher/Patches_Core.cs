using System.Linq;
using Mono.Cecil;

namespace OC2.EightPlayers
{
    static class Patches_Core
    {
        public static void Run(AssemblyDefinition asm)
        {
            ExtendEnum(asm, "EngagementSlot", new[] { "Five", "Six", "Seven", "Eight" });
            // EngagementSlot.Count was inlined everywhere as ldc.i4.4: retarget only the typed sentinel uses.
            var tc = new TypedConst("EngagementSlot", 4, Patcher.Players);
            tc.Run(asm.MainModule);
            Patcher.Log.LogInfo("EngagementSlot.Count sentinel: " + tc.Rewritten.Count + " sites rewritten, " + tc.Unresolved.Count + " unresolved");
            System.IO.File.WriteAllLines("BepInEx/8P-rewritten.txt", tc.Rewritten.ToArray());
            System.IO.File.WriteAllLines("BepInEx/8P-unresolved.txt", tc.Unresolved.ToArray());
        }

        // Adds members before "Count" and moves Count to Patcher.Players.
        static void ExtendEnum(AssemblyDefinition asm, string name, string[] extra)
        {
            var t = Il.Type(asm, name);
            var count = t.Fields.First(f => f.Name == "Count");
            var attrs = FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault;
            int v = (int)t.Fields.First(f => f.Name == "Four").Constant + 1;
            foreach (var e in extra) t.Fields.Insert(t.Fields.IndexOf(count), new FieldDefinition(e, attrs, t) { Constant = v++ });
            count.Constant = Patcher.Players;
        }
    }
}
