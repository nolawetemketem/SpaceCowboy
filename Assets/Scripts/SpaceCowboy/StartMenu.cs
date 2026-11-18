using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceCowboy
{
    /// <summary>
    /// The start menu on Ed's floating screen (ported from Menu.ts, ScreenDrone.ts and ui.css): a battered,
    /// Ed-built screen that hovers on two little thrusters in front of the camera, with the menu on it.
    /// Pages:
    ///   Main      TRAINING (straight into the training area behind the menu), LOCAL ›, ONLINE ›, SETTINGS
    ///   Local     DEATH MATCH ›, CAPTURE THE FLAG ›, then the size of the match (every match is 5 minutes)
    ///   Lobby     every pilot votes for a map; you pick your fighter here too (see Lobby)
    ///   Online    DEATH MATCH, CAPTURE THE FLAG (not ready yet: they give a little shake)
    ///   Settings  the settings panel (sensitivity, controls, and the game's options like time of day)
    /// Works with the mouse, the keyboard (Up/Down or W/S, Enter or Space to choose, Esc or Backspace to go back)
    /// and a gamepad (D-pad or left stick, A/Cross to choose, B/Circle to go back).
    ///
    /// Sizes are in metres, like the web version; the game holds it close to the camera. The screen
    /// faces -Z, toward the camera. The menu is laid out in the web page's CSS pixels, then scaled onto it.
    /// </summary>
    public class StartMenu : MonoBehaviour
    {
        /// <summary>The screen's visible area, the size the menu is drawn at on it.</summary>
        public static readonly Vector2 ScreenSize = new Vector2(0.74f, 0.48f);
        /// <summary>The menu is laid out at this many pixels, then scaled onto the screen.</summary>
        public static readonly Vector2 MenuPixels = new Vector2(540f, 351f);
        static readonly Vector3 Frame = new Vector3(0.86f, 0.62f, 0.07f);
        /// <summary>The whole front of the frame, bezel and all, at the same pixel scale as the menu.</summary>
        public static Vector2 FramePixels => new Vector2(MenuPixels.x * Frame.x / ScreenSize.x, MenuPixels.y * Frame.y / ScreenSize.y);
        const float StickThreshold = 0.6f;

        /// <summary>One choice on a menu page.</summary>
        class Entry
        {
            public string label, value;  // value: small text on the right, like "›"
            public bool comingSoon;      // shown dimmed; choosing it just gives a little shake
            public Action chosen;
            public string hint;          // shown in the hint line while this row is selected (a fighter's special)
        }

        /// <summary>TRAINING was chosen.</summary>
        public event Action TrainingChosen;
        /// <summary>A match was set up (mode, size, the voted map, fighter) and is ready to start.</summary>
        public event Action<MatchSetup> MatchChosen;

        /// <summary>The training area (the map behind the menu). It isn't up for the vote.</summary>
        public string TrainingMap;

        /// <summary>The picture on each fighter's card (see FighterPortrait). Set by the game once the ship is painted.</summary>
        public Texture FighterPicture;

        MatchSetup pending;
        Lobby lobby;
        bool onLobbyPage;
        string pageHint;

        // The two thrusters' flames (see SetThrust).
        const float NozzleRadius = 0.048f, IdleFlame = 0.07f;
        Transform[] flames;
        ParticleSystem[] embers;
        Material flameMaterial;
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        TimeOfDay time;
        int selected;
        bool active = true;
        bool showingControls;
        RectTransform mainPage, rowsHolder;
        TextMeshProUGUI hint;
        ControlsPanel controls;
        bool stickWasHeld;
        readonly List<Entry> entries = new();
        Row[] rows = new Row[0];
        Action back; // what Esc / B does on this page (null on the main page)
        int columns = 1; // 1 for a list of rows; more for a grid of cards (the fighters)
        Image cursor;
        Material amberGlow, plain;

        class Row
        {
            public RectTransform rect;
            public Image background, edge, arrow;
            public TextMeshProUGUI label, tag;
            public float shake = -1f; // seconds into the "nope" shake, or -1
        }

        public static StartMenu Create(Camera camera, TimeOfDay time)
        {
            var menu = new GameObject("Start Menu (floating screen)").AddComponent<StartMenu>();
            menu.time = time;
            menu.BuildDrone();
            menu.BuildScreen(camera);
            return menu;
        }

        /// <summary>Stop taking input (a mode was chosen), but stay on screen while it flies away.</summary>
        public void Lock() => active = false;

        int ignoreInputUntil;

        /// <summary>Take input again (back from Training), on the main page's top row.
        /// The press that brought us back is ignored, so it can't choose something here too.</summary>
        public void Unlock()
        {
            active = true;
            ignoreInputUntil = Time.frameCount + 1;
            stickWasHeld = true;
            lobby = null;
            ShowMain();
        }

        void Update()
        {
            cursor.enabled = Time.unscaledTime % 1f < 0.5f; // blink: 1s steps(1)
            for (int i = 0; i < rows.Length; i++) Shake(rows[i]);
            if (active && Time.frameCount > ignoreInputUntil) ReadInput();
            TickLobby();
        }

        void ReadInput()
        {
            var keys = Keyboard.current;
            if (showingControls)
            {
                controls.Tick();
                return;
            }

            bool goBack = false;
            if (keys != null)
            {
                if (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame) Move(-1);
                if (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame) Move(1);
                if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame) Step(-1);
                if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame) Step(1);
                if (keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame) Activate(selected);
                goBack |= keys.escapeKey.wasPressedThisFrame || keys.backspaceKey.wasPressedThisFrame;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                bool stickHeld = stick.magnitude > StickThreshold;
                bool tilted = stickHeld && !stickWasHeld; // a new push of the stick, read like one press
                bool upDown = Mathf.Abs(stick.y) >= Mathf.Abs(stick.x);
                if (pad.dpad.up.wasPressedThisFrame || (tilted && upDown && stick.y > 0f)) Move(-1);
                if (pad.dpad.down.wasPressedThisFrame || (tilted && upDown && stick.y < 0f)) Move(1);
                if (pad.dpad.left.wasPressedThisFrame || (tilted && !upDown && stick.x < 0f)) Step(-1);
                if (pad.dpad.right.wasPressedThisFrame || (tilted && !upDown && stick.x > 0f)) Step(1);
                if (pad.buttonSouth.wasPressedThisFrame) Activate(selected);
                goBack |= pad.buttonEast.wasPressedThisFrame;
                stickWasHeld = stickHeld;
            }
            if (goBack && active) back?.Invoke();
        }

        /// <summary>Up or down a row. In a grid, that's a whole row of cards (wrapping round, same column).</summary>
        void Move(int by)
        {
            if (rows.Length == 0) return;
            if (columns <= 1)
            {
                Select((selected + by + rows.Length) % rows.Length);
                return;
            }
            int gridRows = (rows.Length + columns - 1) / columns;
            int row = (selected / columns + by + gridRows) % gridRows;
            Select(Mathf.Min(row * columns + selected % columns, rows.Length - 1));
        }

        /// <summary>Left or right: the next card in a grid (lists ignore it).</summary>
        void Step(int by)
        {
            if (columns > 1 && rows.Length > 0) Select((selected + by + rows.Length) % rows.Length);
        }

        void Select(int i)
        {
            selected = i;
            for (int j = 0; j < rows.Length; j++)
            {
                bool on = j == i;
                bool soon = entries[j].comingSoon;
                var r = rows[j];
                // .menu-item.selected: amber bar, yellow left edge, CRT-black text, no glow.
                r.background.color = on ? (soon ? Palette.AmberDim : Palette.Amber) : Color.clear;
                r.edge.color = on ? Palette.Yellow : Color.clear;
                if (r.arrow) r.arrow.enabled = on;
                r.label.color = on ? Palette.Crt : soon ? Palette.AmberDim : Palette.Amber;
                r.label.fontSharedMaterial = on || soon ? plain : amberGlow;
                if (r.tag) r.tag.color = on ? Palette.Crt : soon ? Palette.Blush : Palette.TealGlow;
            }
            if (hint && i < entries.Count) hint.text = entries[i].hint ?? pageHint;
        }

        void Activate(int i)
        {
            if (!active || i >= entries.Count) return;
            Select(i);
            var entry = entries[i];
            if (entry.comingSoon)
            {
                rows[i].shake = 0f; // not yet: a little shake, nothing else happens
                return;
            }
            entry.chosen?.Invoke();
        }

        // --- The pages ----------------------------------------------------------------------------

        static Entry Choice(string label, Action chosen, string value = null) => new Entry { label = label, chosen = chosen, value = value };
        static Entry NotYet(string label) => new Entry { label = label, comingSoon = true };

        void ShowMain() => ShowPage(new List<Entry>
        {
            Choice("TRAINING", () => TrainingChosen?.Invoke()),
            Choice("LOCAL", ShowLocal, "›"),
            Choice("ONLINE", ShowOnline, "›"),
            Choice("SETTINGS", ShowControls),
        }, "↑↓ select · ENTER / Ⓐ choose", null);

        void ShowLocal() => ShowPage(new List<Entry>
        {
            Choice("DEATH MATCH", () => ShowSizes(GameMode.DeathMatch), "›"),
            Choice("CAPTURE THE FLAG", () => ShowSizes(GameMode.CaptureTheFlag), "›"),
            Choice("BACK", ShowMain),
        }, "LOCAL · ↑↓ select · ESC / Ⓑ back", ShowMain);

        void ShowOnline() => ShowPage(new List<Entry> { NotYet("DEATH MATCH"), NotYet("CAPTURE THE FLAG"), Choice("BACK", ShowMain) },
            "ONLINE · ↑↓ select · ESC / Ⓑ back", ShowMain);

        /// <summary>Free for all, 1v1 or 3v3 (capture the flag is team only).</summary>
        void ShowSizes(GameMode mode)
        {
            pending = new MatchSetup { mode = mode };
            var list = new List<Entry>();
            void Size(string label, TeamSize size) => list.Add(Choice(label, () => { pending.size = size; JoinLobby(); }));
            if (mode == GameMode.DeathMatch) Size("FREE FOR ALL", TeamSize.FreeForAll);
            Size("1V1", TeamSize.OneVsOne);
            Size("3V3", TeamSize.ThreeVsThree);
            list.Add(Choice("BACK", ShowLocal));
            ShowPage(list, (mode == GameMode.DeathMatch ? "DEATH MATCH" : "CAPTURE THE FLAG") + " · ESC / Ⓑ back", ShowLocal);
        }

        // --- The lobby ----------------------------------------------------------------------------

        void JoinLobby()
        {
            pending.fighter = Fighters.Chosen;
            lobby = new Lobby(pending, TrainingMap);
            ShowLobby();
        }

        void LeaveLobby()
        {
            lobby = null;
            ShowSizes(pending.mode);
        }

        /// <summary>The maps up for the vote (choosing one is your vote), your fighter, READY and LEAVE.</summary>
        void ShowLobby()
        {
            var list = new List<Entry>();
            for (int i = 0; i < lobby.maps.Count; i++)
            {
                int m = i;
                list.Add(Choice(Maps.Label(lobby.maps[i]), () => lobby.Vote(m), "0"));
            }
            if (lobby.maps.Count == 0) list.Add(NotYet("NO MAPS FOR THIS MODE"));
            int fighterRow = list.Count;
            list.Add(Choice("FIGHTER", ShowFighters, Fighters.Chosen.name));
            list.Add(Choice("READY", () => lobby.SetReady()));
            list.Add(Choice("LEAVE", LeaveLobby));
            list[fighterRow].hint = Fighters.Chosen.specialName + " · " + Fighters.Chosen.blurb;
            ShowPage(list, "", LeaveLobby);
            onLobbyPage = true;
            RefreshLobby();
        }

        /// <summary>Count down, let the others vote, and start the match on the winning map.</summary>
        void TickLobby()
        {
            if (lobby == null || !active) return;
            if (lobby.Tick(Time.unscaledDeltaTime))
            {
                pending.map = lobby.Winner();
                pending.fighter = Fighters.Chosen;
                lobby = null;
                MatchChosen?.Invoke(pending);
                return;
            }
            if (onLobbyPage) RefreshLobby();
        }

        /// <summary>Each map's vote count (yours in yellow), and the countdown in the hint line.</summary>
        void RefreshLobby()
        {
            for (int i = 0; i < lobby.maps.Count && i < rows.Length; i++)
            {
                if (!rows[i].tag) continue;
                rows[i].tag.text = lobby.VotesFor(i).ToString();
                rows[i].tag.color = i == selected ? Palette.Crt : i == lobby.YourVote ? Palette.Yellow : Palette.TealGlow;
            }
            int ready = lobby.maps.Count + 1;
            if (ready < rows.Length) rows[ready].label.text = lobby.Ready ? "  READY *" : "  READY";
            string text = $"{pending.Title} · {Mathf.CeilToInt(lobby.SecondsLeft)}";
            if (hint && (selected >= entries.Count || entries[selected].hint == null)) hint.text = text;
            pageHint = text;
        }

        /// <summary>
        /// The fighters as a grid of cards, each with a picture of its ship and its name; the hint line under the grid
        /// gives the selected one's special. Choosing one goes back to the lobby.
        /// </summary>
        void ShowFighters()
        {
            var list = new List<Entry>();
            foreach (var fighter in Fighters.All)
            {
                var f = fighter;
                var entry = Choice(f.name, () =>
                {
                    Fighters.Chosen = f;
                    pending.fighter = f;
                    ShowLobby();
                });
                entry.hint = f.specialName + " · " + f.blurb;
                list.Add(entry);
            }
            ShowGrid(list, 3, ShowLobby);
            // Start on the fighter you had last time.
            Select(Array.IndexOf(Fighters.All, Fighters.Chosen));
        }

        /// <summary>Swap the rows for a grid of picture cards, so many to a row.</summary>
        void ShowGrid(List<Entry> page, int perRow, Action onBack)
        {
            onLobbyPage = false;
            entries.Clear();
            entries.AddRange(page);
            back = onBack;
            columns = perRow;
            foreach (Transform old in rowsHolder) Destroy(old.gameObject);

            const float padX = 34f, gap = 8f, cardHeight = 88f;
            float cardWidth = (MenuPixels.x - padX * 2f - gap * (perRow - 1)) / perRow;
            rows = new Row[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                float x = padX + (i % perRow) * (cardWidth + gap);
                float top = RowsTop + (i / perRow) * (cardHeight + gap);
                rows[i] = BuildCard(rowsHolder, i, x, top, cardWidth, cardHeight);
            }
            float y = RowsTop + ((entries.Count + perRow - 1) / perRow) * (cardHeight + gap) - gap;
            pageHint = "";
            hint.text = "";
            ((RectTransform)hint.transform).offsetMax = new Vector2(0f, -(y + 6f));
            ((RectTransform)hint.transform).offsetMin = new Vector2(padX, -(y + 6f) - 21f);
            Select(0);
        }

        /// <summary>Swap the rows for a new page's, starting on its top row.</summary>
        void ShowPage(List<Entry> page, string hintText, Action onBack)
        {
            onLobbyPage = false;
            columns = 1;
            entries.Clear();
            entries.AddRange(page);
            back = onBack;
            foreach (Transform old in rowsHolder) Destroy(old.gameObject);

            // .menu-items: 14px below the title; rows 2px apart.
            const float padX = 34f, height = 25f;
            float y = RowsTop;
            rows = new Row[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                rows[i] = BuildRow(rowsHolder, i, padX, y, height);
                y += height + 2f;
            }
            // .hint: 10px below, 14px Courier Prime, dim amber.
            pageHint = hintText;
            hint.text = hintText;
            ((RectTransform)hint.transform).offsetMax = new Vector2(0f, -(y - 2f + 10f));
            ((RectTransform)hint.transform).offsetMin = new Vector2(padX, -(y - 2f + 10f) - 21f);
            Select(0);
        }

        const float RowsTop = 32f + 64f + 4f + 14f;

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
            var frameMat = AlwaysLit(Palette.TechToon(Color.white, FrameTexture()));
            var caseMat = AlwaysLit(Palette.TechToon(Palette.Case));
            var lidMat = AlwaysLit(Palette.TechToon(Palette.Lid));
            var hingeMat = AlwaysLit(Palette.TechToon(Palette.Hinge));
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
                Vector3.one * 0.04f, AlwaysLit(Palette.TechToon(Palette.CraftAccent)));

            // Two thruster pods underneath, each with a flame coming out of its nozzle.
            flameMaterial = new Material(Shader.Find("SpaceCowboy/Thruster"));
            flameMaterial.SetColor("_Color", Palette.Amber);
            var flameMesh = FlameMesh();
            flames = new Transform[2];
            embers = new ParticleSystem[2];
            var emberMaterial = new Material(Shader.Find("SpaceCowboy/Spark"));
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Palette.Shape(PrimitiveType.Cylinder, "Thruster", t, new Vector3(side * 0.27f, -Frame.y / 2f - 0.06f, 0.03f), new Vector3(0.14f, 0.07f, 0.14f), hingeMat);
                var flame = new GameObject("Flame");
                flame.transform.SetParent(t, false);
                flame.transform.localPosition = new Vector3(side * 0.27f, -Frame.y / 2f - 0.13f, 0.03f); // the bottom of the pod
                flame.AddComponent<MeshFilter>().sharedMesh = flameMesh;
                flame.AddComponent<MeshRenderer>().sharedMaterial = flameMaterial;
                flames[i] = flame.transform;
                embers[i] = Embers(t, flame.transform.localPosition, emberMaterial);
            }
            SetThrust(1f);

            // It's held close to the camera: no shadows (they'd land far below, out of place).
            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// How hard the thrusters push: 1 hovering (short, flickering flames), more while it takes off
        /// (longer, brighter flames, spitting sparks that are left behind as it climbs).
        /// </summary>
        public void SetThrust(float power)
        {
            float wobble = 1f + Mathf.Sin(Time.unscaledTime * 31f) * 0.06f + Mathf.Sin(Time.unscaledTime * 17f) * 0.05f;
            foreach (var flame in flames)
                flame.localScale = new Vector3(NozzleRadius, IdleFlame * power * wobble, NozzleRadius);
            flameMaterial.SetFloat(IntensityId, 1.2f + 0.35f * power);
            foreach (var ember in embers)
            {
                var emission = ember.emission;
                emission.rateOverTime = Mathf.Max(0f, power - 1.5f) * 30f; // none while hovering
            }
        }

        /// <summary>
        /// Embers spat out of a nozzle while it's pushing hard. They're left in the world, so they trail behind
        /// as the screen climbs away. Sized in centimetres: the screen is right in front of the camera.
        /// </summary>
        static ParticleSystem Embers(Transform drone, Vector3 nozzle, Material material)
        {
            var go = new GameObject("Embers");
            go.transform.SetParent(drone, false);
            go.transform.localPosition = nozzle;
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // set up before it plays

            var main = particles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.016f);
            main.startColor = new ParticleSystem.MinMaxGradient(ShipFx.SparkDeep, ShipFx.SparkHot);
            main.maxParticles = 120;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = NozzleRadius * 0.6f;
            shape.rotation = new Vector3(90f, 0f, 0f); // the cone points down, out of the nozzle

            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var look = go.GetComponent<ParticleSystemRenderer>();
            look.sharedMaterial = material;
            look.renderMode = ParticleSystemRenderMode.Billboard;
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particles.Play();
            return particles;
        }

        /// <summary>
        /// A flame's shape: a cone pointing down, 1 across at the nozzle and 1 long (scaled to size), slightly
        /// swollen near the top. uv.x goes around it, uv.y from the nozzle (0) to the tip (1), for the shader.
        /// </summary>
        static Mesh FlameMesh()
        {
            const int around = 20, along = 10;
            var points = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int j = 0; j <= along; j++)
            {
                float v = j / (float)along;
                float radius = (1f - v) * (1f + 0.35f * Mathf.Sin(v * Mathf.PI)); // swells, then narrows to a point
                for (int i = 0; i <= around; i++)
                {
                    float a = 2f * Mathf.PI * i / around;
                    var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    points.Add(outward * radius + Vector3.down * v);
                    normals.Add(outward);
                    uvs.Add(new Vector2(i / (float)around, v));
                }
            }
            int row = around + 1;
            for (int j = 0; j < along; j++)
                for (int i = 0; i < around; i++)
                {
                    int p = j * row + i, q = p + 1, r = p + row, s = r + 1;
                    triangles.Add(p); triangles.Add(r); triangles.Add(q);
                    triangles.Add(q); triangles.Add(r); triangles.Add(s);
                }
            var mesh = new Mesh { name = "Thruster Flame" };
            mesh.SetVertices(points);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Material AlwaysLit(Material m)
        {
            m.SetFloat("_BandMid", -2f);
            m.SetFloat("_BandLit", -2f);
            return m;
        }

        static Texture2D frameTexture;

        /// <summary>The frame's front: beige with scuffs, tape on a corner, a few stickers and a grinning emoticon.
        /// Painted once and shared, so the in-game settings window can wear the same bezel.</summary>
        public static Texture2D FrameTexture()
        {
            if (frameTexture) return frameTexture;
            frameTexture = PaintFrame();
            return frameTexture;
        }

        static Texture2D PaintFrame()
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

            // Two layers on the same screen: the menu's pages, and the settings panel.
            mainPage = UI.Rect("Main Page", canvasRect, Vector2.zero, Vector2.one);
            controls = new ControlsPanel(canvasRect, null, time);
            controls.Closed += () =>
            {
                showingControls = false;
                mainPage.gameObject.SetActive(true);
            };
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

            // The rows change with the page; they're built by ShowPage.
            rowsHolder = UI.Rect("Rows", page, Vector2.zero, Vector2.one);
            hint = Line(page, "Hint", "", Fonts.MonoRegular, 14f, Palette.AmberDim, padX, RowsTop, 21f);
            hint.rectTransform.offsetMax = new Vector2(-padX, hint.rectTransform.offsetMax.y);
            hint.enableAutoSizing = true; // long hints (a fighter's special) shrink to fit the line
            hint.fontSizeMin = 9f;
            hint.fontSizeMax = 14f;

            AddScreenEffects(canvasRect);

            ShowMain();
        }

        static Texture2D scanTexture, vignetteTexture;

        /// <summary>
        /// Scanlines and a vignette over everything on a screen, like an old CRT. Add them last, so they sit on top.
        /// The start menu and the in-game settings window both use this, so they look like the same screen.
        /// </summary>
        public static void AddScreenEffects(RectTransform screen)
        {
            if (!scanTexture)
            {
                var scan = new TexturePainter(1, 3, Color.clear);
                scan.FillRect(new UnityEngine.Rect(0f, 0f, 1f, 1f), new Color(0f, 0f, 0f, 0.28f));
                scanTexture = scan.ToTexture(filter: FilterMode.Point);
                scanTexture.wrapMode = TextureWrapMode.Repeat;

                var vignette = new TexturePainter(128, 83, Color.clear);
                vignette.Shade((u, v) =>
                {
                    // radial-gradient(ellipse at center, transparent 50%, rgba(0,0,0,0.55) 100%), reaching the corners.
                    float d = Mathf.Sqrt(((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f);
                    return new Color(0f, 0f, 0f, 0.55f * Mathf.Clamp01((d - 0.5f) / 0.5f));
                });
                vignetteTexture = vignette.ToTexture();
            }

            var scanImage = UI.Rect("Scanlines", screen, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            scanImage.texture = scanTexture;
            scanImage.uvRect = new UnityEngine.Rect(0f, 0f, 1f, MenuPixels.y / 3f);
            scanImage.raycastTarget = false;

            var vignetteImage = UI.Rect("Vignette", screen, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            vignetteImage.texture = vignetteTexture;
            vignetteImage.raycastTarget = false;
        }

        void ShowControls()
        {
            showingControls = true;
            mainPage.gameObject.SetActive(false);
            controls.Open();
        }

        Row BuildRow(RectTransform canvas, int i, float padX, float top, float height)
        {
            var item = entries[i];
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
                // // .tag: 700 12px Oswald, letter-spacing 0.12em, blush red
                // row.tag = UI.Text("Tag", row.rect, "COMING SOON", 12f, Palette.Blush);
                // row.tag.font = Fonts.Condensed;
                // row.tag.characterSpacing = 12f;
            }
            else if (!string.IsNullOrEmpty(item.value))
            {
                // .value: 25px VT323, teal glow
                row.tag = UI.Text("Value", row.rect, item.value, 25f, Palette.TealGlow);
                row.tag.font = Fonts.Lcd;
            }
            if (row.tag)
            {
                row.tag.margin = new Vector4(10f, 0f, 10f, 0f);
                row.tag.alignment = TextAlignmentOptions.MidlineRight;
            }

            // The mouse: hovering selects, clicking chooses.
            UI.Choosable(row.rect.gameObject, () => { if (active) Select(i); }, () => Activate(i));
            return row;
        }

        /// <summary>
        /// One card in a grid: the ship's picture, the name under it, and a thin dim frame. Selected, it fills amber
        /// like a selected row. The mouse works the same as on rows.
        /// </summary>
        Row BuildCard(RectTransform canvas, int i, float left, float top, float width, float height)
        {
            var item = entries[i];
            var holder = Anchored(UI.Rect(item.label, canvas, new Vector2(0f, 1f), new Vector2(0f, 1f)), left, top, width, height);
            var row = new Row { rect = UI.Rect("Card", holder, Vector2.zero, Vector2.one) };
            row.background = row.rect.gameObject.AddComponent<Image>();

            // The frame: four 1px lines, so an unselected card still reads as a card.
            var frameColor = new Color(Palette.AmberDim.r, Palette.AmberDim.g, Palette.AmberDim.b, 0.6f);
            void FrameLine(string name, Vector2 min, Vector2 max, Vector2 size)
            {
                var line = UI.Rect(name, row.rect, min, max);
                line.sizeDelta = size;
                line.pivot = new Vector2(min.x == max.x ? min.x : 0.5f, min.y == max.y ? min.y : 0.5f);
                var image = line.gameObject.AddComponent<Image>();
                image.color = frameColor;
                image.raycastTarget = false;
            }
            FrameLine("Top", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 1f));
            FrameLine("Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 1f));
            FrameLine("Left", Vector2.zero, new Vector2(0f, 1f), new Vector2(1f, 0f));
            FrameLine("Right", new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0f));

            // The left edge, yellow when selected (like the rows).
            var edge = UI.Rect("Edge", row.rect, Vector2.zero, new Vector2(0f, 1f));
            edge.pivot = new Vector2(0f, 0.5f);
            edge.sizeDelta = new Vector2(3f, 0f);
            row.edge = edge.gameObject.AddComponent<Image>();
            row.edge.raycastTarget = false;

            // The picture fills the card above the name.
            const float nameHeight = 22f;
            if (FighterPicture)
            {
                var area = UI.Rect("Picture Area", row.rect, Vector2.zero, Vector2.one);
                area.offsetMin = new Vector2(4f, nameHeight);
                area.offsetMax = new Vector2(-4f, -3f);
                var pictureRect = UI.Rect("Picture", area, Vector2.zero, Vector2.one);
                var picture = pictureRect.gameObject.AddComponent<RawImage>();
                picture.texture = FighterPicture;
                picture.raycastTarget = false;
                var fit = pictureRect.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = FighterPicture.width / (float)FighterPicture.height;
            }

            row.label = UI.Text("Name", row.rect, item.label, 17f, Palette.Amber);
            row.label.font = Fonts.Mono;
            row.label.alignment = TextAlignmentOptions.Center;
            row.label.rectTransform.anchorMin = Vector2.zero;
            row.label.rectTransform.anchorMax = new Vector2(1f, 0f);
            row.label.rectTransform.pivot = new Vector2(0.5f, 0f);
            row.label.rectTransform.offsetMin = new Vector2(0f, 2f);
            row.label.rectTransform.offsetMax = new Vector2(0f, nameHeight);

            UI.Choosable(row.rect.gameObject, () => { if (active) Select(i); }, () => Activate(i));
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

        /// <summary>Place a box by its left edge and top (or middle), in CSS pixels from the top-left.</summary>
        public static RectTransform Anchored(RectTransform rect, float left, float top, float width, float height, bool middle = false)
        {
            rect.pivot = new Vector2(0f, middle ? 0.5f : 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
            return rect;
        }

        /// <summary>One line of text, its box starting at (x, top) in CSS pixels from the top-left.</summary>
        public static TextMeshProUGUI Line(RectTransform canvas, string name, string text, TMP_FontAsset font, float size, Color color, float x, float top, float height)
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
