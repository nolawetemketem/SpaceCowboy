using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>Bebop-style colors, the same as the web game's palette.ts, plus helpers for toon materials.</summary>
    public static class Palette
    {
        public static readonly Color Amber = Hex("#ff8a1f");
        public static readonly Color AmberShip = Hex("#b47133");
        public static readonly Color AmberDim = Hex("#a8521a");
        public static readonly Color Teal = Hex("#3e8f90");
        public static readonly Color TealGlow = Hex("#8fe3dc");
        public static readonly Color Ball = Hex("#f7d51d");   // emoticon yellow
        public static readonly Color Yellow = Hex("#f7d51d");
        public static readonly Color Red = Hex("#d9261c");
        public static readonly Color Blush = Hex("#e8423a");
        public static readonly Color Crt = Hex("#0b0907");    // CRT black
        public static readonly Color LEDGreen = Hex("#5fe704");  // LED phosphor

        public static readonly Color Craft = Hex("#b8b8b8");

        public static readonly Color CraftAccent = Hex("#979595");

        // Ed's gear (the floating menu screen): beige case, grey metal.
        public static readonly Color Case = Hex("#d9c7a0");
        public static readonly Color Lid = Hex("#9da3a4");
        public static readonly Color Hinge = Hex("#6d7375");

        public static readonly Color HologramBlue = Palette.Hex("#56e6ff");

        /// <summary>"#rrggbb" to a Color. Plain C#, so it's safe in field initializers.</summary>
        public static Color Hex(string hex)
        {
            int rgb = System.Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        }

        static Shader toonShader, hologramShader;

        /// <summary>A new material with the SpaceCowboy/Hologram shader: see-through light with scanlines.</summary>
        public static Material Hologram(Color color)
        {
            if (!hologramShader) hologramShader = Shader.Find("SpaceCowboy/Hologram");
            var m = new Material(hologramShader);
            m.SetColor("_HoloColor", color);
            return m;
        }

        /// <summary>A new material with the SpaceCowboy/Toon shader: flat light bands, colored shadows, ink outlines.</summary>
        public static Material Toon(Color color, Texture texture = null)
        {
            if (!toonShader) toonShader = Shader.Find("SpaceCowboy/Toon");
            var m = new Material(toonShader);
            m.SetColor("_BaseColor", color);
            if (texture) m.SetTexture("_BaseMap", texture);
            return m;
        }

        /// <summary>A flat, unlit color (screens, glowing jets). Can be see-through.</summary>
        public static Material Unlit(Color color, Texture texture = null)
        {
            var m = new Material(Shader.Find("Sprites/Default")) { color = color };
            if (texture) m.mainTexture = texture;
            return m;
        }

        /// <summary>A basic shape with a toon material and no collider (for decoration).</summary>
        public static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
