using System;
using System.Collections.Generic;
using UnityEngine;
using Key = UnityEngine.InputSystem.Key;

namespace SpawnMenu
{
    // The menu window. Drawn with plain GUI boxes and labels; clicks, scrolling and typing are
    // read from the Input System in Update and hit-tested here, because the game's build strips
    // most of Unity's immediate-mode widgets (no text fields or scroll views).
    internal static class Menu
    {
        static bool _open;
        static float _closedAt = -10f;
        internal static int BlockHits;
        internal static bool BlocksGameInput => _open || Time.unscaledTime - _closedAt < 0.25f;

        static readonly string[] Tabs = { "All", "Mods", "Guns", "Ammo", "Melee", "Gear", "Food", "Material", "Other" };
        static readonly int[] Amounts = { 1, 5, 10, 50, 100, 0 };   // 0 = one full stack
        static int _tab, _amountIdx, _scroll, _sel;
        static string _search = "";
        static string _status = "";
        static float _statusUntil;
        static readonly List<Entry> _view = new List<Entry>();
        static bool _dirty = true;

        // layout (screen pixels, recomputed every frame)
        static float S;
        static Rect _win, _searchBox, _list, _kitsArea;
        static readonly List<Rect> _tabRects = new List<Rect>();
        static readonly List<Rect> _amountRects = new List<Rect>();
        static readonly List<Rect> _kitRects = new List<Rect>();
        static Rect _btnFill, _btnDump, _btnReload, _btnClose;
        static int _rows;
        static float RowH => 24f * S;

        internal static void Close()
        {
            if (_open) { _closedAt = Time.unscaledTime; SLog.Debug($"Menu closed (game input checks blocked: {BlockHits})"); }
            _open = false; BlockHits = 0;
        }

        static void Open()
        {
            if (!Spawner.InGame) { SLog.Info("Menu: load a game first"); return; }
            _open = true;
            Catalog.Refresh();
            Kits.Load();
            _dirty = true;
            SLog.Debug("Menu opened");
        }

        static void Status(string s) { _status = s; _statusUntil = Time.unscaledTime + 6f; }

        // ------------------------------------------------------------------ update (input)
        internal static void Tick()
        {
            if (In.Down(Plugin.MenuKey.Value)) { if (_open) Close(); else Open(); return; }
            if (!_open) return;
            if (!Spawner.InGame) { Close(); return; }
            Catalog.Refresh();
            if (_dirty) Rebuild();

            if (In.Down(KeyCode.Escape)) { Close(); return; }

            // typing
            bool changed = false;
            foreach (var (k, c) in In.TypeKeys)
                if (In.KeyDown(k)) { if (_search.Length < 40) { _search += c; changed = true; } }
            if (In.KeyDown(Key.Backspace) && _search.Length > 0)
            {
                _search = In.KeyHeld(Key.LeftCtrl) || In.KeyHeld(Key.RightCtrl) ? "" : _search.Substring(0, _search.Length - 1);
                changed = true;
            }
            if (changed) { _dirty = true; Rebuild(); }

            // keyboard list navigation
            if (In.KeyDown(Key.DownArrow)) Select(_sel + 1);
            if (In.KeyDown(Key.UpArrow)) Select(_sel - 1);
            if (In.KeyDown(Key.PageDown)) Select(_sel + _rows);
            if (In.KeyDown(Key.PageUp)) Select(_sel - _rows);
            if (In.KeyDown(Key.Tab)) { _tab = (_tab + (In.KeyHeld(Key.LeftShift) ? Tabs.Length - 1 : 1)) % Tabs.Length; _dirty = true; Rebuild(); }
            if (In.KeyDown(Key.Enter) || In.KeyDown(Key.NumpadEnter)) SpawnSelected();

            // mouse
            Vector2 m = In.MouseGui();
            float wheel = In.Wheel();
            if (wheel != 0f && _list.Contains(m)) Scroll(wheel > 0 ? -3 : 3);

            if (!In.Click()) return;
            for (int i = 0; i < _tabRects.Count; i++) if (_tabRects[i].Contains(m)) { _tab = i; _dirty = true; Rebuild(); return; }
            for (int i = 0; i < _amountRects.Count; i++) if (_amountRects[i].Contains(m)) { _amountIdx = i; return; }
            for (int i = 0; i < _kitRects.Count && i < Kits.List.Count; i++) if (_kitRects[i].Contains(m)) { Status(Kits.Give(Kits.List[i])); return; }
            if (_btnFill.Contains(m)) { var mags = Spawner.MagazinesOnPlayer(); int f = Spawner.FillMagazines(mags); Status($"Loaded {f} of {mags.Count} magazines from the ammo you carry"); return; }
            if (_btnDump.Contains(m)) { try { Status("Saved to " + Catalog.Dump()); } catch (Exception e) { SLog.Error("Dump failed", e); Status("Couldn't save the list, see SpawnMenu.log"); } return; }
            if (_btnReload.Contains(m)) { Kits.Load(); Catalog.Refresh(true); _dirty = true; Rebuild(); Status($"Reloaded: {Kits.List.Count} kits, {Catalog.All.Count} items"); return; }
            if (_btnClose.Contains(m)) { Close(); return; }
            if (_list.Contains(m))
            {
                int row = (int)((m.y - _list.y) / RowH);
                int idx = _scroll + row;
                if (row >= 0 && row < _rows && idx < _view.Count) { _sel = idx; SpawnSelected(); }
            }
        }

