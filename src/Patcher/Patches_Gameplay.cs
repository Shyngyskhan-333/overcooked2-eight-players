using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace OC2.EightPlayers
{
    /// <summary>GAMEPLAY subsystem IL patches. Runtime (Harmony) parts live in src\Plugin\Runtime_Gameplay.cs.
    /// Rationale / site list: docs\subsystem_gameplay.md.</summary>
    static class Patches_Gameplay
    {
        public static void Run(AssemblyDefinition asm)
        {
            ClampSceneVariantLookup(asm);
            InitialiseAvatarsForEight(asm);
            WrapCannonColourIndex(asm);
        }

        /// <summary>SceneDirectoryEntry.GetSceneVarient(int _playerCount): every level only has variants for 1..4 players (or -1 = any).
        /// For >4 players the exact lookup and the -1 fallback both fail and callers get null (level silently refuses to load).
        /// Prepend: if (_playerCount > 4) _playerCount = 4;  -> 5..8 players use the 4-player scene, LevelConfig and star boundaries.
        /// This is also the save-safety decision: progress is keyed by level index, never by player count (see docs).</summary>
        static void ClampSceneVariantLookup(AssemblyDefinition asm)
        {
            var m = Il.Method(asm, "SceneDirectoryData/SceneDirectoryEntry", "GetSceneVarient", 1);
            var body = m.Body; var il = body.GetILProcessor();
            var first = body.Instructions[0];
            if (body.Instructions.Any(i => i.OpCode == OpCodes.Starg_S || i.OpCode == OpCodes.Starg)) throw new Exception("GetSceneVarient already modified");
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_4));
            il.InsertBefore(first, il.Create(OpCodes.Ble, first));
            il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_4));
            il.InsertBefore(first, il.Create(OpCodes.Starg_S, m.Parameters[0]));
        }

        /// <summary>PlayerSwitchingManager.InitialiseAvatars builds one AvatarSet (+ PlayerSwitch button) per player id: loop bound 4 -> 8.
        /// Without it chefs 5..8 are never "switchable"/registered in m_avatarSets (SelectedAvatar() returns null for them).</summary>
        static void InitialiseAvatarsForEight(AssemblyDefinition asm)
        {
            var m = Il.Method(asm, "PlayerSwitchingManager", "InitialiseAvatars", 0);
            int fours = m.Body.Instructions.Count(i => Il.IsLdc(i, 4));
            if (fours == 0 && m.Body.Instructions.Any(i => Il.IsLdc(i, 8))) return; // another step already did it
            Il.ReplaceAll(m, 4, 8, 1);
        }

        /// <summary>ClientCannonCosmeticDecisions.Load: m_colours[i] where i = user index. m_colours has one entry per distinct
        /// chef colour (&lt; 8) so user index 5..8 would throw IndexOutOfRange. Rewrite to m_colours[i % m_colours.Length].</summary>
        static void WrapCannonColourIndex(AssemblyDefinition asm)
        {
            var m = Il.Method(asm, "ClientCannonCosmeticDecisions", "Load", 1);
            var ins = m.Body.Instructions; var il = m.Body.GetILProcessor();
            FieldReference colours = null; int hits = 0;
            for (int k = 0; k + 2 < ins.Count; k++)
            {
                if (ins[k].OpCode == OpCodes.Ldfld && ((FieldReference)ins[k].Operand).Name == "m_colours"
                    && (ins[k + 1].OpCode == OpCodes.Ldloc || ins[k + 1].OpCode == OpCodes.Ldloc_S || ins[k + 1].OpCode == OpCodes.Ldloc_0 || ins[k + 1].OpCode == OpCodes.Ldloc_1 || ins[k + 1].OpCode == OpCodes.Ldloc_2 || ins[k + 1].OpCode == OpCodes.Ldloc_3)
                    && ins[k + 2].OpCode == OpCodes.Ldelem_Any)
                {
                    colours = (FieldReference)ins[k].Operand;
                    var after = ins[k + 1];
                    // stack: [arr, i] -> [arr, i % arr2.Length]
                    il.InsertAfter(after, il.Create(OpCodes.Rem));
                    il.InsertAfter(after, il.Create(OpCodes.Conv_I4));
                    il.InsertAfter(after, il.Create(OpCodes.Ldlen));
                    il.InsertAfter(after, il.Create(OpCodes.Ldfld, colours));
                    il.InsertAfter(after, il.Create(OpCodes.Ldarg_0));
                    hits++; k += 6;
                }
            }
            if (hits != 1) throw new Exception("ClientCannonCosmeticDecisions::Load: expected 1 m_colours[i] site, found " + hits);
        }
    }
}
