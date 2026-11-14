using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace SpawnMenu
{
    internal sealed class Entry
    {
        public ItemAttr Attr;
        public int Id;
        public string Name;
        public string Lower;
        public ItemType Type;
        public string ModGuid;   // "" for the game's own items
        public int Stack;
        public bool Hidden;
    }

    // Every item the game knows (its own and every loaded mod's), read from ItemManager.itemList.
    internal static class Catalog
    {
        internal static readonly List<Entry> All = new List<Entry>();
        static int _builtFrom = -1;
        static int _builtVersion = -1;

        internal static bool Ready => ItemManager.instance != null;

        internal static void Refresh(bool force = false)
        {
            var im = ItemManager.instance;
            if (im == null || im.itemList == null) return;
            int count = im.itemList.Count;
            int ver = 0;
            try { ver = ItemManager.registryVersion; } catch { }
            if (!force && count == _builtFrom && ver == _builtVersion) return;

            All.Clear();
            foreach (var a in im.itemList)
            {
                if (a == null) continue;
                try
                {
                    if (a.isMissingMod) continue;
                    var e = new Entry
                    {
                        Attr = a,
                        Id = a.itemId,
                        Name = NameOf(a),
                        Type = a.itemType,
                        ModGuid = a.modGuid ?? "",
                        Stack = Math.Max(1, a.stackNumber),
                        Hidden = a.hiddenItem,
                    };
                    e.Lower = e.Name.ToLowerInvariant();
                    All.Add(e);
                }
                catch (Exception ex) { SLog.Debug($"Skipped an item while listing: {ex.Message}"); }
            }
            All.Sort((x, y) =>
            {
                int c = TypeRank(x.Type).CompareTo(TypeRank(y.Type));
                return c != 0 ? c : string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
            });
            _builtFrom = count;
            _builtVersion = ver;
            SLog.Info($"Item list built: {All.Count} items ({All.Count(e => e.ModGuid.Length > 0)} from mods, {All.Count(e => e.Hidden)} hidden).");
        }

        internal static string NameOf(ItemAttr a)
        {
            string n = null;
            try { n = a.ItemName; } catch { }
            if (string.IsNullOrWhiteSpace(n)) try { n = a.itemName; } catch { }
            if (string.IsNullOrWhiteSpace(n)) n = "Item " + a.itemId;
            return n.Replace("\n", " ").Trim();
        }

        internal static int TypeRank(ItemType t) => t switch
        {
            ItemType.RangedWeapon => 0, ItemType.Magazine => 1, ItemType.Ammo => 2, ItemType.Arrow => 3,
            ItemType.GunPart => 4, ItemType.MeleeWeapon => 5, ItemType.Throwable => 6,
            ItemType.Clothing => 7, ItemType.Backpack => 8, ItemType.FaceMask => 9, ItemType.Cloak => 10,
            ItemType.Food => 11, ItemType.Liqiud => 12, ItemType.Material => 13,
            ItemType.Deployable => 14, ItemType.Container => 15, _ => 16
        };

        internal static string TypeLabel(ItemType t) => t switch
        {
            ItemType.RangedWeapon => "Gun", ItemType.MeleeWeapon => "Melee", ItemType.FaceMask => "Mask",
            ItemType.GunPart => "Gun part", ItemType.Liqiud => "Liquid", _ => t.ToString()
        };

        // Finds an item for a kit line: a number (item id), mod:<guid> or the item's name.
        internal static Entry Find(string key)
        {
            Refresh();
            key = key.Trim();
            if (key.StartsWith("mod:", StringComparison.OrdinalIgnoreCase))
            {
                string g = key.Substring(4).Trim();
                return All.FirstOrDefault(e => e.ModGuid.Equals(g, StringComparison.OrdinalIgnoreCase));
            }
            if (int.TryParse(key, out int id))
            {
                var hit = All.FirstOrDefault(e => e.Id == id);
                if (hit != null) return hit;
                try
                {
                    var a = ItemManager.instance?.FindItemAttrById(id);
                    if (a != null) return new Entry { Attr = a, Id = id, Name = NameOf(a), Type = a.itemType, ModGuid = a.modGuid ?? "", Stack = Math.Max(1, a.stackNumber) };
                }
                catch { }
                return null;
            }
            return All.FirstOrDefault(e => e.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                ?? All.FirstOrDefault(e => (e.Attr.itemName ?? "").Equals(key, StringComparison.OrdinalIgnoreCase));
        }

        // Writes every item to BepInEx\SpawnMenu.items.txt, to look up ids for kits.
        internal static string Dump()
        {
            Refresh(true);
            string path = Path.Combine(Paths.BepInExRootPath, "SpawnMenu.items.txt");
            using (var w = new StreamWriter(path, false))
            {
                w.WriteLine("# Every item in the game right now. Use the id (or mod:<guid> for mod items) in BepInEx\\config\\SpawnMenu.kits.txt");
                w.WriteLine("# id\ttype\tstack\tname\tmod guid");
                foreach (var e in All)
                    w.WriteLine($"{e.Id}\t{TypeLabel(e.Type)}\t{e.Stack}\t{e.Name}{(e.Hidden ? " (hidden)" : "")}\t{e.ModGuid}");
            }
            SLog.Info($"Wrote {All.Count} items to {path}");
            return path;
        }
    }
}
