using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace OC2.EightPlayers
{
    /// <summary>USERS + SESSION + NETWORK MESSAGE LAYER. See docs\subsystem_users.md for the reasoning behind every site.
    /// Design rules: local play may have up to 8 users; ONLINE stays at 4 (so OnlineMultiplayerConfig.MaxPlayers,
    /// MachineID.Count, session handlers, invite/privilege tasks, Server.cs join gating are deliberately untouched).
    /// Every sub-patch is attempted even if an earlier one fails; failures are aggregated and thrown at the end.</summary>
    static class Patches_Users
    {
        const int Local = 8;                 // local player cap
        const int OldSentinel = 7;           // User.kInvalidChefColour (collides with real colour index 7)
        const int NewSentinel = 15;          // fits the widened 4-bit colour field, never a valid index

        static readonly List<string> Errors = new List<string>();
        static void Try(string name, Action a)
        {
            try { a(); }
            catch (Exception e) { Errors.Add(name + ": " + e.Message); Console.WriteLine("USERS FAIL " + name + ": " + e.Message); Patcher.Log.LogError("Users sub-patch FAILED: " + name + ": " + e.Message); }
        }

        public static void Run(AssemblyDefinition asm)
        {
            Errors.Clear();
            var blt = Il.Op(Code.Blt, Code.Blt_S);
            const string Msg = "Team17.Online.Multiplayer.Messaging.";

            // ---- ServerUserSystem: user cap + slot loops ----
            Try("ServerUserSystem.AddUser cap", () => Il.ReplaceBeforeOp(Il.Method(asm, "Team17.Online.ServerUserSystem", "AddUser"), 4, Local, blt, 1));
            Try("ServerUserSystem.ResetUsersToOfflineState loop", () => Il.ReplaceBeforeOp(Il.Method(asm, "Team17.Online.ServerUserSystem", "ResetUsersToOfflineState"), 4, Local, blt, 1));
            Try("ServerUserSystem.ResetUsersToOnlineState loop", () => Il.ReplaceBeforeOp(Il.Method(asm, "Team17.Online.ServerUserSystem", "ResetUsersToOnlineState"), 4, Local, blt, 1));
            Try("ServerUserSystem.AddNewRemoteUser cap", () => Il.ReplaceBeforeOp(Il.Method(asm, "Team17.Online.ServerUserSystem", "AddNewRemoteUser"), 4, Local, Il.Op(Code.Bne_Un, Code.Bne_Un_S), 1));

            // ---- UserSystemUtils.AtMaxUserCount: used ONLY to block "go online" when the lobby is full (online max 4).
            // Original: Count == 4. With up to 8 local users that must become Count >= 4  ->  Count > 3.
            Try("UserSystemUtils.AtMaxUserCount", () =>
            {
                var m = Il.Method(asm, "Team17.Online.UserSystemUtils", "AtMaxUserCount");
                var ins = m.Body.Instructions; int n = 0;
                for (int k = 0; k + 1 < ins.Count; k++)
                    if (Il.IsLdc(ins[k], 4) && ins[k + 1].OpCode.Code == Code.Ceq) { Il.SetLdc(ins[k], 3); ins[k + 1].OpCode = OpCodes.Cgt; n++; }
                if (n != 1) throw new Exception("AtMaxUserCount: expected 1 site, got " + n);
            });

            // ---- GameUtils.GetEngagedAndInGame: loop over engagement slots ----
            Try("GameUtils.GetEngagedAndInGame loop", () => Il.ReplaceBeforeOp(Il.Method(asm, "GameUtils", "GetEngagedAndInGame"), 4, Local, blt, 1));

            // ---- Lobby flow: per-user choice arrays sized by OnlineMultiplayerConfig.MaxPlayers (4). Local needs 8. ----
            Try("ServerLobbyFlowController.SetState m_userChoices", () => MaxPlayersToLocal(Il.Method(asm, "ServerLobbyFlowController", "SetState"), 2));
            Try("ClientLobbyFlowController.Awake m_userChoices", () => MaxPlayersToLocal(Il.Method(asm, "ClientLobbyFlowController", "Awake"), 1));
            Try("ClientLobbyFlowController.SetState m_userChoices", () => MaxPlayersToLocal(Il.Method(asm, "ClientLobbyFlowController", "SetState"), 2));
            // GameServerSessionData.m_LocalPlayerSessionIds is indexed by EngagementSlot (size 4 would overflow for slots 4..7)
            Try("GameServerSessionData..ctor", () => MaxPlayersToLocal(Il.Method(asm, "GameServerSessionData", ".ctor"), 1));

            // ---- Colour sentinel: kInvalidChefColour 7 -> 15 (7 is a real colour index for the 8th player) ----
            Try("User.kInvalidChefColour const", () =>
            {
                var f = Il.Type(asm, "Team17.Online.User").Fields.First(x => x.Name == "kInvalidChefColour");
                if (!(f.Constant is uint u) || u != OldSentinel) throw new Exception("unexpected constant " + f.Constant);
                f.Constant = (uint)NewSentinel;
            });
            Try("User..ctor default colour param + field init", () =>
            {
                var m = Il.Method(asm, "Team17.Online.User", ".ctor");
                SetUintDefault(m, "colour", OldSentinel, NewSentinel);
                Il.ReplaceAll(m, OldSentinel, NewSentinel, 1);   // m_Colour = 7u
            });
            Try("User.set_SelectedChefData", () => Il.ReplaceAll(Il.Method(asm, "Team17.Online.User", "set_SelectedChefData"), OldSentinel, NewSentinel, 1));
            Try("User.RefreshSelectedChefData", () => Il.ReplaceAll(Il.Method(asm, "Team17.Online.User", "RefreshSelectedChefData"), OldSentinel, NewSentinel, 1));
            Try("ClientUserSystem.AddUser default colour", () => SetUintDefault(Il.Method(asm, "Team17.Online.ClientUserSystem", "AddUser"), "colour", OldSentinel, NewSentinel));
            Try("LobbyFlowController..ctor team colour indices", () => Il.ReplaceAll(Il.Method(asm, "LobbyFlowController", ".ctor"), OldSentinel, NewSentinel, 3));
            Try("LobbyFlowController.GetColourIndex", () => Il.ReplaceAll(Il.Method(asm, "LobbyFlowController", "GetColourIndex"), OldSentinel, NewSentinel, 1));
            Try("CompetitiveKitchenLoaderManager.AssignChefEntities", () => Il.ReplaceAll(Il.Method(asm, "CompetitiveKitchenLoaderManager", "AssignChefEntities"), OldSentinel, NewSentinel, 2));
            Try("FrontendPlayerSlot.SetUIColourForUser", () => Il.ReplaceAll(Il.Method(asm, "FrontendPlayerSlot", "SetUIColourForUser"), OldSentinel, NewSentinel, 1));

            // ---- Wire format widths (symmetrical writer/reader, so local loopback and mod<->mod stay consistent;
            //      NOT compatible with an unmodded peer, see report) ----
            // UserData: slot 3->4 bits (Count==8 needs 4), colour 3->4 bits (sentinel 15)
            Try("UserData.Serialise", () => Il.ReplaceAll(Il.Method(asm, Msg + "UserData", "Serialise"), 3, 4, 2));
            Try("UserData.Deserialise", () => Il.ReplaceAll(Il.Method(asm, Msg + "UserData", "Deserialise"), 3, 4, 2));
            // UsersChangedMessage: user count 3 bits (max 7) -> 4 bits (need 8)
            Try("UsersChangedMessage.Serialise", () => Il.ReplaceAll(Il.Method(asm, Msg + "UsersChangedMessage", "Serialise"), 3, 4, 1));
            Try("UsersChangedMessage.Deserialise", () => Il.ReplaceAll(Il.Method(asm, Msg + "UsersChangedMessage", "Deserialise"), 3, 4, 1));
            // ChefAvatarMessage (machine 3, SLOT 3, split 3) / ControllerSettingsMessage (side 2, machine 3, SLOT 3, split 2): slot is the 2nd 3
            Try("ChefAvatarMessage.Serialise", () => Il.ReplaceNth(Il.Method(asm, "ChefAvatarMessage", "Serialise"), 3, 4, 1));
            Try("ChefAvatarMessage.Deserialise", () => Il.ReplaceNth(Il.Method(asm, "ChefAvatarMessage", "Deserialise"), 3, 4, 1));
            Try("ControllerSettingsMessage.Serialise", () => Il.ReplaceNth(Il.Method(asm, "ControllerSettingsMessage", "Serialise"), 3, 4, 1));
            Try("ControllerSettingsMessage.Deserialise", () => Il.ReplaceNth(Il.Method(asm, "ControllerSettingsMessage", "Deserialise"), 3, 4, 1));
            // JoinDataProvider.GameUserData: Slot is the 1st 3 (then ControllerSide 3, SplitStatus 3)
            Try("GameUserData.Serialize", () => Il.ReplaceNth(Il.Method(asm, "JoinDataProvider/GameUserData", "Serialize"), 3, 4, 0));
            Try("GameUserData.Deserialize", () => Il.ReplaceNth(Il.Method(asm, "JoinDataProvider/GameUserData", "Deserialize"), 3, 4, 0));
            // Lobby messages: chef index 2 bits (0..3) -> 3 bits (0..7)
            Try("LobbyServerMessage.Serialise chefIndex", () => ChefIndexBits(Il.Method(asm, "LobbyServerMessage", "Serialise"), 2));
            Try("LobbyServerMessage.Deserialise chefIndex", () => ChefIndexBits(Il.Method(asm, "LobbyServerMessage", "Deserialise"), 2));
            Try("LobbyClientMessage.Serialise chefIndex", () => ChefIndexBits(Il.Method(asm, "LobbyClientMessage", "Serialise"), 2));
            Try("LobbyClientMessage.Deserialise chefIndex", () => ChefIndexBits(Il.Method(asm, "LobbyClientMessage", "Deserialise"), 2));

            if (Errors.Count > 0) throw new Exception(Errors.Count + " Users sub-patch(es) failed: " + string.Join(" | ", Errors.ToArray()));
        }

        static bool IsMaxPlayersGetter(Instruction i)
        {
            return i.OpCode.Code == Code.Call && i.Operand is MethodReference r && r.Name == "get_MaxPlayers" && r.DeclaringType.Name == "OnlineMultiplayerConfig";
        }

        /// <summary>Replaces 'call OnlineMultiplayerConfig::get_MaxPlayers' that feeds a 'newarr' with ldc.i4 8 (uint32 on stack == int32).</summary>
        static void MaxPlayersToLocal(MethodDefinition m, int expected)
        {
            var ins = m.Body.Instructions; int n = 0;
            for (int k = 0; k + 1 < ins.Count; k++)
                if (IsMaxPlayersGetter(ins[k]) && ins[k + 1].OpCode.Code == Code.Newarr)
                { ins[k].OpCode = OpCodes.Ldc_I4_S; ins[k].Operand = (sbyte)Local; n++; }
            if (n != expected) throw new Exception(m.DeclaringType.Name + "::" + m.Name + ": MaxPlayers->newarr sites " + n + ", expected " + expected);
        }

        static void SetUintDefault(MethodDefinition m, string param, uint from, uint to)
        {
            var p = m.Parameters.FirstOrDefault(x => x.Name == param);
            if (p == null || !p.HasConstant || !(p.Constant is uint u) || u != from)
                throw new Exception(m.DeclaringType.Name + "::" + m.Name + ": param '" + param + "' default is not " + from);
            p.Constant = to;
        }

        /// <summary>Widen the 2-bit chef-index field to 3 bits. Writer: 'ldfld m_chefIndex; ldc 2; callvirt Write'.
        /// Reader: 'ldc 2; callvirt ReadUInt32; conv.i4; stfld m_chefIndex'.</summary>
        static void ChefIndexBits(MethodDefinition m, int expected)
        {
            var ins = m.Body.Instructions; int n = 0;
            for (int k = 1; k + 1 < ins.Count; k++)
            {
                if (!Il.IsLdc(ins[k], 2)) continue;
                bool write = ins[k - 1].OpCode.Code == Code.Ldfld && ((FieldReference)ins[k - 1].Operand).Name == "m_chefIndex";
                bool read = false;
                if (k + 2 < ins.Count && ins[k + 1].OpCode.Code == Code.Callvirt && ((MethodReference)ins[k + 1].Operand).Name == "ReadUInt32")
                {
                    int j = k + 2;
                    if (ins[j].OpCode.Code == Code.Conv_I4) j++;
                    read = j < ins.Count && ins[j].OpCode.Code == Code.Stfld && ((FieldReference)ins[j].Operand).Name == "m_chefIndex";
                }
                if (write || read) { Il.SetLdc(ins[k], 3); n++; }
            }
            if (n != expected) throw new Exception(m.DeclaringType.Name + "::" + m.Name + ": chefIndex width sites " + n + ", expected " + expected);
        }
    }
}
