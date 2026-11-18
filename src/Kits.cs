using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace SpawnMenu
{
    internal sealed class Kit
    {
        public string Name;
        public bool FillMags;
        public bool Helicopter;
        public bool Tank;
        public bool BattleTank;
        public int Rockets;
        public int GasCans;
        public readonly List<(string Key, int Count, int Line)> Items = new List<(string, int, int)>();
    }

    // Kits live in BepInEx\config\SpawnMenu.kits.txt so they can be edited without rebuilding.
    // The file is re-read every time the menu opens.
    internal static class Kits
    {
        internal static readonly List<Kit> List = new List<Kit>();
        internal static string FilePath => Path.Combine(Paths.ConfigPath, "SpawnMenu.kits.txt");

        const string Default =
@"# Spawn Menu kits. Edit this file while the game runs; it is re-read each time the menu opens.
#
# [Kit name]              starts a kit
# <item> <count>          one item per line. <item> is one of:
#                           - the numeric item id         e.g.  1005 20
#                           - mod:<modGuid> for mod items e.g.  mod:cfe0eb6a-4ba6-46f6-b7fc-0476d0a6cd2e 300
#                           - the item's name in quotes    e.g.  ""Bandage"" 10
# fillmags                load the kit's magazines from its ammo after spawning
# helicopter              call in a helicopter (needs the Helicopter mod)
# gascans <n>             n full cans of gasoline (needs the Helicopter or Tank mod)
# tank                    put a tank in front of you (needs the Tank mod)
# battletank              put a Battle Tank (the 3D Desert Sentinel) in front of you (needs Tank 1.3.0 or newer)
# rockets <n>             n of the game's recoilless rockets, the tank's main gun ammo (needs the Tank mod)
#
# The menu's ""Save item list"" button writes every item's id and name to BepInEx\SpawnMenu.items.txt.
# Anything after # is a comment.

[Boltgun kit]
mod:f5fc9e37-40ad-46b3-ad71-6be84c155ae4 1     # Boltgun
mod:bac356ff-83fd-47f1-a864-1a33e3c7ba5c 1     # Bolt Pistol
mod:8c0b9189-f401-48b1-8273-18bf8ed4f4ad 4     # Boltgun Magazine (30)
mod:a9462c6d-1c5f-4cfe-b1d7-87bf808df939 4     # Bolt Pistol Magazine (10)
mod:cfe0eb6a-4ba6-46f6-b7fc-0476d0a6cd2e 300   # .75 Bolt
fillmags

[Laser Gun kit]
mod:93b81951-6391-4707-beb4-0ea971889dc7 1     # Laser Rifle
mod:cef9bff0-579f-401b-a5a2-890dc6fd3ec8 4     # Laser Battery Pack

[Helicopter kit]
helicopter                                     # calls a helicopter in front of you (Helicopter mod)
gascans 2                                      # 2 full cans of gasoline (Shift+Home next to it to refuel)
102 500                                        # 5.56mm rounds for the door gun
104 500                                        # 7.62x39mm rounds for the door gun

[Tank kit]
tank                                           # puts a tank in front of you (Tank mod)
gascans 2                                      # 2 full cans of gasoline (Shift+Page Up next to it to refuel)
rockets 20                                     # main gun ammo: the game's recoilless rockets
104 500                                        # 7.62x39mm rounds for the roof machine gun

[Battle Tank kit]
battletank                                     # puts a Battle Tank (3D Desert Sentinel) in front of you (Tank mod)
gascans 2                                      # 2 full cans of gasoline (Shift+Page Up next to it to refuel)
rockets 20                                     # main gun ammo
104 500                                        # 7.62x39mm rounds for the roof machine gun
";

        // Kits added in later versions: appended to an existing kits file once if it has no kit by that name.
        static readonly (string Name, string Text)[] Added =
        {
            ("Laser Gun kit", @"
[Laser Gun kit]
mod:93b81951-6391-4707-beb4-0ea971889dc7 1     # Laser Rifle
mod:cef9bff0-579f-401b-a5a2-890dc6fd3ec8 4     # Laser Battery Pack
"),
            ("Helicopter kit", @"
[Helicopter kit]
helicopter                                     # calls a helicopter in front of you (Helicopter mod)
gascans 2                                      # 2 full cans of gasoline (Shift+Home next to it to refuel)
102 500                                        # 5.56mm rounds for the door gun
104 500                                        # 7.62x39mm rounds for the door gun
"),
            ("Tank kit", @"
[Tank kit]
tank                                           # puts a tank in front of you (Tank mod)
gascans 2                                      # 2 full cans of gasoline (Shift+Page Up next to it to refuel)
rockets 20                                     # main gun ammo: the game's recoilless rockets
104 500                                        # 7.62x39mm rounds for the roof machine gun
"),
            ("Battle Tank kit", @"
[Battle Tank kit]
battletank                                     # puts a Battle Tank (3D Desert Sentinel) in front of you (Tank mod)
gascans 2                                      # 2 full cans of gasoline (Shift+Page Up next to it to refuel)
rockets 20                                     # main gun ammo
104 500                                        # 7.62x39mm rounds for the roof machine gun
"),
        };

        internal static void EnsureFile()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    File.WriteAllText(FilePath, Default);
                    SLog.Info($"Wrote the default kits to {FilePath}");
                    return;
                }
                string text = File.ReadAllText(FilePath);
                // 1.2.1: the Helicopter kit from 1.2.0 had no fuel; add it once.
                if (text.IndexOf("[Helicopter kit]", StringComparison.OrdinalIgnoreCase) >= 0
                    && text.IndexOf("gascans", StringComparison.OrdinalIgnoreCase) < 0
                    && text.IndexOf("# removed: gascans", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    int at = text.IndexOf("[Helicopter kit]", StringComparison.OrdinalIgnoreCase);
                    int eol = text.IndexOf('\n', at);
                    string add = "gascans 2                                      # 2 full cans of gasoline (Shift+Home next to it to refuel)" + Environment.NewLine;
                    text = eol < 0 ? text + Environment.NewLine + add : text.Insert(eol + 1, add);
                    File.WriteAllText(FilePath, text);
                    SLog.Info($"Added gasoline cans to the Helicopter kit in {FilePath}");
                }
                foreach (var (name, kit) in Added)
                {
                    if (text.IndexOf("[" + name + "]", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (text.IndexOf("# removed: " + name, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    File.AppendAllText(FilePath, kit);
                    text += kit;
                    SLog.Info($"Added the '{name}' kit to {FilePath}");
                }
            }
            catch (Exception e) { SLog.Error("Couldn't write the kits file", e); }
        }

        internal static void Load()
        {
            List.Clear();
            EnsureFile();
            string[] lines;
            try { lines = File.ReadAllLines(FilePath); }
            catch (Exception e) { SLog.Error("Couldn't read the kits file", e); return; }

            Kit cur = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                int hash = IndexOfComment(l);
                if (hash >= 0) l = l.Substring(0, hash);
                l = l.Trim();
                if (l.Length == 0) continue;

                if (l.StartsWith("[") && l.EndsWith("]"))
                {
                    cur = new Kit { Name = l.Substring(1, l.Length - 2).Trim() };
                    List.Add(cur);
                    continue;
                }
                if (cur == null) { SLog.Warn($"Kits line {i + 1}: '{l}' is outside a [Kit name] section, ignored"); continue; }
                if (l.Equals("fillmags", StringComparison.OrdinalIgnoreCase)) { cur.FillMags = true; continue; }
                if (l.Equals("helicopter", StringComparison.OrdinalIgnoreCase)) { cur.Helicopter = true; continue; }
                if (l.Equals("tank", StringComparison.OrdinalIgnoreCase)) { cur.Tank = true; continue; }
                if (l.Equals("battletank", StringComparison.OrdinalIgnoreCase)) { cur.BattleTank = true; continue; }
                if (l.StartsWith("rockets", StringComparison.OrdinalIgnoreCase) && (l.Length == 7 || char.IsWhiteSpace(l[7])))
                {
                    string rest = l.Substring(7).Trim();
                    cur.Rockets = rest.Length == 0 ? 1 : (int.TryParse(rest, out int rk) ? Math.Max(0, rk) : 1);
                    continue;
                }
                if (l.StartsWith("gascans", StringComparison.OrdinalIgnoreCase))
                {
                    string rest = l.Substring(7).Trim();
                    cur.GasCans = rest.Length == 0 ? 1 : (int.TryParse(rest, out int g) ? Math.Max(0, g) : 1);
                    continue;
                }

                string key = l; int count = 1;
                int sp = l.LastIndexOfAny(new[] { ' ', '\t' });
                if (sp > 0 && int.TryParse(l.Substring(sp + 1), out int c)) { key = l.Substring(0, sp).Trim(); count = c; }
                if (key.Length >= 2 && key.StartsWith("\"") && key.EndsWith("\"")) key = key.Substring(1, key.Length - 2);
                cur.Items.Add((key, Math.Max(1, count), i + 1));
            }
            SLog.Debug($"Loaded {List.Count} kit(s) from {FilePath}");
        }

        // The Helicopter mod (girthy_mods.zedzone.helicopter) has a public
        // ZedHelicopter.HeliApi.CallIn(bool ignoreLimit). Found by reflection so Spawn Menu
        // doesn't need the Helicopter mod to load.
        static string CallHelicopter(string method, object arg) => CallMod("ZedHelicopter.HeliApi", "Helicopter", method, arg);

        // The Tank mod (girthy_mods.zedzone.tank) has a public ZedTank.TankApi, found the same way.
        static string CallTank(string method, object arg) => CallMod("ZedTank.TankApi", "Tank", method, arg);

        static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(name, false);
                if (t != null) return t;
            }
            return null;
        }

        static string CallMod(string typeName, string mod, string method, object arg)
        {
            try
            {
                Type t = FindType(typeName);
                if (t == null) return $"No {mod.ToLowerInvariant()}: the {mod} mod isn't installed";
                var m = t.GetMethod(method, new[] { arg.GetType() });
                if (m == null) return $"{mod} mod is too old for '{method}' (update it)";
                string r = m.Invoke(null, new object[] { arg }) as string ?? "";
                return r.StartsWith("!") ? r.Substring(1) : r;
            }
            catch (Exception e)
            {
                SLog.Error($"Calling the {mod} mod failed", e);
                return $"No {mod.ToLowerInvariant()}: " + e.GetBaseException().Message;
            }
        }

        // The tank's main gun fires the game's recoilless rockets; the Tank mod knows their item id.
        static string GiveRockets(int count)
        {
            Type t = FindType("ZedTank.TankApi");
            if (t == null) return "No rockets: the Tank mod isn't installed";
            int id = -1;
            try { id = (int)t.GetMethod("RocketItemId").Invoke(null, null); }
            catch (Exception e) { SLog.Error("Asking the Tank mod for the rocket id failed", e); }
            if (id < 0) return "No rockets: the Tank mod found no recoilless rockets in this game";
            var e2 = Catalog.Find(id.ToString());
            if (e2 == null) return $"No rockets: item {id} isn't in the item list";
            return Spawner.Give(e2, count, new List<ItemData>()) > 0 ? $"{count} rockets" : "No rockets: spawning failed";
        }

        // '#' starts a comment unless it is inside quotes.
        static int IndexOfComment(string l)
        {
            bool q = false;
            for (int i = 0; i < l.Length; i++)
            {
                if (l[i] == '"') q = !q;
                else if (l[i] == '#' && !q) return i;
            }
            return -1;
        }

        // Spawns a kit. Returns a short message for the menu's status line.
        internal static string Give(Kit k)
        {
            var mags = new List<ItemData>();
            int ok = 0;
            var missing = new List<string>();
            foreach (var (key, count, line) in k.Items)
            {
                var e = Catalog.Find(key);
                if (e == null) { missing.Add(key); SLog.Warn($"Kit '{k.Name}' line {line}: no item '{key}' (mod not installed or wrong id?)"); continue; }
                var made = new List<ItemData>();
                if (Spawner.Give(e, count, made) > 0) ok++;
                if (e.Type == ItemType.Magazine) mags.AddRange(made);
            }
            string msg = $"{k.Name}: spawned {ok} of {k.Items.Count} items";
            if (k.Helicopter) msg += ". " + CallHelicopter("CallIn", true);
            if (k.Tank) msg += ". " + CallTank("CallIn", true);
            if (k.BattleTank) msg += ". " + CallTank("CallInBattleTank", true);
            if (k.GasCans > 0) msg += ". " + (FindType("ZedHelicopter.HeliApi") != null || FindType("ZedTank.TankApi") == null
                ? CallHelicopter("GiveFuelCans", k.GasCans) : CallTank("GiveFuelCans", k.GasCans));
            if (k.Rockets > 0) msg += ". " + GiveRockets(k.Rockets);
            if (k.FillMags && Plugin.FillMagazines.Value && mags.Count > 0)
            {
                int f = Spawner.FillMagazines(mags);
                msg += $", loaded {f}/{mags.Count} magazines";
            }
            if (missing.Count > 0) msg += $". Missing: {string.Join(", ", missing)}";
            SLog.Info(msg);
            return msg;
        }
    }
}
