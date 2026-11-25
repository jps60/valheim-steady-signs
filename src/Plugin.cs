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
        private const string Version = "1.1.0";

        private static bool standHoldUsed;

        [HarmonyPatch(typeof(Player), "Update")]
        private static class StandHold
        {
            private static void Prefix(Player __instance)
            {
                if (__instance == Player.m_localPlayer &&
                    (ZInput.GetButtonDown("Use") || ZInput.GetButtonDown("JoyUse") ||
                     (!ZInput.GetButton("Use") && !ZInput.GetButton("JoyUse"))))
                    standHoldUsed = false;
            }
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

        private static ArmorStand GetArmorStand(Switch control)
        {
            // The equipment switches use the armor stand's UseItem callback.
            // The separate pose switch is deliberately excluded.
            ArmorStand stand = control.m_onUse?.Target as ArmorStand ?? control.GetComponentInParent<ArmorStand>();
            if (stand == null || control == stand.m_changePoseSwitch)
                return null;

            foreach (ArmorStand.ArmorStandSlot slot in stand.m_slots)
            {
                if (slot.m_switch == control)
                    return stand;
            }
            return null;
        }

        private static ItemDrop.ItemData FindItem(Humanoid character, Func<ItemDrop.ItemData, bool> canAttach)
        {
            if (character.RightItem != null && canAttach(character.RightItem))
                return character.RightItem;
            if (character.LeftItem != null && canAttach(character.LeftItem))
                return character.LeftItem;
            for (int i = 0; i < 8; ++i)
            {
                var item = character.GetInventory().GetItemAt(i, 0);
                if (item != null && canAttach(item))
                    return item;
            }
            return null;
        }

        private static string HoldBinding => ZInput.IsGamepadActive()
            ? "<b>$ui_hold $KEY_Use</b>" : "[<color=yellow><b>$ui_hold $KEY_Use</b></color>]";

        private static void AddMountHint(ref string text, ItemDrop.ItemData item)
        {
            if (item != null)
                text += Localization.instance.Localize("\n" + HoldBinding + " $piece_itemstand_attach (" + item.m_shared.m_name + ")");
        }

        [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.Interact))]
        private static class ItemStandInteraction
        {
            private static bool Prefix(ItemStand __instance, Humanoid user, bool hold, bool alt, ref bool __result)
            {
                if (user != Player.m_localPlayer || __instance.m_guardianPower != null)
                    return true;
                if (__instance.HaveAttachment())
                {
                    if (!hold || alt || !__instance.m_canBeRemoved)
                        return true;
                    if (standHoldUsed)
                        return false;
                    standHoldUsed = true;
                    return true;
                }

                if (!hold || standHoldUsed)
                    return false;
                standHoldUsed = true;
                if (__instance.m_autoAttach && __instance.m_supportedItems.Count == 1)
                    return true;
                if (PrivateArea.CheckAccess(__instance.transform.position))
                {
                    var item = FindStandItem(__instance, user);
                    if (item != null)
                        __result = __instance.UseItem(user, item);
                }
                return false;
            }
        }

        private static readonly Func<ItemStand, ItemDrop.ItemData, bool> ItemStandCanAttach =
            AccessTools.MethodDelegate<Func<ItemStand, ItemDrop.ItemData, bool>>(
                AccessTools.Method(typeof(ItemStand), "CanAttach"), virtualCall: false);

        private static ItemDrop.ItemData FindStandItem(ItemStand stand, Humanoid user) =>
            FindItem(user, item => item.m_dropPrefab != null && ItemStandCanAttach(stand, item));

        [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.GetHoverText))]
        private static class ItemStandHint
        {
            private static void Postfix(ItemStand __instance, ref string __result)
            {
                if (!Player.m_localPlayer || __instance.m_guardianPower != null || __instance.HaveAttachment())
                    return;
                if (__instance.m_autoAttach && __instance.m_supportedItems.Count == 1)
                    ReplaceUseHint(ref __result, "$piece_itemstand_attach", HoldBinding);
                else if (HasHotbarHint(__result))
                    AddMountHint(ref __result, FindStandItem(__instance, Player.m_localPlayer));
            }
        }

        private static bool HasHotbarHint(string text) => text.Contains(
            Localization.instance.Localize("\n[<color=yellow><b>$KEY_HotbarUse</b></color>] $piece_itemstand_attach"));

        private static bool HasArmorAttachment(ArmorStand stand, Switch control)
        {
            for (int i = 0; i < stand.m_slots.Count; ++i)
                if (stand.m_slots[i].m_switch == control && stand.HaveAttachment(i))
                    return true;
            return false;
        }

        private static ItemDrop.ItemData FindArmorItem(ArmorStand stand, Switch control, Humanoid user) =>
            FindItem(user, item =>
            {
                var type = item.m_shared.m_attachOverride != ItemDrop.ItemData.ItemType.None
                    ? item.m_shared.m_attachOverride : item.m_shared.m_itemType;
                for (int i = 0; i < stand.m_slots.Count; ++i)
                {
                    var slot = stand.m_slots[i];
                    if (slot.m_switch == control && (slot.m_supportedTypes.Count == 0 || slot.m_supportedTypes.Contains(type)))
                        return !stand.HaveAttachment(i) && HasArmorVisual(item);
                }
                return false;
            });

        private static bool HasArmorVisual(ItemDrop.ItemData item)
        {
            var type = item.m_shared.m_itemType;
            if (type == ItemDrop.ItemData.ItemType.Chest || type == ItemDrop.ItemData.ItemType.Legs)
                return true;
            var prefab = item.m_dropPrefab;
            if (prefab == null)
                return false;
            for (int i = 0; i < prefab.transform.childCount; ++i)
            {
                string name = prefab.transform.GetChild(i).name;
                if (name == "attach" || name == "attach_skin")
                    return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(Switch), nameof(Switch.Interact))]
        private static class ArmorInteraction
        {
            private static bool Prefix(Switch __instance, Humanoid character, ref bool hold, ref bool __result)
            {
                var stand = GetArmorStand(__instance);
                if (stand == null || character != Player.m_localPlayer)
                    return true;
                if (!hold || standHoldUsed)
                    return false;
                standHoldUsed = true;

                var item = FindArmorItem(stand, __instance, character);
                if (item != null)
                {
                    __result = __instance.UseItem(character, item);
                    return false;
                }
                if (HasArmorAttachment(stand, __instance))
                {
                    // Player.Interact has already applied the game’s hold delay.
                    hold = false;
                    return true;
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(Switch), nameof(Switch.GetHoverText))]
        private static class ArmorRemovalHint
        {
            private static void Postfix(Switch __instance, ref string __result)
            {
                var stand = GetArmorStand(__instance);
                if (stand == null)
                    return;
                if (!Player.m_localPlayer || !HasHotbarHint(__result))
                    return;
                var item = FindArmorItem(stand, __instance, Player.m_localPlayer);
                if (item == null && HasArmorAttachment(stand, __instance))
                    ReplaceUseHint(ref __result, "$piece_itemstand_take", HoldBinding);
                else
                {
                    string takeHint = Localization.instance.Localize("\n[<color=yellow><b>$KEY_Use</b></color>] $piece_itemstand_take");
                    if (__result.EndsWith(takeHint, StringComparison.Ordinal))
                        __result = __result.Substring(0, __result.Length - takeHint.Length);
                    AddMountHint(ref __result, item);
                }
            }
        }
    }
}
