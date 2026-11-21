using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpawnMenu
{
    // Unity's default IMGUI font has no Chinese / Japanese / Korean glyphs, so item names in
    // those languages came out as garbage. The game ships its own UI fonts that do cover them
    // (it is translated into those languages), so the menu borrows one of those while it draws.
    // Unity's CreateDynamicFontFromOSFont is stripped from this game, so OS fonts aren't an option.
    internal static class Fonts
    {
        static Font _font;
        static string _checkedFor;   // the sample text the current choice was made for

        // The font to draw with, or null to keep Unity's default (all names are plain Latin text).
        internal static Font Current => _font != null && _font.Pointer != IntPtr.Zero ? _font : null;

        internal static void Choose(IEnumerable<string> texts)
        {
            string mode = (Plugin.MenuFont.Value ?? "auto").Trim();
            var sample = Sample(texts, 40);
            string key = mode + "|" + sample;
            if (key == _checkedFor && (_font == null || _font.Pointer != IntPtr.Zero)) return;
            _checkedFor = key;
            _font = null;

            if (mode.Equals("default", StringComparison.OrdinalIgnoreCase)) { SLog.Info("Font: Unity default (MenuFont = default)"); return; }
            bool auto = mode.Equals("auto", StringComparison.OrdinalIgnoreCase);
            if (auto && sample.Length == 0) { SLog.Debug("Font: all names are Latin, keeping Unity's default font"); return; }

            var candidates = Candidates();
            if (!auto)
            {
                _font = candidates.FirstOrDefault(f => Name(f).Equals(mode, StringComparison.OrdinalIgnoreCase))
                     ?? candidates.FirstOrDefault(f => Name(f).IndexOf(mode, StringComparison.OrdinalIgnoreCase) >= 0);
                SLog.Info(_font != null ? $"Font: using '{Name(_font)}' (MenuFont = {mode})"
                                        : $"Font: no loaded font called '{mode}'. Loaded fonts: {string.Join(", ", candidates.Select(Name))}");
                if (_font != null || sample.Length == 0) return;
            }

            Font best = null; int bestScore = 0;
            foreach (var f in candidates)
            {
                int score = Covered(f, sample);
                SLog.Debug($"Font: '{Name(f)}' covers {score}/{sample.Length} sample characters");
                if (score > bestScore) { best = f; bestScore = score; }
                if (score == sample.Length) break;
            }
            _font = best;
            SLog.Info(best != null
                ? $"Font: using the game's '{Name(best)}' for non-Latin names ({bestScore}/{sample.Length} sample characters covered)"
                : $"Font: none of the game's {candidates.Count} fonts covers '{sample}', keeping Unity's default. Loaded fonts: {string.Join(", ", candidates.Select(Name))}");
        }

        // The game's own UI fonts first (they're picked for the current language), then every loaded font.
        static List<Font> Candidates()
        {
            var list = new List<Font>();
            void Add(Func<Font> get)
            {
                try { var f = get(); if (f != null && f.Pointer != IntPtr.Zero && !list.Any(x => x.Pointer == f.Pointer)) list.Add(f); }
                catch { }
            }
            Add(() => UIFontPolicy.VectorFont);
            Add(() => UIFontPolicy.PixelFont);
            Add(() => ModEditorWindow.GetFont());
            try { foreach (var f in Resources.FindObjectsOfTypeAll<Font>()) Add(() => f); }
            catch (Exception e) { SLog.Debug("Font: couldn't list loaded fonts: " + e.Message); }
            return list;
        }

        // Up to `max` distinct characters beyond Latin / Cyrillic / Greek, which Unity's default font already has.
        static string Sample(IEnumerable<string> texts, int max)
        {
            var seen = new HashSet<char>();
            var sb = new System.Text.StringBuilder();
            foreach (var t in texts)
            {
                if (t == null) continue;
                foreach (char c in t)
                {
                    if (c < 0x0530 || char.IsWhiteSpace(c) || char.IsSurrogate(c) || (c >= 0x2000 && c <= 0x206F)) continue;
                    if (seen.Add(c)) { sb.Append(c); if (sb.Length >= max) return sb.ToString(); }
                }
            }
            return sb.ToString();
        }

        static int Covered(Font f, string sample)
        {
            int n = 0;
            try { if (f.dynamic) f.RequestCharactersInTexture(sample, 16, FontStyle.Normal); } catch { }
            foreach (char c in sample)
            {
                try { if (f.HasCharacter(c)) n++; } catch { }
            }
            return n;
        }

        static string Name(Font f) { try { return f.name ?? "?"; } catch { return "?"; } }
    }
}
