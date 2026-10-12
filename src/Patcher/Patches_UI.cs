using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace OC2.EightPlayers
{
    /// <summary>UI/frontend/HUD IL patches. Only hard-coded 4s that mean "max local players" are changed here; the scene-serialised
    /// UI objects (slots, chefs, pad panels, cards) are grown at runtime by Runtime_UI*.cs. See docs\subsystem_ui.md.
    /// Each step is independent; failures are aggregated and thrown at the end so one game-update mismatch cannot hide the others.</summary>
    static class Patches_UI
    {
        const int From = 4, To = Patcher.Players;
        static readonly Func<Instruction, bool> Beq = Il.Op(Code.Beq, Code.Beq_S, Code.Bne_Un, Code.Bne_Un_S);
        static readonly Func<Instruction, bool> Blt = Il.Op(Code.Blt, Code.Blt_S);
        static readonly Func<Instruction, bool> NewArr = Il.Op(Code.Newarr);
        static readonly Func<Instruction, bool> AnyBr = Il.Op(Code.Br, Code.Br_S);

        public static void Run(AssemblyDefinition asm)
        {
            var errors = new List<string>();
            Action<string, Action> step = (name, a) =>
            {
                try { a(); Patcher.Log.LogInfo("ui: " + name); }
                catch (Exception e) { errors.Add(name + ": " + e.Message); Patcher.Log.LogError("UI FAIL " + name + ": " + e.Message); }
            };

            // ---- FrontendPlayerLobby: "lobby is full" (4) -> 8 ----
            step("FrontendPlayerLobby::OnUsersChangedEvent", () => Il.ReplaceBeforeOp(Il.Method(asm, "FrontendPlayerLobby", "OnUsersChangedEvent"), From, To, Beq, 1));
            step("FrontendPlayerLobby::FocusMenu", () => Il.ReplaceBeforeOp(Il.Method(asm, "FrontendPlayerLobby", "FocusMenu"), From, To, Beq, 1));
            step("FrontendPlayerLobby::CanAddSplitPadGuest", () => Il.ReplaceBeforeOp(Il.Method(asm, "FrontendPlayerLobby", "CanAddSplitPadGuest"), From, To, Blt, 1));
            step("FrontendPlayerLobby::ShowCorrectPrompt", () => Il.ReplaceBeforeOp(Il.Method(asm, "FrontendPlayerLobby", "ShowCorrectPrompt"), From, To, Beq, 1));
            step("FrontendPlayerLobby::UpdatePromptPlayerCount", () => Il.ReplaceBeforeOp(Il.Method(asm, "FrontendPlayerLobby", "UpdatePromptPlayerCount"), From, To, Beq, 1));
            // BuildInputConfig loop is also patched by the Input subsystem: tolerate "already 8".
            step("FrontendPlayerLobby::BuildInputConfig", () => Soft(Il.Method(asm, "FrontendPlayerLobby", "BuildInputConfig"), Blt, 1));

            // ---- voice-chat slot sentinel EngagementSlot.Count (4 -> 8) not resolved by TypedConst ----
            step("UIPlayerMenuBehaviour::set_UserInfo", () => Soft(Il.Method(asm, "UIPlayerMenuBehaviour", "set_UserInfo"), AnyBr, 1));

            // ---- pause menu: one 'Pause' ILogicalButton per user index ----
            step("PauseMenuManager::BuildStartButtons", () => Il.ReplaceBeforeOp(Il.Method(asm, "PauseMenuManager", "BuildStartButtons"), From, To, NewArr, 1));

            // ---- MultiChefUIController.m_padUI initialiser (runtime code fills slots 5-8) ----
            step("MultiChefUIController::.ctor", () => Il.ReplaceBeforeOp(Il.Method(asm, "MultiChefUIController", ".ctor"), From, To, NewArr, 1));

            // ---- FrontendChef.SetAnimationSet: the animator only knows 4 sets; wrap user index 4..7 onto 0..3 ----
            step("FrontendChef::SetAnimationSet", () =>
            {
                var m = Il.Method(asm, "FrontendChef", "SetAnimationSet");
                var il = m.Body.GetILProcessor();
                var first = m.Body.Instructions[0];
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_1));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldc_I4_3));
                il.InsertBefore(first, Instruction.Create(OpCodes.And));
                il.InsertBefore(first, Instruction.Create(OpCodes.Starg_S, m.Parameters[0]));
            });

            if (errors.Count > 0) throw new Exception("UI: " + errors.Count + " step(s) failed: " + string.Join(" | ", errors.ToArray()));
        }

        /// <summary>ReplaceBeforeOp, but accept that the site was already rewritten by another subsystem (then 0 hits).</summary>
        static void Soft(MethodDefinition m, Func<Instruction, bool> next, int expected)
        {
            var ins = m.Body.Instructions; int n = 0;
            for (int k = 0; k + 1 < ins.Count; k++)
                if (Il.IsLdc(ins[k], From) && next(ins[k + 1])) { Il.SetLdc(ins[k], To); n++; }
            if (n != expected && n != 0) throw new Exception(m.DeclaringType.Name + "::" + m.Name + ": replaced " + n + ", expected " + expected);
        }
    }
}
