using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// The web game's three fonts (ui.css): Courier Prime for typewriter text, Oswald for bold condensed
    /// caps, VT323 for the LCD readout. They live in Resources/Fonts and are turned into TextMeshPro fonts
    /// the first time they're used. Anything they don't have (arrows, symbols) falls back to TMP's default.
    /// </summary>
    public static class Fonts
    {
        static readonly Dictionary<string, TMP_FontAsset> loaded = new();

        /// <summary>Courier Prime Bold (--mono).</summary>
        public static TMP_FontAsset Mono => Load("CourierPrime-Bold");
        public static TMP_FontAsset MonoRegular => Load("CourierPrime-Regular");
        /// <summary>Oswald Bold (--cond).</summary>
        public static TMP_FontAsset Condensed => Load("Oswald-Bold");
        /// <summary>VT323 (--lcd).</summary>
        public static TMP_FontAsset Lcd => Load("VT323-Regular");

        static TMP_FontAsset Load(string name)
        {
            if (loaded.TryGetValue(name, out var asset) && asset) return asset;
            var font = Resources.Load<Font>("Fonts/" + name);
            asset = font ? TMP_FontAsset.CreateFontAsset(font) : TMP_Settings.defaultFontAsset;
            if (font)
            {
                asset.name = name;
                asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (Symbols) asset.fallbackFontAssetTable.Add(Symbols);
                asset.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
            }
            loaded[name] = asset;
            return asset;
        }

        static TMP_FontAsset symbols;
        static bool triedSymbols;

        /// <summary>
        /// A system font with arrows and circled letters (↑↓ Ⓐ ▶ ‹ ›), which the web fonts don't have.
        /// The browser borrowed them from the system too. Null where the font isn't installed.
        /// </summary>
        static TMP_FontAsset Symbols
        {
            get
            {
                if (triedSymbols) return symbols;
                triedSymbols = true;
                foreach (var family in new[] { "Arial Unicode MS", "Segoe UI Symbol", "DejaVu Sans" })
                {
                    try { symbols = TMP_FontAsset.CreateFontAsset(family, "Regular"); }
                    catch (System.Exception) { symbols = null; }
                    if (symbols) break;
                }
                return symbols;
            }
        }

        /// <summary>
        /// A copy of a font's material with a soft glow around the letters, like CSS's
        /// text-shadow: 0 0 6px color. Strength is how far the glow reaches (0..1).
        /// </summary>
        public static Material Glow(TMP_FontAsset font, Color color, float strength = 0.5f)
        {
            var m = new Material(font.material);
            m.EnableKeyword("GLOW_ON");
            m.SetColor("_GlowColor", color);
            m.SetFloat("_GlowOffset", 0f);
            m.SetFloat("_GlowInner", 0f);
            m.SetFloat("_GlowOuter", strength);
            m.SetFloat("_GlowPower", 1f);
            return m;
        }
    }
}
