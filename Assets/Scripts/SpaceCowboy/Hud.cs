using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceCowboy
{
    /// <summary>
    /// The on-screen overlay during play, in Radical Ed's style (ported from Hud.ts and ui.css):
    /// a green LED scoreboard at the top, and a "coming soon" slot for Ed's special. Hidden on the menu.
    /// Sizes are the web page's CSS pixels, on a 1280 × 720 reference screen.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        // ui.css tokens
        static readonly Color GreenGhost = new Color(0.224f, 0.824f, 0.42f, 0.08f);
        static readonly Color GreenGlow = new Color(0.224f, 0.824f, 0.42f, 0.4f);

        LedNumber a, b;
        GameObject root;

        public static Hud Create()
        {
            var go = new GameObject("HUD");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            return go.AddComponent<Hud>();
        }

        void Awake()
        {
            root = UI.Rect("Overlay", transform, Vector2.zero, Vector2.one).gameObject;
            BuildScoreboard();
            BuildSpecialSlot();
            Show(false);
        }

        /// <summary>Scoreboard: top centre. Two LED numbers with a dash between them, nothing else.</summary>
        void BuildScoreboard()
        {
            // padding 8px 14px, gap 14px: | 14 | 49 | 14 | 14 | 14 | 49 | 14 | = 168 × 56
            var board = UI.Rect("Scoreboard", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            board.pivot = new Vector2(0.5f, 1f);
            board.anchoredPosition = new Vector2(0f, -14f);
            board.sizeDelta = new Vector2(168f, 56f);
            UI.Box(board, new Color(0.027f, 0.039f, 0.027f, 0.85f), 4f, new Color(0.224f, 0.824f, 0.42f, 0.3f), 1f);

            a = new LedNumber(board, new Vector2(-45.5f, 0f));
            b = new LedNumber(board, new Vector2(45.5f, 0f));
            // The dash between the two scores: one always-lit segment.
            var dash = UI.Rect("Dash", board, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            dash.sizeDelta = new Vector2(14f, 5f);
            var dashImage = dash.gameObject.AddComponent<Image>();
            dashImage.sprite = Segments.Horizontal;
            dashImage.color = Palette.Green;
            Glow(dashImage);
        }

        /// <summary>Special slot (bottom right): a placeholder for Ed's special and ult.</summary>
        void BuildSpecialSlot()
        {
            var slot = UI.Rect("Special Slot", root.transform, new Vector2(1f, 0f), new Vector2(1f, 0f));
            slot.pivot = new Vector2(1f, 0f);
            slot.anchoredPosition = new Vector2(-18f, 18f);

            var label = UI.Text("Label", slot, $"SPECIAL <color=#{ColorUtility.ToHtmlStringRGB(Palette.Amber)}>//</color> COMING SOON",
                13f, Palette.Blush);
            label.font = Fonts.Mono;
            label.characterSpacing = 8f; // letter-spacing: 0.08em
            label.alignment = TextAlignmentOptions.Center;
            label.ForceMeshUpdate();
            // padding 8px 14px, 2px border
            slot.sizeDelta = new Vector2(label.preferredWidth + 28f + 4f, label.preferredHeight + 16f + 4f);
            UI.Box(slot, new Color(0.043f, 0.035f, 0.027f, 0.7f), 6f, Color.clear, 0f);
            UI.DashedBorder(slot, Palette.Blush, 2f, 6f, 4f);
            label.transform.SetAsLastSibling();
        }

        public void Show(bool visible) => root.SetActive(visible);

        /// <summary>Left = goal A's count, right = goal B's.</summary>
        public void SetScore(int scoreA, int scoreB)
        {
            a.Show(scoreA);
            b.Show(scoreB);
        }

        void Update()
        {
            a.Fade(Time.unscaledDeltaTime);
            b.Fade(Time.unscaledDeltaTime);
        }

        /// <summary>The LEDs' soft green halo (filter: drop-shadow(0 0 4px green)).</summary>
        static Outline Glow(Graphic graphic)
        {
            var glow = graphic.gameObject.AddComponent<Outline>();
            glow.effectColor = GreenGlow;
            glow.effectDistance = new Vector2(1.5f, 1.5f);
            glow.useGraphicAlpha = true;
            return glow;
        }

        /// <summary>The two segment shapes, with pointed ends (ui.css's clip-path polygons).</summary>
        static class Segments
        {
            static Sprite horizontal, vertical;

            /// <summary>a, g, d: polygon(20% 0, 80% 0, 100% 50%, 80% 100%, 20% 100%, 0 50%).</summary>
            public static Sprite Horizontal => horizontal ? horizontal : horizontal = Make(64, 20, 0.2f, 0f, 0.8f, 0f, 1f, 0.5f, 0.8f, 1f, 0.2f, 1f, 0f, 0.5f);
            /// <summary>b, c, e, f: polygon(50% 0, 100% 12%, 100% 88%, 50% 100%, 0 88%, 0 12%).</summary>
            public static Sprite Vertical => vertical ? vertical : vertical = Make(20, 68, 0.5f, 0f, 1f, 0.12f, 1f, 0.88f, 0.5f, 1f, 0f, 0.88f, 0f, 0.12f);

            static Sprite Make(int w, int h, params float[] polygon)
            {
                var p = new TexturePainter(w, h, Color.clear);
                p.Fill(p.Polygon(polygon), Color.white);
                return p.ToSprite();
            }
        }

        /// <summary>A two-digit LED number; the tens digit stays dark until it's needed. LED panels lean slightly.</summary>
        class LedNumber
        {
            readonly LedDigit tens, ones;

            public LedNumber(Transform parent, Vector2 centre)
            {
                // Two 22px digits, 5px apart.
                tens = new LedDigit(parent, centre + new Vector2(-13.5f, 0f));
                ones = new LedDigit(parent, centre + new Vector2(13.5f, 0f));
            }

            public void Show(int n)
            {
                string text = Mathf.Min(n, 99).ToString().PadLeft(2);
                tens.Show(text[0]);
                ones.Show(text[1]);
            }

            public void Fade(float dt)
            {
                tens.Fade(dt);
                ones.Fade(dt);
            }
        }

        /// <summary>
        /// One seven-segment LED digit, 22 × 40. Which segments are lit for each digit:
        ///    aaa
        ///   f   b
        ///    ggg
        ///   e   c
        ///    ddd
        /// Switching off fades out quickly; switching on waits a beat, then fades in.
        /// </summary>
        class LedDigit
        {
            const string Names = "abcdefg";
            static readonly string[] Lit = { "abcdef", "bc", "abdeg", "abcdg", "bcfg", "acdfg", "acdefg", "abc", "abcdefg", "abcdfg" };
            const float W = 22f, H = 40f, T = 5f;
            static readonly float Lean = Mathf.Tan(6f * Mathf.Deg2Rad); // transform: skewX(-6deg)

            readonly Image[] segs = new Image[7];
            readonly Outline[] glows = new Outline[7];
            readonly bool[] on = new bool[7];
            readonly float[] level = new float[7]; // 0 = off, 1 = on
            readonly float[] wait = new float[7];

            public LedDigit(Transform parent, Vector2 centre)
            {
                // Each segment's box in the digit, from the top-left: x, y, width, height (ui.css).
                Rect[] boxes =
                {
                    new Rect(3f, 0f, W - 6f, T),                      // a: top
                    new Rect(W - T, 2f, T, H / 2f - 3f),              // b: upper right
                    new Rect(W - T, H / 2f + 1f, T, H / 2f - 3f),     // c: lower right
                    new Rect(3f, H - T, W - 6f, T),                   // d: bottom
                    new Rect(0f, H / 2f + 1f, T, H / 2f - 3f),        // e: lower left
                    new Rect(0f, 2f, T, H / 2f - 3f),                 // f: upper left
                    new Rect(3f, H / 2f - T / 2f, W - 6f, T),         // g: middle
                };
                for (int i = 0; i < 7; i++)
                {
                    var box = boxes[i];
                    var seg = UI.Rect($"Segment {Names[i]}", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                    seg.sizeDelta = box.size;
                    // Centre of the box, relative to the digit's centre (y up), leaning right toward the top.
                    float y = H / 2f - box.y - box.height / 2f;
                    seg.anchoredPosition = centre + new Vector2(-W / 2f + box.x + box.width / 2f + y * Lean, y);
                    segs[i] = seg.gameObject.AddComponent<Image>();
                    segs[i].sprite = box.width > box.height ? Segments.Horizontal : Segments.Vertical;
                    segs[i].color = GreenGhost;
                    glows[i] = Glow(segs[i]);
                    glows[i].enabled = false;
                }
            }

            public void Show(char digit)
            {
                string lit = digit >= '0' && digit <= '9' ? Lit[digit - '0'] : "";
                for (int i = 0; i < 7; i++)
                {
                    bool now = lit.IndexOf(Names[i]) >= 0;
                    if (now && !on[i]) wait[i] = 0.12f; // transition: ... 0.18s ease-out 0.12s
                    on[i] = now;
                }
            }

            public void Fade(float dt)
            {
                for (int i = 0; i < 7; i++)
                {
                    if (on[i] && wait[i] > 0f) { wait[i] -= dt; continue; }
                    level[i] = Mathf.MoveTowards(level[i], on[i] ? 1f : 0f, dt / (on[i] ? 0.18f : 0.15f));
                    segs[i].color = Color.Lerp(GreenGhost, Palette.Green, level[i]);
                    glows[i].enabled = level[i] > 0.5f;
                }
            }
        }
    }

    /// <summary>Small helpers for building uGUI from code.</summary>
    public static class UI
    {
        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static readonly System.Collections.Generic.Dictionary<int, Sprite> rounded = new();

        /// <summary>A rounded rectangle (white, tinted by the Image), 9-sliced so it stretches to any size.
        /// Drawn at twice the size for crisp corners (hence pixelsPerUnitMultiplier = 2 where it's used).</summary>
        public static Sprite Rounded(float radius)
        {
            int r = Mathf.Max(1, Mathf.RoundToInt(radius * 2f));
            if (rounded.TryGetValue(r, out var sprite) && sprite) return sprite;
            int size = r * 2 + 4;
            var p = new TexturePainter(size, size, Color.clear);
            p.Fill(RoundedOutline(new UnityEngine.Rect(0, 0, size, size), r), Color.white);
            sprite = p.ToSprite(r + 1);
            rounded[r] = sprite;
            return sprite;
        }

        static System.Collections.Generic.List<Vector2> RoundedOutline(UnityEngine.Rect box, float r)
        {
            var points = new System.Collections.Generic.List<Vector2>();
            Vector2[] corners = { new(box.xMax - r, box.yMin + r), new(box.xMax - r, box.yMax - r), new(box.xMin + r, box.yMax - r), new(box.xMin + r, box.yMin + r) };
            for (int c = 0; c < 4; c++)
            for (int i = 0; i <= 6; i++)
            {
                float a = (c - 1 + i / 6f) * Mathf.PI / 2f;
                points.Add(corners[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return points;
        }

        /// <summary>A filled box with rounded corners and an optional solid border.</summary>
        public static void Box(RectTransform rect, Color fill, float radius, Color border, float borderWidth)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Rounded(radius);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            image.color = fill;
            if (borderWidth <= 0f) return;
            // The border: four thin strips along the edges.
            foreach (var (min, max, size, pos) in new[]
            {
                (new Vector2(0, 1), new Vector2(1, 1), new Vector2(-2 * radius, borderWidth), new Vector2(0, -borderWidth / 2f)),
                (new Vector2(0, 0), new Vector2(1, 0), new Vector2(-2 * radius, borderWidth), new Vector2(0, borderWidth / 2f)),
                (new Vector2(0, 0), new Vector2(0, 1), new Vector2(borderWidth, -2 * radius), new Vector2(borderWidth / 2f, 0)),
                (new Vector2(1, 0), new Vector2(1, 1), new Vector2(borderWidth, -2 * radius), new Vector2(-borderWidth / 2f, 0)),
            })
            {
                var strip = Rect("Border", rect, min, max);
                strip.sizeDelta = size;
                strip.anchoredPosition = pos;
                var img = strip.gameObject.AddComponent<Image>();
                img.color = border;
                img.raycastTarget = false;
            }
        }

        /// <summary>A dashed border (border: 2px dashed), drawn as short strips along each edge.</summary>
        public static void DashedBorder(RectTransform rect, Color color, float width, float dash, float gap)
        {
            Vector2 size = rect.sizeDelta;
            void Edge(Vector2 from, Vector2 direction, float length, bool horizontal)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt((length + gap) / (dash + gap)));
                float step = (length - dash) / Mathf.Max(1, count - 1);
                for (int i = 0; i < count; i++)
                {
                    var d = Rect("Dash", rect, Vector2.zero, Vector2.zero);
                    d.pivot = new Vector2(0f, 0f);
                    d.sizeDelta = horizontal ? new Vector2(dash, width) : new Vector2(width, dash);
                    d.anchoredPosition = from + direction * (i * step);
                    var img = d.gameObject.AddComponent<Image>();
                    img.color = color;
                    img.raycastTarget = false;
                }
            }
            Edge(new Vector2(4f, 0f), Vector2.right, size.x - 8f, true);
            Edge(new Vector2(4f, size.y - width), Vector2.right, size.x - 8f, true);
            Edge(new Vector2(0f, 4f), Vector2.up, size.y - 8f, false);
            Edge(new Vector2(size.x - width, 4f), Vector2.up, size.y - 8f, false);
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color, FontStyles style = FontStyles.Normal)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.one);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