        static void Select(int i)
        {
            if (_view.Count == 0) { _sel = 0; return; }
            _sel = Mathf.Clamp(i, 0, _view.Count - 1);
            if (_sel < _scroll) _scroll = _sel;
            if (_sel >= _scroll + _rows) _scroll = _sel - _rows + 1;
        }

        static void Scroll(int d) => _scroll = Mathf.Clamp(_scroll + d, 0, Math.Max(0, _view.Count - _rows));

        static void SpawnSelected()
        {
            if (_sel < 0 || _sel >= _view.Count) return;
            var e = _view[_sel];
            int amount = Amounts[_amountIdx] == 0 ? e.Stack : Amounts[_amountIdx];
            int n = Spawner.Give(e, amount);
            Status(n > 0 ? $"Spawned {e.Name} x{n}" : $"Couldn't spawn {e.Name}, see BepInEx\\SpawnMenu.log");
        }

        static void Rebuild()
        {
            _dirty = false;
            _view.Clear();
            string q = _search.Trim().ToLowerInvariant();
            bool hidden = Plugin.ShowHiddenItems.Value;
            foreach (var e in Catalog.All)
            {
                if (e.Hidden && !hidden) continue;
                if (!InTab(e, _tab)) continue;
                if (q.Length > 0 && !e.Lower.Contains(q) && e.Id.ToString() != q && !(q.Length >= 4 && e.ModGuid.StartsWith(q))) continue;
                _view.Add(e);
            }
            _scroll = 0; _sel = 0;
        }

        static bool InTab(Entry e, int tab)
        {
            switch (Tabs[tab])
            {
                case "All": return true;
                case "Mods": return e.ModGuid.Length > 0;
                case "Guns": return e.Type == ItemType.RangedWeapon || e.Type == ItemType.GunPart;
                case "Ammo": return e.Type == ItemType.Ammo || e.Type == ItemType.Magazine || e.Type == ItemType.Arrow;
                case "Melee": return e.Type == ItemType.MeleeWeapon || e.Type == ItemType.Throwable;
                case "Gear": return e.Type == ItemType.Clothing || e.Type == ItemType.Backpack || e.Type == ItemType.FaceMask || e.Type == ItemType.Cloak;
                case "Food": return e.Type == ItemType.Food || e.Type == ItemType.Liqiud;
                case "Material": return e.Type == ItemType.Material;
                default: return e.Type == ItemType.Deployable || e.Type == ItemType.Container;
            }
        }

        // ------------------------------------------------------------------ layout + draw
        static void Layout()
        {
            S = Mathf.Clamp(Screen.height / 1080f, 0.6f, 3f) * Mathf.Clamp(Plugin.UiScale.Value, 0.5f, 3f);
            float w = 960f * S, h = 680f * S;
            w = Mathf.Min(w, Screen.width - 20f); h = Mathf.Min(h, Screen.height - 20f);
            _win = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            float pad = 10f * S, x = _win.x + pad, y = _win.y + 34f * S;
            float kitsW = 250f * S;
            float listW = _win.width - kitsW - pad * 3;

            _searchBox = new Rect(x, y, listW * 0.5f, 28f * S);
            _amountRects.Clear();
            float ax = _searchBox.xMax + pad + 60f * S, aw = (x + listW - ax) / Amounts.Length;
            for (int i = 0; i < Amounts.Length; i++) _amountRects.Add(new Rect(ax + i * aw, y, aw - 3f * S, 28f * S));
            y += 34f * S;

            _tabRects.Clear();
            float tw = listW / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++) _tabRects.Add(new Rect(x + i * tw, y, tw - 3f * S, 26f * S));
            y += 32f * S;

