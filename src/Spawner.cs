using System;
using System.Collections.Generic;

namespace SpawnMenu
{
    internal static class Spawner
    {
        internal static HumanCharacterController Player
        {
            get { try { return GameController.instance?.playerCharacter; } catch { return null; } }
        }

        internal static bool InGame => Player != null && ItemManager.instance != null;

        // Gives `count` of an item to the local player. Stackable items come in full stacks
        // (e.g. 300 rounds of ammo = as many stacks as needed); the rest come one by one.
        // Whatever doesn't fit in the inventory is dropped at the player's feet by the game.
        // Returns how many were given; spawned items are added to `made` if it isn't null.
        internal static int Give(Entry e, int count, List<ItemData> made = null)
        {
            var gc = GameController.instance;
            var im = ItemManager.instance;
            if (gc == null || im == null || Player == null) { SLog.Warn("Spawn: not in a game"); return 0; }
            if (count <= 0) return 0;

            int given = 0, pieces = 0, maxPieces = Math.Max(1, Plugin.MaxPerClick.Value);
            while (given < count && pieces < maxPieces)
            {
                int n = e.Stack > 1 ? Math.Min(e.Stack, count - given) : 1;
                var data = Make(im, e, n);
                if (data == null) break;
                bool ok;
                try { ok = gc.AddItemToPlayer(data, true); }
                catch (Exception ex) { SLog.Error($"Spawn: AddItemToPlayer failed for {e.Name} ({e.Id})", ex); break; }
                SLog.Debug($"Spawn: {e.Name} x{n} -> {(ok ? "inventory" : "dropped at feet")}");
                made?.Add(data);
                given += n;
                pieces++;
            }
            if (given < count && pieces >= maxPieces)
                SLog.Info($"Spawn: stopped at {pieces} pieces of {e.Name} (MaxPerClick)");
            SLog.Info($"Spawned {e.Name} x{given} (id {e.Id}{(e.ModGuid.Length > 0 ? ", mod " + e.ModGuid : "")})");
            return given;
        }

        static ItemData Make(ItemManager im, Entry e, int number)
        {
            ItemData d = null;
            try { d = im.GetRandomDataByAttr(e.Attr, false, false); }
            catch (Exception ex) { SLog.Debug($"Spawn: GetRandomDataByAttr failed for {e.Id}: {ex.Message}"); }
            if (d == null)
            {
                try { d = new ItemData(); d.itemId = e.Id; }
                catch (Exception ex) { SLog.Error($"Spawn: couldn't create item {e.Name} ({e.Id})", ex); return null; }
            }
            try
            {
                d.itemNumberFloat = number;
                d.durability = InventoryData.MAX_ITEM_DURABILITY;
            }
            catch { }
            return d;
        }

        // Loads each magazine from the ammo in the player's inventory, the same as the game's
        // "fill magazine" action. Logs how many rounds each one holds afterwards.
        internal static int FillMagazines(List<ItemData> mags)
        {
            var p = Player;
            if (p == null) return 0;
            int filled = 0;
            foreach (var m in mags)
            {
                try
                {
                    p.FillAmmoToMagazine(m, null);
                    int n = ItemData.GetMagazineAmmoNumber(m);
                    SLog.Info($"Fill magazines: {Catalog.NameOf(ItemManager.instance.GetItemAttrById(m.itemId))} now holds {n}");
                    if (n > 0) filled++;
                }
                catch (Exception ex) { SLog.Error("Fill magazines: failed", ex); }
            }
            return filled;
        }

        // Every magazine in the player's inventory (backpack included).
        internal static List<ItemData> MagazinesOnPlayer()
        {
            var list = new List<ItemData>();
            var seen = new HashSet<IntPtr>();
            try
            {
                var cd = Player?.characterData;
                if (cd == null) return list;
                foreach (var inv in cd.GetCarriedInventoryList())
                {
                    if (inv == null) continue;
                    foreach (var it in inv.GetAllItemsIncludeBackpack())
                    {
                        if (it == null) continue;
                        var a = ItemManager.instance.GetItemAttrById(it.itemId);
                        if (a != null && a.itemType == ItemType.Magazine && seen.Add(it.Pointer)) list.Add(it);
                    }
                }
            }
            catch (Exception ex) { SLog.Error("Couldn't list the magazines on the player", ex); }
            return list;
        }
    }
}
