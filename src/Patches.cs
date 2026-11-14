using System;
using HarmonyLib;

namespace SpawnMenu
{
    // While the menu is open the game must not act on clicks (shooting, swinging) or keys
    // (typing "w" in the search box must not walk). The game already has these switches for
    // its own chat box and UI, so the menu just turns them on while it is open.
    internal static class Patches
    {
        internal static void Apply(Harmony h)
        {
            Postfix(h, typeof(ZedZone.Net.NetChatBar), "get_isTypingChat", nameof(True));
            Postfix(h, typeof(GenericConfirmPanel), "get_blocksCharacterInput", nameof(True));
            Postfix(h, typeof(MouseController), "IsMouseOverUI", nameof(True));
            Postfix(h, typeof(CharacterInput), "get_CanControlMouseActions", nameof(False));
        }

        static void Postfix(Harmony h, Type t, string method, string patch)
        {
            try
            {
                var target = AccessTools.Method(t, method);
                if (target == null) { SLog.Warn($"Input block: {t.Name}.{method} not found, skipped"); return; }
                h.Patch(target, postfix: new HarmonyMethod(typeof(Patches), patch));
                SLog.Debug($"Input block: patched {t.Name}.{method}");
            }
            catch (Exception e) { SLog.Error($"Input block: couldn't patch {t.Name}.{method}", e); }
        }

        static void True(ref bool __result) { if (!__result && Menu.BlocksGameInput) { __result = true; Menu.BlockHits++; } }
        static void False(ref bool __result) { if (__result && Menu.BlocksGameInput) { __result = false; Menu.BlockHits++; } }
    }
}
