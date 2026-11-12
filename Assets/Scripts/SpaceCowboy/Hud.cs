using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceCowboy
{
    /// <summary>
    /// The on-screen overlay during play: the aim cursor, the hit marker and the count of targets destroyed.
    /// Hidden on the menu. Sizes are on a 1280 × 720 reference screen.
    /// </summary>
    [DefaultExecutionOrder(100)] // after the camera has moved, so the aim cursor sits exactly on its spot
    public class Hud : MonoBehaviour
    {
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
            go.AddComponent<GraphicRaycaster>(); // so the mouse can use the settings panel
            return go.AddComponent<Hud>();
        }

        void Awake()
        {
            root = UI.Rect("Overlay", transform, Vector2.zero, Vector2.one).gameObject;
            BuildControlsPanel();
            Show(false);
        }

        /// <summary>MAIN MENU was chosen on the settings panel.</summary>
        public event System.Action MainMenuChosen;

        /// <summary>The controls and settings panel, opened during play. It starts closed.</summary>
        public ControlsPanel Controls { get; private set; }

        void BuildControlsPanel()
        {
            // The same battered screen as the start menu: Ed's beige bezel around a black CRT,
            // with scanlines and a vignette over the page.
            var centre = new Vector2(0.5f, 0.5f);
            var holder = UI.Rect("Settings Window", root.transform, centre, centre);
            holder.sizeDelta = StartMenu.FramePixels;
            holder.localScale = Vector3.one * 1.45f;
            var bezel = holder.gameObject.AddComponent<RawImage>();
            bezel.texture = StartMenu.FrameTexture();
            bezel.color = new Color(0.8f, 0.78f, 0.74f); // a touch of shade, like the lit 3D screen on the menu

            var screen = UI.Rect("Screen", holder, centre, centre);
            screen.sizeDelta = StartMenu.MenuPixels;
            screen.gameObject.AddComponent<Image>().color = Palette.Crt;
            Controls = new ControlsPanel(screen, () => MainMenuChosen?.Invoke());
            StartMenu.AddScreenEffects(screen);
            holder.gameObject.SetActive(false);
            Controls.Closed += () => holder.gameObject.SetActive(false);
            controlsWindow = holder;
        }

        RectTransform controlsWindow;

        /// <summary>Open the settings over the game. The game keeps running behind them.</summary>
        public void OpenControls()
        {
            controlsWindow.gameObject.SetActive(true);
            controlsWindow.SetAsLastSibling();
            Controls.Open();
        }

        public void Show(bool visible) => root.SetActive(visible);

        ShipWeapon aimWeapon;
        Camera aimCamera;
        Image aimCursor;

        /// <summary>Show a cursor on screen where this weapon's shots will go.</summary>
        public void ShowAim(ShipWeapon weapon, Camera camera)
        {
            aimWeapon = weapon;
            aimCamera = camera;
            if (aimCursor) return;
            var rect = UI.Rect("Aim Cursor", root.transform, Vector2.zero, Vector2.zero);
            rect.sizeDelta = new Vector2(40f, 40f);
            aimCursor = rect.gameObject.AddComponent<Image>();
            aimCursor.sprite = AimSprite();
            aimCursor.color = Palette.LEDGreen;
            aimCursor.raycastTarget = false;

            var markRect = UI.Rect("Hit Marker", rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            markRect.sizeDelta = new Vector2(46f, 46f);
            hitMarker = markRect.gameObject.AddComponent<Image>();
            hitMarker.sprite = HitSprite();
            hitMarker.raycastTarget = false;
            hitMarker.enabled = false;

            // How many you've destroyed, at the top of the screen. Appears after the first one.
            kills = UI.Text("X_X ", root.transform, "", 22f, Palette.LEDGreen);
            kills.font = Fonts.Lcd;
            kills.alignment = TMPro.TextAlignmentOptions.Top;
            kills.rectTransform.offsetMax = new Vector2(0f, -18f);
            kills.text = $"X_X {killCount:00}";
            kills.enabled = true;

            weapon.LandedHit += _ => ShowHit(Color.white, 0.1f);
            weapon.Destroyed += _ =>
            {
                ShowHit(Palette.Blush, 0.3f);
                killCount++;
                kills.text = $"X_X {killCount:00}";
                // kills.enabled = true;
            };
        }

        Image hitMarker;
        TextMeshProUGUI kills;
        int killCount;
        float hitMarkerLeft;

        void ShowHit(Color color, float seconds)
        {
            hitMarker.color = color;
            hitMarker.enabled = true;
            hitMarkerLeft = seconds;
        }

        void LateUpdate()
        {
            if (!aimCursor || !aimWeapon) return;
            Vector3 screen = aimCamera.WorldToScreenPoint(aimWeapon.AimPoint);
            aimCursor.enabled = screen.z > 0f; // mid-flip it can swing behind the camera
            aimCursor.rectTransform.position = new Vector3(screen.x, screen.y, 0f);
            aimCursor.color = aimWeapon.AimTarget ? Palette.Blush : Palette.LEDGreen; // red when shots will hit something

            if (hitMarkerLeft > 0f)
            {
                hitMarkerLeft -= Time.deltaTime;
                if (hitMarkerLeft <= 0f) hitMarker.enabled = false;
            }
        }

        /// <summary>Four short diagonal strokes around the cursor.</summary>
        static Sprite HitSprite()
        {
            const int size = 120;
            var p = new TexturePainter(size, size, Color.clear);
            var c = new Vector2(size / 2f, size / 2f);
            foreach (var d in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
                p.Stroke(new System.Collections.Generic.List<Vector2> { c + d * 22f, c + d * 40f }, 7f, Color.white);
            return p.ToSprite();
        }

        /// <summary>A ring with four inward ticks and a center dot, like an LED gunsight.</summary>
        static Sprite AimSprite()
        {
            const int size = 120;
            var p = new TexturePainter(size, size, Color.clear);
            var c = new Vector2(size / 2f, size / 2f);
            p.Stroke(TexturePainter.Ellipse(c, new Vector2(44f, 44f)), 6f, Color.white);
            foreach (var d in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
                p.Stroke(new System.Collections.Generic.List<Vector2> { c + d * 58f, c + d * 30f }, 6f, Color.white);
            p.Fill(TexturePainter.Ellipse(c, new Vector2(6f, 6f)), Color.white);
            return p.ToSprite();
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
