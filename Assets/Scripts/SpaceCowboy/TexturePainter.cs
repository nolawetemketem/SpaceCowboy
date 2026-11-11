using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// Draws simple shapes into a texture, like the web game's &lt;canvas&gt; drawings: filled shapes,
    /// thick curved lines, ellipses. Coordinates are in pixels with y going DOWN, like a canvas,
    /// so drawings copy straight over from the web code. Edges are softened by one pixel.
    /// </summary>
    public class TexturePainter
    {
        public readonly int Width, Height;
        readonly Color[] pixels;

        public TexturePainter(int width, int height, Color background)
        {
            Width = width;
            Height = height;
            pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = background;
        }

        /// <summary>Points along a quadratic curve (an SVG "Q"), from a to b, bent toward control.</summary>
        public static List<Vector2> Curve(Vector2 a, Vector2 control, Vector2 b, int steps = 24)
        {
            var points = new List<Vector2>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float u = 1f - t;
                points.Add(u * u * a + 2f * u * t * control + t * t * b);
            }
            return points;
        }

        /// <summary>Points around an ellipse (closed: the last point repeats the first).</summary>
        public static List<Vector2> Ellipse(Vector2 centre, Vector2 radius, int steps = 64)
        {
            var points = new List<Vector2>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                points.Add(centre + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y));
            }
            return points;
        }

        /// <summary>Points around a rotated ellipse (angle in radians, like canvas's ellipse()).</summary>
        public static List<Vector2> Ellipse(Vector2 centre, Vector2 radius, float angle, int steps = 32)
        {
            var points = Ellipse(Vector2.zero, radius, steps);
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            for (int i = 0; i < points.Count; i++)
                points[i] = centre + new Vector2(points[i].x * c - points[i].y * s, points[i].x * s + points[i].y * c);
            return points;
        }

        /// <summary>A rectangle centred on a point and turned by an angle (radians), as a closed outline.</summary>
        public static List<Vector2> Box(Vector2 centre, Vector2 size, float angle = 0f)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            var points = new List<Vector2>();
            foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
            {
                Vector2 p = Vector2.Scale(corner, size * 0.5f);
                points.Add(centre + new Vector2(p.x * c - p.y * s, p.x * s + p.y * c));
            }
            return points;
        }

        /// <summary>Points as fractions of a box (0..1 across and down), like CSS clip-path: polygon(...).</summary>
        public List<Vector2> Polygon(params float[] xy)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < xy.Length; i += 2) points.Add(new Vector2(xy[i] * Width, xy[i + 1] * Height));
            return points;
        }

        /// <summary>Color every pixel from its position (u, v: 0..1 across and down).</summary>
        public void Shade(System.Func<float, float, Color> color)
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                pixels[y * Width + x] = color((x + 0.5f) / Width, (y + 0.5f) / Height);
        }

        /// <summary>The texture as a UI sprite. A border makes it 9-sliced (corners stay put when it stretches).</summary>
        public Sprite ToSprite(float border = 0f)
        {
            var tex = ToTexture();
            return Sprite.Create(tex, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        /// <summary>Fill the inside of a closed outline.</summary>
        public void Fill(List<Vector2> outline, Color color, List<Vector2> clip = null)
        {
            Bounds(outline, out int x0, out int y0, out int x1, out int y1, 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float edge = DistanceToLine(p, outline, true);
                float coverage = Inside(p, outline) ? Mathf.Clamp01(edge + 0.5f) : Mathf.Clamp01(0.5f - edge);
                if (coverage > 0f) Blend(x, y, color, coverage * Clip(p, clip));
            }
        }

        /// <summary>Draw a line of the given width along the points (round ends and corners).</summary>
        public void Stroke(List<Vector2> line, float width, Color color, List<Vector2> clip = null)
        {
            Bounds(line, out int x0, out int y0, out int x1, out int y1, Mathf.CeilToInt(width));
            float half = width * 0.5f;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float coverage = Mathf.Clamp01(half - DistanceToLine(p, line, false) + 0.5f);
                if (coverage > 0f) Blend(x, y, color, coverage * Clip(p, clip));
            }
        }

        public void FillRect(Rect rect, Color color)
        {
            for (int y = Mathf.Max(0, (int)rect.yMin); y < Mathf.Min(Height, (int)rect.yMax); y++)
            for (int x = Mathf.Max(0, (int)rect.xMin); x < Mathf.Min(Width, (int)rect.xMax); x++)
                Blend(x, y, color, 1f);
        }

        public Texture2D ToTexture(bool linear = false, FilterMode filter = FilterMode.Bilinear)
        {
            var flipped = new Color[pixels.Length]; // a texture's rows run upward, a canvas's downward
            for (int y = 0; y < Height; y++)
                System.Array.Copy(pixels, y * Width, flipped, (Height - 1 - y) * Width, Width);
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, true, linear)
            {
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 8,
            };
            tex.SetPixels(flipped);
            tex.Apply(true, true);
            return tex;
        }

        void Blend(int x, int y, Color color, float coverage)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            int i = y * Width + x;
            float a = color.a * coverage;
            Color under = pixels[i];
            float outA = a + under.a * (1f - a);
            Color mixed = outA > 0f ? (color * a + under * (under.a * (1f - a))) / outA : Color.clear;
            mixed.a = outA;
            pixels[i] = mixed;
        }

        static float Clip(Vector2 p, List<Vector2> clip)
        {
            if (clip == null) return 1f;
            float edge = DistanceToLine(p, clip, true);
            return Inside(p, clip) ? Mathf.Clamp01(edge + 0.5f) : Mathf.Clamp01(0.5f - edge);
        }

        void Bounds(List<Vector2> points, out int x0, out int y0, out int x1, out int y1, int pad)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in points)
            {
                minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y);
                maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y);
            }
            x0 = Mathf.Max(0, Mathf.FloorToInt(minX) - pad);
            y0 = Mathf.Max(0, Mathf.FloorToInt(minY) - pad);
            x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(maxX) + pad);
            y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(maxY) + pad);
        }

        static float DistanceToLine(Vector2 p, List<Vector2> points, bool closed)
        {
            float best = float.MaxValue;
            int n = points.Count;
            int segments = closed ? n : n - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % n];
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, (a + ab * t - p).sqrMagnitude);
            }
            return Mathf.Sqrt(best);
        }

        static bool Inside(Vector2 p, List<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }
    }

    /// <summary>
    /// The "grin" emoticon from the scope page: an original face in the spirit of Ed's.
    /// Its shapes are in a 100 × 100 drawing, centred on (50, 50), the same paths as the web game.
    /// </summary>
    public static class Emoticon
    {
        static readonly Color Ink = Palette.Hex("#111111");
        static readonly Color Blush = Palette.Hex("#e8423a");

        /// <summary>The whole emoticon, round face and outline included, as a sticker on a clear background.</summary>
        public static Texture2D Sticker(int size = 256)
        {
            var painter = new TexturePainter(size, size, Color.clear);
            DrawSticker(painter, new Vector2(size / 2f, size / 2f), size);
            return painter.ToTexture();
        }

        /// <summary>Draw the whole emoticon into another drawing, centred on a point, this many pixels across.</summary>
        public static void DrawSticker(TexturePainter painter, Vector2 centre, float size)
        {
            float scale = size / 100f;
            System.Func<Vector2, Vector2> map = p => centre + (p - new Vector2(50f, 50f)) * scale;
            painter.Fill(TexturePainter.Ellipse(map(new Vector2(50, 50)), Vector2.one * 42f * scale), Palette.Ball);
            painter.Stroke(TexturePainter.Ellipse(map(new Vector2(50, 50)), Vector2.one * 42f * scale), 5f * scale, Ink);
            DrawFace(painter, map, scale);
        }

        static void DrawFace(TexturePainter painter, System.Func<Vector2, Vector2> map, float scale)
        {
            List<Vector2> Q(float ax, float ay, float cx, float cy, float bx, float by) =>
                TexturePainter.Curve(map(new Vector2(ax, ay)), map(new Vector2(cx, cy)), map(new Vector2(bx, by)));

            // Eyes: two arches.
            painter.Stroke(Q(18, 43, 29, 20, 44, 46), 4.5f * scale, Ink);
            painter.Stroke(Q(56, 46, 71, 20, 82, 43), 4.5f * scale, Ink);

            // Blush under each eye.
            painter.Fill(TexturePainter.Ellipse(map(new Vector2(27f, 47.8f)), new Vector2(5.5f, 2.6f) * scale), Blush);
            painter.Fill(TexturePainter.Ellipse(map(new Vector2(73.5f, 47.8f)), new Vector2(5.5f, 2.6f) * scale), Blush);

            // The grin: M21.7 50.8 Q50 64 80.3 50.3 Q74 79 50 78.6 Q27 78 21.7 50.8 Z
            var mouth = Q(21.7f, 50.8f, 50, 64, 80.3f, 50.3f);
            mouth.AddRange(Q(80.3f, 50.3f, 74, 79, 50, 78.6f));
            mouth.AddRange(Q(50, 78.6f, 27, 78, 21.7f, 50.8f));
            painter.Fill(mouth, Color.white);
            foreach (float x in new[] { 31f, 40.5f, 50f, 59.5f, 69f }) // teeth, clipped to the mouth
                painter.Stroke(new List<Vector2> { map(new Vector2(x, 50)), map(new Vector2(x, 82)) }, 2.6f * scale, Ink, mouth);
            mouth.Add(mouth[0]);
            painter.Stroke(mouth, 4f * scale, Ink);
        }
    }
}
