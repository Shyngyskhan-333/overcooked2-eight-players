using BepInEx.Configuration;
using HarmonyLib;

namespace OC2.EightPlayers
{
    static class UnlockConfig
    {
        static ConfigEntry<bool> unlockAllChefs;

        public static bool UnlockAllChefs
        {
            get
            {
                if (unlockAllChefs == null)
                    unlockAllChefs = Plugin.Instance.Config.Bind("Unlocks", "UnlockAllChefs", true,
                        "Show every chef avatar as unlocked (uses the game's own 'Unlock all chefs' debug flag). Display only: the save file is never changed.");
                return unlockAllChefs.Value;
            }
        }
    }

    /// <summary>MetaGameProgress.GetUnlockedAvatars (used by the lobby, chef selection cards and online avatar lists) already honours
    /// DebugManager.GetOption("Unlock all chefs"). Forcing that one flag on unlocks every chef without touching "AvatarUnlocks" in the save.
    /// The game's other filters still apply (ActuallyAllowed, platform, and the ForDlc availability check is bypassed by the flag).</summary>
    [HarmonyPatch(typeof(DebugManager), nameof(DebugManager.GetOption))]
    static class Patch_UnlockAllChefs
    {
        const string OptionName = "Unlock all chefs";

        static void Postfix(string optionName, ref bool __result)
        {
            if (optionName == OptionName && UnlockConfig.UnlockAllChefs) __result = true;
        }
    }
}
