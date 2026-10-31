using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceCowboy
{
    /// <summary>
    /// The start menu on Ed's floating screen (ported from Menu.ts, ScreenDrone.ts and ui.css): a battered,
    /// Ed-built screen that hovers on two little thrusters in front of the camera, with the menu on it.
    /// Works with the mouse, the keyboard (Up/Down or W/S, Enter or Space, Left/Right for the time of day)
    /// and a gamepad (D-pad or left stick, A/Cross to choose).
    ///
    /// Sizes are in metres, like the web version; the game holds it close to the camera. The screen
    /// faces -Z, toward the camera. The menu is laid out in the web page's CSS pixels, then scaled onto it.
    /// </summary>
    public class StartMenu : MonoBehaviour
    {
        /// <summary>The screen's visible area, the size the menu is drawn at on it.</summary>
        public static readonly Vector2 ScreenSize = new Vector2(0.74f, 0.48f);
        /// <summary>The menu is laid out at this many pixels, then scaled onto the screen.</summary>
        static readonly Vector2 MenuPixels = new Vector2(540f, 351f);
        static readonly Vector3 Frame = new Vector3(0.86f, 0.62f, 0.07f);
        const float StickThreshold = 0.6f;

        enum Item { Training, Controls, Local, Online, Time }
        static readonly (Item id, string label, bool comingSoon)[] Items =
        {
            (Item.Training, "TRAINING", false),
            (Item.Controls, "CONTROLS", false),
            (Item.Local, "1V1 LOCAL", true),
            (Item.Online, "ONLINE", true),
            (Item.Time, "TIME OF DAY", false),
        };

        public event Action TrainingChosen;
        public event Action<TimeOfDaySetting> TimeOfDayChosen;

        /// <summary>The glowing jet discs under the thrusters (brightened as it flies away).</summary>
        public Renderer[] Jets { get; private set; }

        TimeOfDaySetting time;
        int selected;
        bool active = true;
        bool showingControls;
        RectTransform mainPage, controlsPage;
        bool stickWasHeld;
        Row[] rows;
        TextMeshProUGUI timeValue;
        Image cursor;
        Material amberGlow, plain;

        class Row
        {
            public RectTransform rect;
            public Image background, edge, arrow;
            public TextMeshProUGUI label, tag;
            public float shake = -1f; // seconds into the "nope" shake, or -1
        }

        public static StartMenu Create(Camera camera, TimeOfDaySetting time)
        {
            var menu = new GameObject("Start Menu (floating screen)").AddComponent<StartMenu>();
            menu.time = time;
            menu.BuildDrone();
            menu.BuildScreen(camera);
            return menu;
        }

        /// <summary>Stop taking input (a mode was chosen), but stay on screen while it flies away.</summary>
        public void Lock() => active = false;

        void Update()
        {
            cursor.enabled = Time.unscaledTime % 1f < 0.5f; // blink: 1s steps(1)
            for (int i = 0; i < rows.Length; i++) Shake(rows[i]);
            if (active) ReadInput();
        }

        void ReadInput()
        {
            var keys = Keyboard.current;
            var gamepad = Gamepad.current;
            if (showingControls)
            {
                // Any of these goes back to the menu.
                bool back = (keys != null && (keys.escapeKey.wasPressedThisFrame || keys.backspaceKey.wasPressedThisFrame ||
                                              keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame)) ||
                            (gamepad != null && (gamepad.buttonEast.wasPressedThisFrame || gamepad.buttonSouth.wasPressedThisFrame ||
                                                 gamepad.startButton.wasPressedThisFrame));
                if (back) ShowControls(false);
                return;
            }

            if (keys != null)
            {
                if (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame) Move(-1);
                if (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame) Move(1);
                if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame) CycleTime(-1);
                if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame) CycleTime(1);
                if (keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame) Activate(selected);
            }

            var pad = Gamepad.current;
            if (pad == null) return;
            float stickY = pad.leftStick.ReadValue().y;
            bool stickHeld = Mathf.Abs(stickY) > StickThreshold;
            if (pad.dpad.up.wasPressedThisFrame || (stickHeld && !stickWasHeld && stickY > 0f)) Move(-1);
            if (pad.dpad.down.wasPressedThisFrame || (stickHeld && !stickWasHeld && stickY < 0f)) Move(1);
            if (pad.dpad.left.wasPressedThisFrame) CycleTime(-1);
            if (pad.dpad.right.wasPressedThisFrame) CycleTime(1);
            if (pad.buttonSouth.wasPressedThisFrame) Activate(selected);
            stickWasHeld = stickHeld;
        }

        void Move(int by) => Select((selected + by + Items.Length) % Items.Length);

        void Select(int i)
        {
            selected = i;
            for (int j = 0; j < rows.Length; j++)
            {
                bool on = j == i;
                bool soon = Items[j].comingSoon;
                var r = rows[j];
                // .menu-item.selected: amber bar, yellow left edge, CRT-black text, no glow.
                r.background.color = on ? (soon ? Palette.AmberDim : Palette.Amber) : Color.clear;
                r.edge.color = on ? Palette.Yellow : Color.clear;
                r.arrow.enabled = on;
                r.label.color = on ? Palette.Crt : soon ? Palette.AmberDim : Palette.Amber;
                r.label.fontSharedMaterial = on || soon ? plain : amberGlow;
                if (r.tag) r.tag.color = on ? Palette.Crt : soon ? Palette.Blush : Palette.TealGlow;
            }
        }

        void Activate(int i)
        {
            if (!active) return;
            Select(i);
            var item = Items[i];
            if (item.comingSoon)
            {
                rows[i].shake = 0f; // not yet: a little shake, nothing else happens
                return;
            }
            if (item.id == Item.Time) CycleTime(1);
            if (item.id == Item.Controls) ShowControls(true);
            if (item.id == Item.Training) TrainingChosen?.Invoke();
        }

        /// <summary>Left / Right (or choosing the row) steps through Auto, Day, Night.</summary>
        void CycleTime(int by)
        {
            if (Items[selected].id != Item.Time) return;
            time = (TimeOfDaySetting)(((int)time + by + 3) % 3);
            ShowTime();
            TimeOfDayChosen?.Invoke(time);
        }

        void ShowTime()
        {
            string name = time switch { TimeOfDaySetting.Day => "DAY", TimeOfDaySetting.Night => "NIGHT", _ => "AUTO (CLOCK)" };
            timeValue.text = $"‹ {name} ›";
        }

        /// <summary>The "nope" shake: 6px left and right over 0.35s.</summary>
        static void Shake(Row row)
        {
            if (row.shake < 0f) return;
            row.shake += Time.unscaledDeltaTime;
            float t = row.shake / 0.35f;
            float x = t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI * 4f) * 6f;
            row.rect.anchoredPosition = new Vector2(x, 0f);
            if (t >= 1f) row.shake = -1f;
        }

        // --- The floating screen ------------------------------------------------------------------

        void BuildDrone()
        {
            // Held right in front of the camera, it would often face away from the sun and go dark.
            // These materials are always in their lit band, like the web game's sunlit screen.
            var frameMat = AlwaysLit(Palette.Toon(Color.white, FrameTexture()));
            var caseMat = AlwaysLit(Palette.Toon(Palette.Case));
            var lidMat = AlwaysLit(Palette.Toon(Palette.Lid));
            var hingeMat = AlwaysLit(Palette.Toon(Palette.Hinge));
            var t = transform;

            // The frame around the screen (scuffed beige front), and the plain black screen itself.
            Palette.Shape(PrimitiveType.Cube, "Frame", t, Vector3.zero, Frame, caseMat);
            Palette.Shape(PrimitiveType.Quad, "Frame Front", t, new Vector3(0f, 0f, -Frame.z / 2f - 0.001f),
                new Vector3(Frame.x, Frame.y, 1f), frameMat);
            Palette.Shape(PrimitiveType.Quad, "Screen", t, new Vector3(0f, 0f, -Frame.z / 2f - 0.002f),
                new Vector3(ScreenSize.x, ScreenSize.y, 1f), Palette.Unlit(Palette.Crt));

            // The guts on the back, a side clamp and an antenna.
            Palette.Shape(PrimitiveType.Cube, "Back", t, new Vector3(0f, 0f, Frame.z / 2f + 0.06f), new Vector3(0.5f, 0.34f, 0.12f), lidMat);
            Palette.Shape(PrimitiveType.Cube, "Clamp", t, new Vector3(Frame.x / 2f + 0.02f, 0.05f, 0f), new Vector3(0.04f, 0.3f, 0.06f), hingeMat);
            var antenna = Palette.Shape(PrimitiveType.Cylinder, "Antenna", t, new Vector3(-Frame.x / 2f + 0.08f, Frame.y / 2f + 0.13f, 0.02f),
                new Vector3(0.012f, 0.13f, 0.012f), hingeMat);
            antenna.transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            Palette.Shape(PrimitiveType.Sphere, "Antenna Tip", t, new Vector3(-Frame.x / 2f + 0.047f, Frame.y / 2f + 0.255f, 0.02f),
                Vector3.one * 0.04f, AlwaysLit(Palette.Toon(Palette.CraftAccent)));

            // Two thruster pods underneath, each with a glowing jet disc.
            var jetTexture = new TexturePainter(64, 64, Color.clear);
            jetTexture.Fill(TexturePainter.Ellipse(new Vector2(32f, 32f), new Vector2(30f, 30f)), Color.white);
            var jetMat = Palette.Unlit(new Color(Palette.Amber.r, Palette.Amber.g, Palette.Amber.b, 0.85f), jetTexture.ToTexture());
            Jets = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Palette.Shape(PrimitiveType.Cylinder, "Thruster", t, new Vector3(side * 0.27f, -Frame.y / 2f - 0.06f, 0.03f), new Vector3(0.14f, 0.07f, 0.14f), hingeMat);
                var jet = Palette.Shape(PrimitiveType.Quad, "Jet", t, new Vector3(side * 0.27f, -Frame.y / 2f - 0.132f, 0.03f), new Vector3(0.11f, 0.11f, 1f), jetMat);
                jet.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // facing down
                Jets[i] = jet.GetComponent<Renderer>();
            }

            // It's held close to the camera: no shadows (they'd land far below, out of place).
            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Material AlwaysLit(Material m)
        {
            m.SetFloat("_BandMid", -2f);
            m.SetFloat("_BandLit", -2f);
            return m;
        }

        /// <summary>The frame's front: beige with scuffs, tape on a corner, a few stickers and a grinning emoticon.</summary>
        static Texture2D FrameTexture()
        {
            const int w = 1024, h = 738;
            var p = new TexturePainter(w, h, Palette.Case);
            var rand = Seeded(31);
            for (int i = 0; i < 60; i++)
            {
                var scuff = rand() < 0.5f ? new Color(120 / 255f, 95 / 255f, 60 / 255f, 0.08f) : new Color(1f, 250 / 255f, 235 / 255f, 0.10f);
                var centre = new Vector2(rand() * w, rand() * h);
                var radius = new Vector2(8f + rand() * 40f, 4f + rand() * 14f);
                p.Fill(TexturePainter.Ellipse(centre, radius, rand() * Mathf.PI), scuff);
            }
            // Tape over the bottom-left corner of the bezel.
            p.Fill(TexturePainter.Box(new Vector2(50f, h - 45f), new Vector2(200f, 48f), 0.6f), new Color(214 / 255f, 190 / 255f, 140 / 255f, 0.9f));
            // Stickers along the bezel.
            p.Fill(TexturePainter.Box(new Vector2(470f, h - 30f), new Vector2(30f, 30f), 0.2f), Palette.Hex("#5b8fd6"));
            p.Fill(TexturePainter.Box(new Vector2(540f, h - 34f), new Vector2(30f, 30f), -0.25f), Palette.Hex("#e8423a"));
            p.Fill(TexturePainter.Box(new Vector2(930f, 36f), new Vector2(30f, 30f), 0.15f), Palette.Hex("#8fe3dc"));
            Emoticon.DrawSticker(p, new Vector2(w - 70f, h - 40f), 64f);
            return p.ToTexture();
        }

        /// <summary>The web game's tiny seeded random generator, so the scuffs land in the same places.</summary>
        static Func<float> Seeded(long seed) => () => (seed = seed * 16807 % 2147483647) / 2147483647f;

        // --- The menu on the screen (ui.css .machine-screen) ----------------------------------------

        void BuildScreen(Camera camera)
        {
            var canvasGo = new GameObject("Menu Canvas", typeof(RectTransform));
            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.SetParent(transform, false);
            canvasRect.localPosition = new Vector3(0f, 0f, -Frame.z / 2f - 0.004f);
            canvasRect.sizeDelta = MenuPixels;
            canvasRect.localScale = Vector3.one * (ScreenSize.x / MenuPixels.x);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvasGo.AddComponent<GraphicRaycaster>();

            amberGlow = Fonts.Glow(Fonts.Mono, new Color(1f, 0.54f, 0.12f, 0.55f), 0.35f); // text-shadow: 0 0 6px amber
            plain = Fonts.Mono.material;

            // padding: 26px 34px
            const float padX = 34f;

            // Two pages on the same screen: the menu, and the controls (for new players).
            mainPage = UI.Rect("Main Page", canvasRect, Vector2.zero, Vector2.one);
            BuildControlsPage(canvasRect, padX);
            var page = mainPage;

            // h1: 700 64px Oswald, uppercase, yellow, with a hard red shadow 3px down-right and a soft glow.
            var shadow = Line(page, "Title Shadow", "SPACE COWBOY", Fonts.Condensed, 64f, Palette.Red, padX + 3f, 32f + 3f, 64f);
            var title = Line(page, "Title", "SPACE COWBOY", Fonts.Condensed, 64f, Palette.Yellow, padX, 32f, 64f);
            title.fontSharedMaterial = Fonts.Glow(Fonts.Condensed, new Color(0.97f, 0.84f, 0.11f, 0.35f), 0.6f);
            title.ForceMeshUpdate();

            // The blinking cursor after the title: 0.3em × 0.8em, 0.08em to the right.
            var cursorRect = Anchored(UI.Rect("Cursor", page, new Vector2(0f, 1f), new Vector2(0f, 1f)), padX + title.preferredWidth + 5f, 32f + 9f, 19f, 51f);
            cursor = cursorRect.gameObject.AddComponent<Image>();
            cursor.color = Palette.Amber;
            cursor.raycastTarget = false;

            // .menu-items: 14px below the title; rows 2px apart.
            float y = 32f + 64f + 4f + 14f;
            rows = new Row[Items.Length];
            for (int i = 0; i < Items.Length; i++)
            {
                rows[i] = BuildRow(page, i, padX, y, 25f);
                y += 25f + 2f;
            }
            // .hint: 10px below, 14px Courier Prime, dim amber.
            Line(page, "Hint", "↑↓ select · ENTER / Ⓐ choose", Fonts.MonoRegular, 14f, Palette.AmberDim, padX, y - 2f + 10f, 21f);

            // Scanlines and a vignette on top, like an old screen.
            var scan = new TexturePainter(1, 3, Color.clear);
            scan.FillRect(new UnityEngine.Rect(0f, 0f, 1f, 1f), new Color(0f, 0f, 0f, 0.28f));
            var scanImage = UI.Rect("Scanlines", canvasRect, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            var scanTexture = scan.ToTexture(filter: FilterMode.Point);
            scanTexture.wrapMode = TextureWrapMode.Repeat;
            scanImage.texture = scanTexture;
            scanImage.uvRect = new UnityEngine.Rect(0f, 0f, 1f, MenuPixels.y / 3f);
            scanImage.raycastTarget = false;

            var vignette = new TexturePainter(128, 83, Color.clear);
            vignette.Shade((u, v) =>
            {
                // radial-gradient(ellipse at center, transparent 50%, rgba(0,0,0,0.55) 100%), reaching the corners.
                float d = Mathf.Sqrt(((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f);
                return new Color(0f, 0f, 0f, 0.55f * Mathf.Clamp01((d - 0.5f) / 0.5f));
            });
            var vignetteImage = UI.Rect("Vignette", canvasRect, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            vignetteImage.texture = vignette.ToTexture();
            vignetteImage.raycastTarget = false;

            Select(0);
            ShowTime();
        }

        void ShowControls(bool show)
        {
            showingControls = show;
            mainPage.gameObject.SetActive(!show);
            controlsPage.gameObject.SetActive(show);
        }

        /// <summary>What each control does, on the keyboard and on a controller.</summary>
        static readonly (string action, string keys, string pad)[] ControlList =
        {
            ("THROTTLE", "W / \u2191", "RT"),
            ("BRAKE \u00B7 REVERSE", "S / \u2193", "LT"),
            ("STEER", "A D / \u2190 \u2192", "LEFT STICK"),
            ("RISE (HOLD)", "SPACE", "\u24B6 / RB"),
            ("DROP (HOLD)", "C", "\u24CE / LB"),
            ("BOOST", "LEFT SHIFT", "\u24B7"),
            ("RESET SHIP", "R", "VIEW"),
            ("RESET BALL", "B", "\u24CD"),
            ("LOOK AROUND", "\u2014", "RIGHT STICK"),
        };

        /// <summary>The controls page: a heading, then each action with its key and its controller button.</summary>
        void BuildControlsPage(RectTransform canvas, float padX)
        {
            controlsPage = UI.Rect("Controls Page", canvas, Vector2.zero, Vector2.one);
            // Clicking anywhere on the page goes back.
            var catcher = controlsPage.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            var events = controlsPage.gameObject.AddComponent<EventTrigger>();
            AddEvent(events, EventTriggerType.PointerClick, () => { if (active) ShowControls(false); });

            Line(controlsPage, "Heading Shadow", "CONTROLS", Fonts.Condensed, 40f, Palette.Red, padX + 2f, 24f + 2f, 44f);
            var heading = Line(controlsPage, "Heading", "CONTROLS", Fonts.Condensed, 40f, Palette.Yellow, padX, 24f, 44f);
            heading.fontSharedMaterial = Fonts.Glow(Fonts.Condensed, new Color(0.97f, 0.84f, 0.11f, 0.35f), 0.6f);

            const float keysX = 200f, padColumnX = 340f, top = 96f, step = 23f;
            foreach (var (text, x) in new[] { ("KEYBOARD", keysX), ("CONTROLLER", padColumnX) })
            {
                var header = Line(controlsPage, text, text, Fonts.Condensed, 12f, Palette.Blush, x, top - 20f, 18f);
                header.characterSpacing = 12f;
            }

            var glow = Fonts.Glow(Fonts.Mono, new Color(1f, 0.54f, 0.12f, 0.55f), 0.35f);
            for (int i = 0; i < ControlList.Length; i++)
            {
                var (action, keys, pad) = ControlList[i];
                float y = top + i * step;
                if (i % 2 == 0)
                {
                    // A faint band on every other row, so each line is easy to follow across.
                    var band = Anchored(UI.Rect("Band", controlsPage, new Vector2(0f, 1f), new Vector2(0f, 1f)), padX - 6f, y, MenuPixels.x - 2f * padX + 12f, step);
                    var bandImage = band.gameObject.AddComponent<Image>();
                    bandImage.color = new Color(1f, 0.54f, 0.12f, 0.07f);
                    bandImage.raycastTarget = false;
                }
                var label = Line(controlsPage, action, action, Fonts.Mono, 14f, Palette.Amber, padX, y, step);
                label.fontSharedMaterial = glow;
                Line(controlsPage, action + " Keys", keys, Fonts.Lcd, 20f, Palette.TealGlow, keysX, y, step);
                Line(controlsPage, action + " Pad", pad, Fonts.Lcd, 20f, Palette.TealGlow, padColumnX, y, step);
            }

            Line(controlsPage, "Back Hint", "ENTER / ESC / \u24B7 back", Fonts.MonoRegular, 14f, Palette.AmberDim, padX, top + ControlList.Length * step + 8f, 21f);
            controlsPage.gameObject.SetActive(false);
        }

        Row BuildRow(RectTransform canvas, int i, float padX, float top, float height)
        {
            var item = Items[i];
            var holder = UI.Rect(item.label, canvas, new Vector2(0f, 1f), new Vector2(1f, 1f));
            holder.offsetMin = new Vector2(padX, -top - height);
            holder.offsetMax = new Vector2(-padX, -top);
            var row = new Row { rect = UI.Rect("Row", holder, Vector2.zero, Vector2.one) };
            row.background = row.rect.gameObject.AddComponent<Image>();

            // border-left: 3px
            var edge = UI.Rect("Edge", row.rect, Vector2.zero, new Vector2(0f, 1f));
            edge.pivot = new Vector2(0f, 0.5f);
            edge.sizeDelta = new Vector2(3f, 0f);
            row.edge = edge.gameObject.AddComponent<Image>();
            row.edge.raycastTarget = false;

            // The selected row's "▶" (drawn, since Courier Prime doesn't have it), then the label after two spaces.
            var arrowRect = Anchored(UI.Rect("Arrow", row.rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f)), 14f, 0f, 9f, 11f, middle: true);
            row.arrow = arrowRect.gameObject.AddComponent<Image>();
            row.arrow.sprite = Triangle();
            row.arrow.color = Palette.Crt;
            row.arrow.raycastTarget = false;

            // font: 700 19px Courier Prime; padding: 2px 10px
            row.label = UI.Text("Label", row.rect, "  " + item.label, 19f, Palette.Amber);
            row.label.font = Fonts.Mono;
            row.label.margin = new Vector4(13f, 0f, 10f, 0f);
            row.label.alignment = TextAlignmentOptions.MidlineLeft;
            if (item.comingSoon)
            {
                // .tag: 700 12px Oswald, letter-spacing 0.12em, blush red
                row.tag = UI.Text("Tag", row.rect, "COMING SOON", 12f, Palette.Blush);
                row.tag.font = Fonts.Condensed;
                row.tag.characterSpacing = 12f;
            }
            else if (item.id == Item.Time)
            {
                // .value: 25px VT323, teal glow
                row.tag = timeValue = UI.Text("Value", row.rect, "", 25f, Palette.TealGlow);
                row.tag.font = Fonts.Lcd;
            }
            if (row.tag)
            {
                row.tag.margin = new Vector4(10f, 0f, 10f, 0f);
                row.tag.alignment = TextAlignmentOptions.MidlineRight;
            }

            // The mouse: hovering selects, clicking chooses.
            var events = row.rect.gameObject.AddComponent<EventTrigger>();
            AddEvent(events, EventTriggerType.PointerEnter, () => { if (active) Select(i); });
            AddEvent(events, EventTriggerType.PointerClick, () => Activate(i));
            return row;
        }

        static Sprite triangle;

        static Sprite Triangle()
        {
            if (triangle) return triangle;
            var p = new TexturePainter(36, 44, Color.clear);
            p.Fill(p.Polygon(0f, 0f, 1f, 0.5f, 0f, 1f), Color.white);
            return triangle = p.ToSprite();
        }

        static void AddEvent(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        /// <summary>Place a box by its left edge and top (or middle), in CSS pixels from the top-left.</summary>
        static RectTransform Anchored(RectTransform rect, float left, float top, float width, float height, bool middle = false)
        {
            rect.pivot = new Vector2(0f, middle ? 0.5f : 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
            return rect;
        }

        /// <summary>One line of text, its box starting at (x, top) in CSS pixels from the top-left.</summary>
        static TextMeshProUGUI Line(RectTransform canvas, string name, string text, TMP_FontAsset font, float size, Color color, float x, float top, float height)
        {
            var tmp = UI.Text(name, canvas, text, size, color);
            tmp.font = font;
            var rect = tmp.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(x, -top - height);
            rect.offsetMax = new Vector2(0f, -top);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            return tmp;
        }
    }
}
