using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpaceCowboy
{
    /// <summary>
    /// The on-screen overlay during play. Kept to a minimum, with as little text as possible:
    ///   the aim cursor (red when your shots will hit), with the hit marker flashing on it (red when something's destroyed);
    ///   the streak ring around the cursor: it fills a third with each kill in a row, glows yellow when your special is
    ///     ready, and runs down while the special is going;
    ///   a hollow diamond on where to go next (capture the flag), pinned to the screen's edge when it's behind you;
    ///   the screen's edges flashing red when you're hit;
    ///   the score and the clock at the top, as bare amber numbers (yours bright, theirs dim).
    /// Hidden on the menu. Sizes are on a 1280 × 720 reference screen.
    /// </summary>
    [DefaultExecutionOrder(100)] // after the camera has moved, so the aim cursor sits exactly on its spot
    public class Hud : MonoBehaviour
    {
        GameObject root;

        /// <summary>time is the scene's TimeOfDay, for the settings panel's GAME tab.</summary>
        public static Hud Create(TimeOfDay time)
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
            var hud = go.AddComponent<Hud>();
            hud.BuildControlsPanel(time); // while the overlay is still showing, so its text can be measured
            hud.Results = new ResultsPanel(hud.root.transform);
            hud.Show(false);
            return hud;
        }

        void Awake()
        {
            root = UI.Rect("Overlay", transform, Vector2.zero, Vector2.one).gameObject;
        }

        /// <summary>MAIN MENU was chosen on the settings panel.</summary>
        public event System.Action MainMenuChosen;

        /// <summary>The controls and settings panel, opened during play. It starts closed.</summary>
        public ControlsPanel Controls { get; private set; }

        void BuildControlsPanel(TimeOfDay time)
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
            Controls = new ControlsPanel(screen, () => MainMenuChosen?.Invoke(), time);
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

            // The streak ring: a thin circle around the cursor that fills like a clock hand.
            var ringRect = UI.Rect("Streak Ring", rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            ringRect.sizeDelta = new Vector2(64f, 64f);
            streakRing = ringRect.gameObject.AddComponent<Image>();
            streakRing.sprite = RingSprite();
            streakRing.type = Image.Type.Filled;
            streakRing.fillMethod = Image.FillMethod.Radial360;
            streakRing.fillOrigin = (int)Image.Origin360.Top;
            streakRing.fillClockwise = true;
            streakRing.raycastTarget = false;
            streakRing.enabled = false;

            weapon.LandedHit += _ => ShowHit(Color.white, 0.1f);
            weapon.Destroyed += _ => ShowHit(Palette.Blush, 0.3f);
        }

        // --- Getting hit ----------------------------------------------------------------------------

        Damageable yourHealth;
        RawImage hurtGlow;
        float hurtFlash;

        /// <summary>
        /// Show your ship's damage without numbers: the screen's edges flash red with each hit, and stay faintly red
        /// while your health is low.
        /// </summary>
        public void TrackHealth(Damageable health)
        {
            yourHealth = health;
            if (!hurtGlow)
            {
                var rect = UI.Rect("Hurt Glow", root.transform, Vector2.zero, Vector2.one);
                rect.SetAsFirstSibling(); // under everything else
                hurtGlow = rect.gameObject.AddComponent<RawImage>();
                hurtGlow.texture = HurtTexture();
                hurtGlow.raycastTarget = false;
                hurtGlow.color = Color.clear;
            }
            health.Damaged += (amount, _) => hurtFlash = Mathf.Min(1f, hurtFlash + 0.35f + amount / Mathf.Max(health.maxHealth, 1f));
        }

        void UpdateHurtGlow()
        {
            if (!hurtGlow || !yourHealth) return;
            hurtFlash = Mathf.Max(0f, hurtFlash - Time.deltaTime * 2.5f);
            float low = yourHealth.IsDead ? 0f : Mathf.Clamp01(1f - yourHealth.Health / yourHealth.maxHealth);
            float alpha = Mathf.Max(hurtFlash * 0.8f, low * low * 0.55f);
            var c = Palette.Blush;
            c.a = alpha;
            hurtGlow.color = c;
            hurtGlow.enabled = alpha > 0.01f;
        }

        static Texture2D hurtTexture;

        /// <summary>Clear in the middle, solid at the edges.</summary>
        static Texture2D HurtTexture()
        {
            if (hurtTexture) return hurtTexture;
            var p = new TexturePainter(128, 72, Color.clear);
            p.Shade((u, v) =>
            {
                float d = Mathf.Sqrt(((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f);
                float a = Mathf.Clamp01((d - 0.55f) / 0.45f);
                return new Color(1f, 1f, 1f, a * a);
            });
            return hurtTexture = p.ToTexture();
        }

        // --- The streak ring ---------------------------------------------------------------------

        Image streakRing;
        float ringFill, ringNudge = -1f;
        Color ringColor;
        bool ringPulse;

        /// <summary>
        /// The ring around the cursor: how full (0 to 1), its color, and whether it pulses (special ready).
        /// fill 0 hides it.
        /// </summary>
        public void SetStreakRing(float fill, Color color, bool pulse)
        {
            ringFill = fill;
            ringColor = color;
            ringPulse = pulse;
        }

        /// <summary>A quick shake and red flash of the ring: the special isn't ready yet.</summary>
        public void NudgeStreakRing() => ringNudge = 0f;

        void UpdateStreakRing()
        {
            if (!streakRing) return;
            bool nudging = ringNudge >= 0f;
            streakRing.enabled = ringFill > 0.001f || nudging;
            if (!streakRing.enabled) return;
            Color color = ringColor;
            if (ringPulse) color.a *= 0.55f + 0.45f * Mathf.Sin(Time.time * 8f) * 0.5f + 0.225f;
            float scale = ringPulse ? 1f + 0.06f * Mathf.Sin(Time.time * 8f) : 1f;
            Vector2 shift = Vector2.zero;
            if (nudging)
            {
                ringNudge += Time.deltaTime;
                float t = ringNudge / 0.35f;
                shift.x = t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI * 4f) * 5f;
                color = Color.Lerp(Palette.Blush, color, t);
                if (ringFill < 0.001f) color.a *= 1f - t; // an empty ring shows itself just for the shake
                if (t >= 1f) ringNudge = -1f;
            }
            streakRing.fillAmount = nudging && ringFill < 0.001f ? 1f : ringFill;
            streakRing.color = color;
            streakRing.rectTransform.anchoredPosition = shift;
            streakRing.rectTransform.localScale = Vector3.one * scale;
        }

        /// <summary>A thin circle, for the streak ring.</summary>
        static Sprite RingSprite()
        {
            const int size = 128;
            var p = new TexturePainter(size, size, Color.clear);
            p.Stroke(TexturePainter.Ellipse(new Vector2(size / 2f, size / 2f), new Vector2(58f, 58f)), 5f, Color.white);
            return p.ToSprite();
        }

        // --- Score and clock ------------------------------------------------------------------------

        TextMeshProUGUI score, timer;
        float scorePunch;
        bool timerBlinks;

        /// <summary>
        /// The score at the top: yours, then theirs (the other team, or the best rival in free for all).
        /// All amber like the rest of the HUD: yours bright, theirs dim. It swells for a moment when it changes.
        /// </summary>
        public void ShowScore(int yours, int theirs)
        {
            if (!score)
            {
                score = UI.Text("Score", root.transform, "", 34f, Color.white);
                score.font = Fonts.Lcd;
                score.alignment = TextAlignmentOptions.Top;
                score.rectTransform.offsetMax = new Vector2(0f, -18f);
            }
            string text = $"<color=#{AmberHex}>{yours}</color>  <color=#{AmberDimHex}>{theirs}</color>";
            score.gameObject.SetActive(true);
            if (score.text != text && !string.IsNullOrEmpty(score.text)) scorePunch = 0.4f;
            score.text = text;
        }

        static readonly string AmberHex = ColorUtility.ToHtmlStringRGB(Palette.Amber);
        static readonly string AmberDimHex = ColorUtility.ToHtmlStringRGB(Palette.AmberDim);

        /// <summary>The match clock, under the score. Empty hides it. blink: it flashes (sudden death).</summary>
        public void SetTimer(string text, Color color, bool blink = false)
        {
            if (!timer)
            {
                timer = UI.Text("Match Clock", root.transform, "", 22f, Palette.Amber);
                timer.font = Fonts.Lcd;
                timer.alignment = TextAlignmentOptions.Top;
                timer.rectTransform.offsetMax = new Vector2(0f, -52f);
            }
            timer.text = text;
            timer.color = color;
            timerBlinks = blink;
        }

        void UpdateScoreAndClock()
        {
            if (score)
            {
                scorePunch = Mathf.Max(0f, scorePunch - Time.deltaTime);
                score.rectTransform.localScale = Vector3.one * (1f + scorePunch * 0.6f);
            }
            if (timer) timer.enabled = !timerBlinks || Time.time % 0.8f < 0.5f;
        }

        // --- Markers -------------------------------------------------------------------------------

        Image objectiveMarker;
        Vector3? objectiveSpot;
        readonly System.Collections.Generic.List<Image> markers = new();
        static Sprite markerSprite;

        /// <summary>Where to go next (capture the flag): a hollow amber diamond over that spot. null hides it.</summary>
        public void SetObjective(Vector3? spot)
        {
            if (!objectiveMarker)
            {
                objectiveMarker = MakeMarker("Objective", 34f);
                objectiveMarker.color = Palette.Amber;
            }
            objectiveSpot = spot;
        }

        /// <summary>
        /// Diamonds on screen over these spots, seen through anything (the Spy's special). null clears them.
        /// Spots behind you are pinned to the edge of the screen, so you know which way to turn.
        /// </summary>
        public void SetMarkers(System.Collections.Generic.List<Vector3> spots, Color color)
        {
            int count = spots == null || !aimCamera ? 0 : spots.Count;
            while (markers.Count < count) markers.Add(MakeMarker("Marker", 28f));
            for (int i = 0; i < markers.Count; i++)
            {
                bool on = i < count;
                markers[i].enabled = on;
                if (!on) continue;
                PlaceOver(markers[i], spots[i]);
                markers[i].color = color;
            }
        }

        void UpdateObjective()
        {
            if (!objectiveMarker) return;
            objectiveMarker.enabled = objectiveSpot.HasValue && aimCamera;
            if (objectiveMarker.enabled) PlaceOver(objectiveMarker, objectiveSpot.Value);
        }

        Image MakeMarker(string name, float size)
        {
            var rect = UI.Rect(name, root.transform, Vector2.zero, Vector2.zero);
            rect.sizeDelta = new Vector2(size, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = MarkerSprite();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Put a marker over a spot in the world; a spot behind you is pinned to the screen's edge.</summary>
        void PlaceOver(Image marker, Vector3 spot)
        {
            Vector3 screen = aimCamera.WorldToScreenPoint(spot);
            if (screen.z < 0f) screen = new Vector3(Screen.width - screen.x, screen.y < Screen.height / 2f ? 0f : Screen.height, 0f);
            screen.x = Mathf.Clamp(screen.x, 20f, Screen.width - 20f);
            screen.y = Mathf.Clamp(screen.y, 20f, Screen.height - 20f);
            marker.rectTransform.position = new Vector3(screen.x, screen.y, 0f);
        }

        /// <summary>A hollow diamond.</summary>
        static Sprite MarkerSprite()
        {
            if (markerSprite) return markerSprite;
            var p = new TexturePainter(64, 64, Color.clear);
            p.Fill(p.Polygon(0.5f, 0.05f, 0.95f, 0.5f, 0.5f, 0.95f, 0.05f, 0.5f), Color.white);
            p.Fill(p.Polygon(0.5f, 0.3f, 0.7f, 0.5f, 0.5f, 0.7f, 0.3f, 0.5f), Color.clear);
            return markerSprite = p.ToSprite();
        }

        // --- Matches ---------------------------------------------------------------------------

        /// <summary>The end-of-match screen.</summary>
        public ResultsPanel Results { get; private set; }

        /// <summary>Clear everything a match put on screen.</summary>
        public void HideMatch()
        {
            if (score) score.gameObject.SetActive(false);
            if (timer) timer.text = "";
            objectiveSpot = null;
            SetStreakRing(0f, Color.white, false);
            SetMarkers(null, Color.white);
            Results.Hide();
        }

        // --- Every frame ---------------------------------------------------------------------------

        void LateUpdate()
        {
            UpdateScoreAndClock();
            UpdateObjective();
            UpdateStreakRing();
            UpdateHurtGlow();
            UpdateAim();
        }

        Image hitMarker;
        float hitMarkerLeft;

        void ShowHit(Color color, float seconds)
        {
            hitMarker.color = color;
            hitMarker.enabled = true;
            hitMarkerLeft = seconds;
        }

        /// <summary>The cursor sits where shots will land; it's red when they'll hit something.</summary>
        void UpdateAim()
        {
            if (!aimCursor || !aimWeapon) return;
            Vector3 screen = aimCamera.WorldToScreenPoint(aimWeapon.Aim(out var target));
            aimCursor.enabled = screen.z > 0f; // mid-flip it can swing behind the camera
            aimCursor.rectTransform.position = new Vector3(screen.x, screen.y, 0f);
            aimCursor.color = target ? Palette.Blush : Palette.LEDGreen;

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

        /// <summary>Run action when the mouse does this to the object (enters it, clicks it, drags on it...).</summary>
        public static void On(GameObject target, EventTriggerType type, System.Action<BaseEventData> action)
        {
            var trigger = target.GetComponent<EventTrigger>();
            if (!trigger) trigger = target.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(data => action(data));
            trigger.triggers.Add(entry);
        }

        public static void On(Component target, EventTriggerType type, System.Action<BaseEventData> action) => On(target.gameObject, type, action);

        /// <summary>The usual pair for a menu choice: hovering selects it, clicking chooses it.</summary>
        public static void Choosable(GameObject target, System.Action select, System.Action choose)
        {
            On(target, EventTriggerType.PointerEnter, _ => select());
            On(target, EventTriggerType.PointerClick, _ => choose());
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
