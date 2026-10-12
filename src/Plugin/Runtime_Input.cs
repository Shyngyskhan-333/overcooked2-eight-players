using HarmonyLib;
using InControl;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace OC2.EightPlayers
{
    // Runtime half of the INPUT subsystem (see docs/subsystem_input.md).
    // NOTE: the plugin compiles against the UNPATCHED Assembly-CSharp, so nothing here names EngagementSlot.Five..Eight.

    /// <summary>World-map van horns for players 5..8. The patcher widened MapAvatarControlsMessage.m_bHorns to 8 and
    /// BuildHornButtons to 8 buttons, but the stock handler only plays horns 0..3 (only 4 horn audio tags exist), so
    /// players 5..8 re-use the four van horn sounds.</summary>
    [HarmonyPatch(typeof(ClientMapAvatarControls), "HandleMapAvatarMessage")]
    static class Patch_MapHorns58
    {
        static void Postfix(ClientMapAvatarControls __instance, Serialisable serialisable)
        {
            var msg = serialisable as MapAvatarControlsMessage;
            if (msg == null || msg.m_bHorns == null) return;
            for (int i = 4; i < msg.m_bHorns.Length; i++)
            {
                if (!msg.m_bHorns[i]) continue;
                GameOneShotAudioTag tag;
                switch (i % 4)
                {
                    case 0: tag = GameOneShotAudioTag.VanHorn; break;
                    case 1: tag = GameOneShotAudioTag.VanHorn02; break;
                    case 2: tag = GameOneShotAudioTag.VanHorn03; break;
                    default: tag = GameOneShotAudioTag.VanHorn04; break;
                }
                GameUtils.TriggerAudio(tag, __instance.gameObject.layer);
            }
        }
    }

    /// <summary>Diagnostics for the in-game checklist: which InControl devices does the engine see, and which pad/slot
    /// does each one get engaged to? Written to BepInEx/LogOutput.log.</summary>
    [HarmonyPatch(typeof(PCPadInputProvider), "OnDeviceAttached")]
    static class Patch_LogDeviceAttached
    {
        static void Postfix(InputDevice _device)
        {
            Plugin.Log.LogInfo("[input] device attached: '" + _device.Name + "' meta='" + _device.Meta + "' sortOrder=" + _device.SortOrder
                + " total InControl devices=" + InputManager.Devices.Count);
        }
    }

    [HarmonyPatch(typeof(PCPadInputProvider), "OnDeviceDetached")]
    static class Patch_LogDeviceDetached
    {
        static void Postfix(InputDevice _device)
        {
            Plugin.Log.LogInfo("[input] device detached: '" + _device.Name + "' sortOrder=" + _device.SortOrder
                + " total InControl devices=" + InputManager.Devices.Count);
        }
    }

    [HarmonyPatch(typeof(PCPadInputProvider), "EngagePad")]
    static class Patch_LogEngagePad
    {
        static void Postfix(ControlPadInput.PadNum _oldPadNum, ControlPadInput.PadNum _newPadNum, PlayerActionSet __result)
        {
            var sas = __result as StandardActionSet;
            string dev = sas == null ? "<none>" : (sas.Device == null ? "Keyboard" : sas.Device.Name + " (sortOrder " + sas.Device.SortOrder + ")");
            Plugin.Log.LogInfo("[input] EngagePad: unengaged PadNum " + _oldPadNum + " -> slot/PadNum " + _newPadNum + " = " + dev);
        }
    }
}