            float listBottom = _win.yMax - 34f * S;
            _list = new Rect(x, y, listW, listBottom - y);
            _rows = Math.Max(1, (int)(_list.height / RowH));

            float kx = x + listW + pad, ky = _win.y + 34f * S;
            _kitsArea = new Rect(kx, ky, kitsW, listBottom - ky);
            _kitRects.Clear();
            float by = ky + 28f * S;
            for (int i = 0; i < Kits.List.Count && i < 12; i++) { _kitRects.Add(new Rect(kx, by, kitsW, 30f * S)); by += 34f * S; }
            float bottom = listBottom;
            _btnClose = new Rect(kx, bottom - 30f * S, kitsW, 30f * S);
            _btnReload = new Rect(kx, bottom - 64f * S, kitsW, 30f * S);
            _btnDump = new Rect(kx, bottom - 98f * S, kitsW, 30f * S);
            _btnFill = new Rect(kx, bottom - 132f * S, kitsW, 30f * S);
        }

        internal static void Draw()
        {
            if (!_open) return;
            Layout();
            var skin = GUI.skin;
            int oldLabel = skin.label.fontSize, oldBox = skin.box.fontSize, oldButton = skin.button.fontSize;
            var oldAlign = skin.label.alignment;
            try
            {
                int fs = Mathf.RoundToInt(14f * S);
                skin.label.fontSize = fs; skin.box.fontSize = fs; skin.button.fontSize = fs;
                Vector2 m = In.MouseGui();

                GUI.Box(_win, ""); GUI.Box(_win, ""); GUI.Box(_win, "");   // stacked for a darker backing
                GUI.Label(new Rect(_win.x + 10f * S, _win.y + 6f * S, _win.width - 20f * S, 24f * S),
                    $"<b>Spawn Menu</b>   {_view.Count} items   ·   type to search, click an item to spawn it, {Plugin.MenuKey.Value} / Esc to close");

                // search + amount
                GUI.Box(_searchBox, "");
                GUI.Label(Inset(_searchBox), "Search: " + _search + ((int)(Time.unscaledTime * 2f) % 2 == 0 ? "|" : ""));
                GUI.Label(new Rect(_searchBox.xMax + 10f * S, _searchBox.y + 4f * S, 60f * S, 24f * S), "Amount");
                for (int i = 0; i < _amountRects.Count; i++)
                    Button(_amountRects[i], Amounts[i] == 0 ? "Stack" : "x" + Amounts[i], i == _amountIdx, m);

                for (int i = 0; i < _tabRects.Count; i++) Button(_tabRects[i], Tabs[i], i == _tab, m);

                // list
                GUI.Box(_list, "");
                if (_view.Count == 0)
                    GUI.Label(Inset(new Rect(_list.x, _list.y, _list.width, RowH)), Catalog.All.Count == 0 ? "No items loaded yet." : "Nothing matches.");
                for (int r = 0; r < _rows; r++)
                {
                    int idx = _scroll + r;
                    if (idx >= _view.Count) break;
                    var e = _view[idx];
                    var row = new Rect(_list.x + 2f * S, _list.y + r * RowH, _list.width - 4f * S, RowH);
                    if (idx == _sel) { GUI.Box(row, ""); GUI.Box(row, ""); }
                    else if (row.Contains(m)) GUI.Box(row, "");
                    string stack = e.Stack > 1 ? $"  (stack {e.Stack})" : "";
                    string mod = e.ModGuid.Length > 0 ? "  <color=#7fd4ff>[mod]</color>" : "";
                    string hid = e.Hidden ? "  <color=#ff9f7f>[hidden]</color>" : "";
                    GUI.Label(Inset(row), $"<color=#aaaaaa>{Catalog.TypeLabel(e.Type),-9}</color>  {e.Name}{stack}{mod}{hid}");
                    skin.label.alignment = TextAnchor.MiddleRight;
                    GUI.Label(Inset(row), $"<color=#888888>#{e.Id}</color>");
                    skin.label.alignment = oldAlign;
                }
                if (_view.Count > _rows)
                    GUI.Label(new Rect(_list.x, _list.yMax + 2f * S, _list.width, 24f * S),
                        $"<color=#aaaaaa>{_scroll + 1}-{Math.Min(_view.Count, _scroll + _rows)} of {_view.Count}   ·   mouse wheel / arrows / PgUp PgDn to scroll, Enter spawns the highlighted item, Tab switches tabs</color>");

                // kits
                GUI.Label(new Rect(_kitsArea.x, _kitsArea.y, _kitsArea.width, 24f * S), "<b>Kits</b>");
                if (Kits.List.Count == 0)
                    GUI.Label(new Rect(_kitsArea.x, _kitsArea.y + 28f * S, _kitsArea.width, 60f * S), "No kits. Add some to\nBepInEx\\config\\SpawnMenu.kits.txt");
                for (int i = 0; i < _kitRects.Count; i++) Button(_kitRects[i], Kits.List[i].Name, false, m);
                Button(_btnFill, "Fill my magazines", false, m);
                Button(_btnDump, "Save item list", false, m);
                Button(_btnReload, "Reload kits", false, m);
                Button(_btnClose, "Close", false, m);

                if (_status.Length > 0 && Time.unscaledTime < _statusUntil)
                {
                    var sr = new Rect(_win.x, _win.yMax + 4f * S, _win.width, 28f * S);
                    GUI.Box(sr, ""); GUI.Box(sr, "");
                    GUI.Label(Inset(sr), _status);
                }
            }
            finally
            {
                skin.label.fontSize = oldLabel; skin.box.fontSize = oldBox; skin.button.fontSize = oldButton;
                skin.label.alignment = oldAlign;
            }
        }

