using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace DeliberateInteract
{
    [BepInPlugin(Id, "Deliberate Interact", Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string Id = "com.swiftfingers.deliberateinteract";
        private const string Version = "1.1.1";

        private const float MountHoldSeconds = 0.2f;
        private static PendingMount pendingMount;

        private static readonly Func<Player, bool> CanTakeInput = AccessTools.MethodDelegate<Func<Player, bool>>(
            AccessTools.Method(typeof(Player), "TakeInput"), virtualCall: false);
        private static readonly Action<Humanoid, GameObject> AnimateUse = AccessTools.MethodDelegate<Action<Humanoid, GameObject>>(
            AccessTools.Method(typeof(Humanoid), "DoInteractAnimation"));

        private sealed class PendingMount
        {
            public Player Player;
            public MonoBehaviour Stand;
            public ItemDrop.ItemData Item;
            public int Index;
            public string Button;
            public float StartedAt;
        }

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

        private static MonoBehaviour GetMountTarget(GameObject hovered)
        {
            if (!hovered || hovered.GetComponentInParent<Hoverable>() == null)
                return null;
            var target = hovered.GetComponentInParent<Interactable>();
            if (target is ItemStand itemStand && itemStand.m_guardianPower == null)
                return itemStand;
            return target is Switch control && IsArmorSlot(control) ? control : null;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UseHotbarItem))]
        private static class StartMountHold
        {
            private static bool Prefix(Player __instance, int index)
            {
                if (__instance != Player.m_localPlayer || index < 1 || index > 8)
                    return true;
                string button = $"Hotbar{index}";
                if (!ZInput.GetButtonDown(button))
                {
                    button += "Alt";
                    if (!ZInput.GetButtonDown(button))
                        return true;
                }
                var stand = GetMountTarget(__instance.GetHoverObject());
                if (!stand)
                    return true;
                var item = __instance.GetInventory().GetItemAt(index - 1, 0);
                pendingMount = item == null ? null : new PendingMount
                {
                    Player = __instance,
                    Stand = stand,
                    Item = item,
                    Index = index,
                    Button = button,
                    StartedAt = Time.unscaledTime
                };
                return false;
            }
        }

        [HarmonyPatch(typeof(Player), "Update")]
        private static class FinishMountHold
        {
            private static void Postfix(Player __instance)
            {
                var mount = pendingMount;
                if (mount == null || __instance != Player.m_localPlayer)
                    return;
                var hovered = __instance.GetHoverObject();
                if (mount.Player != __instance || !mount.Stand || !ZInput.GetButton(mount.Button) ||
                    !CanTakeInput(__instance) || Hud.InRadial() ||
                    GetMountTarget(hovered) != mount.Stand ||
                    __instance.GetInventory().GetItemAt(mount.Index - 1, 0) != mount.Item)
                {
                    pendingMount = null;
                    return;
                }
                if (Time.unscaledTime - mount.StartedAt < MountHoldSeconds)
                    return;

                pendingMount = null;
                if (mount.Stand is ItemStand && !PrivateArea.CheckAccess(mount.Stand.transform.position))
                    return;
                if (((Interactable)mount.Stand).UseItem(__instance, mount.Item))
                    AnimateUse(__instance, hovered);
            }
        }

        private static void ShowHoldHotbar(ref string text)
        {
            if (ZInput.IsGamepadActive())
                return;
            const string hint = "[<color=yellow><b>$KEY_HotbarUse</b></color>] $piece_itemstand_attach";
            text = text.Replace(Localization.instance.Localize(hint),
                Localization.instance.Localize(hint.Replace("$KEY_HotbarUse", "$ui_hold $KEY_HotbarUse")));
        }

        [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.GetHoverText))]
        private static class ItemStandMountHint
        {
            private static void Postfix(ItemStand __instance, ref string __result)
            {
                if (__instance.m_guardianPower == null)
                    ShowHoldHotbar(ref __result);
            }
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
                ShowHoldHotbar(ref __result);
                string binding = ZInput.IsGamepadActive()
                    ? "<b>$ui_hold $KEY_Use</b>" : "[<color=yellow><b>$ui_hold $KEY_Use</b></color>]";
                ReplaceUseHint(ref __result, "$piece_itemstand_take", binding);
            }
        }
    }
}
