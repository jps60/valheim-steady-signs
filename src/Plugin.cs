using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace DeliberateInteract
{
    [BepInPlugin(Id, "Deliberate Interact", Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string Id = "com.swiftfingers.deliberateinteract";
        private const string Version = "1.0.0";

        private void Awake()
        {
            if (!UnityEngine.Application.isBatchMode)
                new Harmony(Id).PatchAll(typeof(Plugin).Assembly);
        }

        [HarmonyPatch]
        private static class EditInteraction
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Sign), nameof(Sign.Interact));
                yield return AccessTools.Method(typeof(TeleportWorld), nameof(TeleportWorld.Interact));
            }

            private static bool Prefix(bool hold, bool alt) => !hold && alt;
        }

        [HarmonyPatch(typeof(Sign), nameof(Sign.GetHoverText))]
        private static class SignHint
        {
            private static void Postfix(ref string __result) => ShowAlternateUse(ref __result, "$piece_use");
        }

        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.GetHoverText))]
        private static class PortalHint
        {
            private static void Postfix(ref string __result) => ShowAlternateUse(ref __result, "$piece_portal_settag");
        }

        private static void ShowAlternateUse(ref string text, string action)
        {
            string modifier = ZInput.IsNonClassicFunctionality() && ZInput.IsGamepadActive()
                ? "$KEY_AltKeys" : "$KEY_AltPlace";
            ReplaceUseHint(ref text, action, $"[<color=yellow><b>{modifier} + $KEY_Use</b></color>]");
        }

        private static void ReplaceUseHint(ref string text, string action, string binding)
        {
            string original = Localization.instance.Localize("\n[<color=yellow><b>$KEY_Use</b></color>] " + action);
            if (text.EndsWith(original, StringComparison.Ordinal))
                text = text.Substring(0, text.Length - original.Length)
                    + Localization.instance.Localize("\n" + binding + " " + action);
        }

        private static bool IsArmorSlot(Switch control)
        {
            // The equipment switches use the armor stand's UseItem callback.
            // The separate pose switch is deliberately excluded.
            ArmorStand stand = control.m_onUse?.Target as ArmorStand ?? control.GetComponentInParent<ArmorStand>();
            if (stand == null || control == stand.m_changePoseSwitch)
                return false;

            foreach (ArmorStand.ArmorStandSlot slot in stand.m_slots)
            {
                if (slot.m_switch == control)
                    return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(Switch), nameof(Switch.Interact))]
        private static class ArmorRemoval
        {
            private static bool Prefix(Switch __instance, ref bool hold)
            {
                if (!IsArmorSlot(__instance))
                    return true;
                if (!hold)
                    return false;

                // Player.Interact already applies the vanilla item-stand hold delay.
                // Run the normal switch callback without Switch's separate repeat timer.
                hold = false;
                return true;
            }
        }

        [HarmonyPatch(typeof(Switch), nameof(Switch.GetHoverText))]
        private static class ArmorRemovalHint
        {
            private static void Postfix(Switch __instance, ref string __result)
            {
                if (!IsArmorSlot(__instance))
                    return;
                string binding = ZInput.IsGamepadActive()
                    ? "<b>$ui_hold $KEY_Use</b>" : "[<color=yellow><b>$ui_hold $KEY_Use</b></color>]";
                ReplaceUseHint(ref __result, "$piece_itemstand_take", binding);
            }
        }
    }
}