        static Rect Inset(Rect r) => new Rect(r.x + 6f * S, r.y + 2f * S, r.width - 12f * S, r.height - 4f * S);

        static void Button(Rect r, string text, bool on, Vector2 mouse)
        {
            GUI.Box(r, "");
            if (on) { GUI.Box(r, ""); GUI.Box(r, ""); }
            else if (r.Contains(mouse)) GUI.Box(r, "");
            var skin = GUI.skin;
            var old = skin.label.alignment;
            skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, on ? $"<b>{text}</b>" : text);
            skin.label.alignment = old;
        }
    }

    // Keyboard / mouse through the Input System (what the game uses), with legacy Input as fallback.
    internal static class In
    {
        internal static readonly List<(Key, char)> TypeKeys = new List<(Key, char)>();
        static bool _legacyBroken;

        static In()
        {
            for (char c = 'a'; c <= 'z'; c++) TypeKeys.Add(((Key)Enum.Parse(typeof(Key), c.ToString().ToUpperInvariant()), c));
            for (char c = '0'; c <= '9'; c++)
            {
                TypeKeys.Add(((Key)Enum.Parse(typeof(Key), "Digit" + c), c));
                TypeKeys.Add(((Key)Enum.Parse(typeof(Key), "Numpad" + c), c));
            }
            TypeKeys.Add((Key.Space, ' '));
            TypeKeys.Add((Key.Minus, '-'));
            TypeKeys.Add((Key.Period, '.'));
        }

        internal static bool KeyDown(Key k)
        {
            try { var kb = UnityEngine.InputSystem.Keyboard.current; return kb != null && kb[k].wasPressedThisFrame; }
            catch { return false; }
        }

        internal static bool KeyHeld(Key k)
        {
            try { var kb = UnityEngine.InputSystem.Keyboard.current; return kb != null && kb[k].isPressed; }
            catch { return false; }
        }

        internal static bool Down(KeyCode key)
        {
            var k = Map(key);
            if (k != Key.None && UnityEngine.InputSystem.Keyboard.current != null) return KeyDown(k);
            if (_legacyBroken) return false;
            try { return Input.GetKeyDown(key); } catch { _legacyBroken = true; return false; }
        }

        static Key Map(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return DigitKey(key - KeyCode.Alpha0);
            if (key == KeyCode.Return) return Key.Enter;
            if (key == KeyCode.BackQuote) return Key.Backquote;
            return Enum.TryParse(key.ToString(), out Key k) ? k : Key.None;
        }

        static Key DigitKey(int d) => (Key)Enum.Parse(typeof(Key), "Digit" + d);

        // Mouse position in GUI coordinates (origin top-left).
        internal static Vector2 MouseGui()
        {
            Vector2 p;
            try
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                p = mouse != null ? mouse.position.ReadValue() : (Vector2)Input.mousePosition;
            }
            catch { p = Vector2.zero; }
            return new Vector2(p.x, Screen.height - p.y);
        }

        internal static bool Click()
        {
            try
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null) return mouse.leftButton.wasPressedThisFrame;
                return Input.GetMouseButtonDown(0);
            }
            catch { return false; }
        }

        internal static float Wheel()
        {
            try
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null) return mouse.scroll.ReadValue().y;
                return Input.mouseScrollDelta.y;
            }
            catch { return 0f; }
        }
    }
}
