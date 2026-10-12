using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace OC2.EightPlayers
{
    /// <summary>INPUT + ENGAGEMENT subsystem. See docs/subsystem_input.md for the reasoning behind every site.
    /// Pipeline recap: InControl InputManager.Devices -> PCPadInputProvider.m_allDevices (keyboard first) -> PadNum
    /// (index among *unengaged* devices; engaged devices live at PadNum == EngagementSlot) -> PlayerManagerShared engagement
    /// slots -> User.Engagement -> GameInputConfig (Player -> PadNum) -> PlayerInputLookup logical buttons.
    /// PCPadInputProvider / PlayerInputLookup.GetSystemControllerMaximum (12) are already generic, so only the loops,
    /// arrays and literal player lists that cap at 4 need to change.</summary>
    static class Patches_Input
    {
        const int From = 4, To = Patcher.Players;
        static readonly Func<Instruction, bool> LoopBranch = Il.Op(Code.Blt, Code.Blt_S);
        static readonly Func<Instruction, bool> NewArr = Il.Op(Code.Newarr);

        public static void Run(AssemblyDefinition asm)
        {
            var errors = new List<string>();
            Action<string, Action> step = (name, a) =>
            {
                try { a(); Patcher.Log.LogInfo("input: " + name); }
                catch (Exception e) { errors.Add(name + ": " + e.Message); Patcher.Log.LogError("input FAILED " + name + ": " + e.Message); }
            };

            // ---- core engagement-slot loops (cap = number of local engagement slots) ----
            step("PCPlayerManager::Update", () =>      // disengage slots whose pad detached
                Il.ReplaceBeforeOp(Il.Method(asm, "PCPlayerManager", "Update"), From, To, LoopBranch, 1));
            step("PlayerManagerShared::GetUnengagedSlot", () =>   // first free slot (the 'return Count' sentinel was done by TypedConst)
                Il.ReplaceBeforeOp(Il.Method(asm, "PlayerManagerShared`1", "GetUnengagedSlot"), From, To, LoopBranch, 1));
            step("OvercookedEngagementController::SetUsersSticky", () =>
                Il.ReplaceBeforeOp(Il.Method(asm, "OvercookedEngagementController", "SetUsersSticky"), From, To, LoopBranch, 1));
            step("DialogueAnimationState::HasDifferentControllers", () =>
                Il.ReplaceBeforeOp(Il.Method(asm, "DialogueAnimationState", "HasDifferentControllers"), From, To, LoopBranch, 1));
            // GameUtils / FrontendPlayerLobby sites may also be touched by the Users/UI engineers: tolerate "already 8".
            step("GameUtils::GetEngagedAndInGame", () =>
                Soft(Il.Method(asm, "GameUtils", "GetEngagedAndInGame"), LoopBranch, 1));
            step("FrontendPlayerLobby::BuildInputConfig", () =>
                Soft(Il.Method(asm, "FrontendPlayerLobby", "BuildInputConfig"), LoopBranch, 1));

            // ---- GamepadEngagementManager: m_cancelButtons is indexed by (int)slot in OnEngagementChanged for ANY engagement ----
            step("GamepadEngagementManager::.ctor", () =>
                Il.ReplaceBeforeOp(Il.Method(asm, "GamepadEngagementManager", ".ctor"), From, To, NewArr, 1));
            step("GamepadEngagementManager::GatherButtons", () =>
                Il.ReplaceBeforeOp(Il.Method(asm, "GamepadEngagementManager", "GatherButtons"), From, To, LoopBranch, 1));
            // Update(): VERIFIED IN GAME that this is also the path that lets extra LOCAL pads join in the frontend lobby
            // (the lobby mode check passes there), so 'Users.Count < 4' gates the 5th player and the cancel/leave loop covers slots 0..3.
            step("GamepadEngagementManager::Update", () =>
            {
                var m = Il.Method(asm, "GamepadEngagementManager", "Update");
                Il.ReplaceBeforeOp(m, From, To, Il.Op(Code.Bge, Code.Bge_S), 1);   // ClientUserSystem.m_Users.Count < 4
                Il.ReplaceBeforeOp(m, From, To, LoopBranch, 1);                    // for (i = 0; i < 4; i++) cancel buttons
            });

            // ---- GameInputConfig fix-up: guarantees every Player has an entry and every (Player,Pad) pair is resolvable ----
            step("PadSplitManager::FixupConfigList", () =>   // i<4 (players), j<4 (pads), p<4 (UIHandedness)
                Il.ReplaceBeforeOp(Il.Method(asm, "PadSplitManager", "FixupConfigList"), From, To, LoopBranch, 3));
            // PadSplitManager::Start/AnyOpenPressed: CanOpenPadSplit() is hard-wired to 'return false' in this build -> dead, left alone.

            // ---- PlayerInputLookup.GetAnyButton/GetAnyValue: literal arrays of Player.One..Four ----
            step("PlayerInputLookup::GetAnyButton(id)", () =>
                ExtendPlayerArray(Il.Type(asm, "PlayerInputLookup").Methods.Where(x => x.Name == "GetAnyButton" && x.Parameters.Count == 1).Single()));
            step("PlayerInputLookup::GetAnyButton(id,side)", () =>
                ExtendPlayerArray(Il.Type(asm, "PlayerInputLookup").Methods.Where(x => x.Name == "GetAnyButton" && x.Parameters.Count == 2).Single()));
            step("PlayerInputLookup::GetAnyValue", () =>
                ExtendPlayerArray(Il.Method(asm, "PlayerInputLookup", "GetAnyValue")));

            // ---- Map (world map van) horn: bool[4] per player, serialised as 4 bits ----
            step("MapAvatarControlsMessage horns", () =>
            {
                var t = Il.Type(asm, "Team17.Online.Multiplayer.Messaging.MapAvatarControlsMessage");
                Il.ReplaceBeforeOp(t.Methods.First(x => x.Name == ".ctor"), From, To, NewArr, 1);
                Il.ReplaceBeforeOp(t.Methods.First(x => x.Name == "Serialise"), From, To, LoopBranch, 1);
                Il.ReplaceBeforeOp(t.Methods.First(x => x.Name == "Deserialise"), From, To, LoopBranch, 1);
            });
            step("ClientMapAvatarControls::BuildHornButtons", () =>
            {
                var m = Il.Method(asm, "ClientMapAvatarControls", "BuildHornButtons");
                Il.ReplaceBeforeOp(m, From, To, NewArr, 1);
            });

            if (errors.Count > 0) throw new Exception(errors.Count + " input sub-step(s) failed: " + string.Join(" | ", errors.ToArray()));
        }

        /// <summary>Like Il.ReplaceBeforeOp(From->To) but if the site was already rewritten by another subsystem
        /// (no 'ldc 4' + op left, and a matching 'ldc 8' + op exists) it is accepted silently.</summary>
        static void Soft(MethodDefinition m, Func<Instruction, bool> next, int expected)
        {
            var ins = m.Body.Instructions; int have4 = 0, have8 = 0;
            for (int k = 0; k + 1 < ins.Count; k++)
            {
                if (Il.IsLdc(ins[k], From) && next(ins[k + 1])) have4++;
                if (Il.IsLdc(ins[k], To) && next(ins[k + 1])) have8++;
            }
            if (have4 == 0 && have8 >= expected) { Patcher.Log.LogInfo("input: " + m.DeclaringType.Name + "::" + m.Name + " already patched elsewhere"); return; }
            Il.ReplaceBeforeOp(m, From, To, next, expected);
        }

        /// <summary>Rewrites 'new T[4]{ f(0), f(1), f(2), f(3) }' (array initialiser emitted as dup/ldc idx/.../stelem.ref
        /// blocks, with the index also passed as the Player argument) into the same for 0..7. The 4th block is cloned.</summary>
        static void ExtendPlayerArray(MethodDefinition m)
        {
            var il = m.Body.GetILProcessor();
            var ins = m.Body.Instructions;
            int na = -1;
            for (int k = 0; k + 1 < ins.Count; k++)
                if (Il.IsLdc(ins[k], From) && ins[k + 1].OpCode.Code == Code.Newarr) { if (na >= 0) throw new Exception("multiple 'ldc 4; newarr'"); na = k; }
            if (na < 0) throw new Exception(m.Name + ": no 'ldc 4; newarr'");
            // element blocks: each starts at 'dup' and ends at 'stelem.ref'
            var blocks = new List<List<Instruction>>(); List<Instruction> cur = null; int endIdx = -1;
            for (int k = na + 2; k < ins.Count; k++)
            {
                if (ins[k].OpCode.Code == Code.Dup) cur = new List<Instruction>();
                else if (cur == null) break;
                cur.Add(ins[k]);
                if (ins[k].OpCode.Code == Code.Stelem_Ref) { blocks.Add(cur); cur = null; endIdx = k; }
            }
            if (blocks.Count != 4) throw new Exception(m.Name + ": expected 4 element blocks, got " + blocks.Count);
            var last = blocks[3];
            Il.SetLdc(ins[na], To);
            Instruction anchor = ins[endIdx];
            for (int p = From; p < To; p++)
            {
                int replaced = 0;
                foreach (var src in last)
                {
                    var c = Instruction.Create(OpCodes.Nop);
                    c.OpCode = src.OpCode; c.Operand = src.Operand;
                    if (Il.IsLdc(src, 3)) { Il.SetLdc(c, p); replaced++; }
                    il.InsertAfter(anchor, c); anchor = c;
                }
                if (replaced != 2) throw new Exception(m.Name + ": expected 2 index loads per block, found " + replaced);
            }
        }
    }
}
