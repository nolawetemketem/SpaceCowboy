using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace SpaceCowboy
{
    /// <summary>
    /// Runs the game, like the web game's Game.ts. This scene holds the ship, camera, sun, HUD and menu;
    /// the place you fly in is a map, a separate scene from Assets/Maps loaded on top of this one (see ArenaMap).
    ///   Menu:     the camera slowly circles the map; Ed's floating screen hovers in front of it.
    ///   Leaving:  TRAINING was chosen; the screen dips as its thrusters spool up, then launches straight up out of view.
    ///   Playing:  the camera swoops down behind the craft and you play: Training, or a match (see Match).
    ///             R resets the craft. MAIN MENU on the settings (Esc or the controller's Menu button) goes back.
    ///   Results:  a match has ended; its results are on screen (PLAY AGAIN or MAIN MENU).
    /// Your fighter's special (SpecialSlot) works in Training and in matches.
    /// </summary>
    public class SpaceCowboyGame : MonoBehaviour
    {
        enum State { Menu, Leaving, Playing, Results }

        public HoverCraft craft;
        public CraftInput input;
        public FollowCamera follow;
        public TimeOfDay timeOfDay;

        [Header("Maps")]
        [Tooltip("The training area: the map behind the start menu, and where TRAINING takes place (a scene name in Assets/Maps). " +
                 "The other maps are for matches only.")]
        public string menuMap = "Desert Training";
        [Tooltip("Testing: go straight into Training on this map (a scene name in Assets/Maps). Empty = start on the menu.")]
        public string testMap = "";

        /// <summary>Set when Play is pressed inside a map scene: go straight into Training on that map.</summary>
        public static string MapToTest;

        [Header("Look")]
        [Tooltip("Paint the Fighter like the web game: cream body, red engines.")]
        public bool webFighterColors = true;
        public float fieldOfView = 65f;

        [Header("Start menu")]
        [Tooltip("Skip the menu and start Training straight away on the menu map (handy while tuning).")]
        public bool skipMenu;
        [Tooltip("The camera circles the field at this distance, height, and speed (radians/s).")]
        public float orbitRadius = 250f;
        public float orbitHeight = 65f;
        public float orbitSpeed = 0.05f;
        [Tooltip("The floating screen fills this share of the view.")]
        [Range(0.2f, 0.9f)] public float screenFill = 0.6f;
        [Tooltip("Seconds the floating screen takes to fly away once a mode is chosen.")]
        public float flyAwaySeconds = 0.9f;
        [Tooltip("Seconds for the camera to swoop from the overhead shot down to the craft.")]
        public float glideSeconds = 1.8f;

        State state;
        Camera cam;
        Hud hud;
        StartMenu menu;
        ShipDestruction destruction;
        ShipWeapon weapon;
        float menuTime, leavingTime;

        ArenaMap map;
        string mapScene;
        TrainingBots trainingBots;
        CaptureTheFlag flagGame;
        Match match;
        MatchSetup setup;
        SpecialSlot special;
        bool loading;

        /// <summary>Where you start and come back: your team's spawn in a match, the map's start spot otherwise.</summary>
        Pose YourSpawn => match ? match.YourSpawn : new Pose(map.PlayerStartPosition, map.PlayerStartRotation);

        void GoToSpawn()
        {
            var pose = YourSpawn;
            craft.ResetTo(pose.position, pose.rotation);
        }

        IEnumerator Start()
        {
            cam = follow.GetComponent<Camera>();
            cam.fieldOfView = fieldOfView;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 6000f); // see across the biggest maps
            EnsureEventSystem();
            if (webFighterColors) PaintFighter();

            hud = Hud.Create(timeOfDay);
            hud.Controls.Closed += input.Clear; // presses made in the settings don't reach the ship
            hud.MainMenuChosen += BackToMenu;

            // Any ship can carry a ShipWeapon. If the Fighter doesn't have one in the scene, give it the default.
            weapon = craft.GetComponent<ShipWeapon>();
            if (!weapon) weapon = craft.gameObject.AddComponent<ShipWeapon>();
            hud.ShowAim(weapon, cam);
            MakeDestructible();
            var yourHealth = craft.GetComponent<Damageable>();
            yourHealth.regenPerSecond = 12f; // back to full a while after the shooting stops
            hud.TrackHealth(yourHealth);

            special = SpecialSlot.Attach(craft, Fighters.Chosen, hud, () => map, Enemies);
            hud.Results.PlayAgain += PlayAgain;
            hud.Results.MainMenu += BackToMenu;

            menu = StartMenu.Create(cam, timeOfDay);
            menu.TrainingMap = menuMap;
            menu.FighterPicture = FighterPortrait.Take(craft.transform.Find("BankPivot"));
            menu.TrainingChosen += () => StartCoroutine(Begin(new MatchSetup { mode = GameMode.Training, map = menuMap, fighter = Fighters.Chosen }));
            menu.MatchChosen += chosen => StartCoroutine(Begin(chosen));
            menu.gameObject.SetActive(false);
            craft.controlsEnabled = false;
            follow.enabled = false;

            // A map scene's Play button, or the Testing field, goes straight into Training.
            string straightTo = !string.IsNullOrEmpty(MapToTest) ? MapToTest : !string.IsNullOrEmpty(testMap) ? testMap : skipMenu ? menuMap : null;
            MapToTest = null;
            yield return LoadMap(straightTo ?? menuMap);
            if (!map) yield break;

            // The craft waits at its start spot, floating, while the menu is up.
            craft.ResetTo(map.PlayerStartPosition, map.PlayerStartRotation);
            menu.gameObject.SetActive(true);
            state = State.Menu;
            if (straightTo != null)
            {
                SetUpMode(new MatchSetup { mode = GameMode.Training, map = mapScene, fighter = Fighters.Chosen });
                StartPlaying(fromMenu: false);
            }
        }

        /// <summary>The ships you're up against: the other team in a match, the bots in Training (for the Spy).</summary>
        IEnumerable<Transform> Enemies()
        {
            if (match) return match.EnemiesOfYou;
            if (trainingBots) return trainingBots.Ships;
            return Enumerable.Empty<Transform>();
        }

        /// <summary>
        /// Set up what this mode needs on the loaded map: bots and any flags in Training,
        /// the bots and clock in a match, flags in capture the flag.
        /// </summary>
        void SetUpMode(MatchSetup chosen)
        {
            TearDownMode();
            setup = chosen;
            special.SetFighter(chosen.fighter);
            bool flags = map.flagA && map.flagB && chosen.mode != GameMode.DeathMatch;
            if (chosen.mode == GameMode.Training && map.trainingBots)
                trainingBots = TrainingBots.Create(map, craft, chosen.fighter);
            if (flags) flagGame = CaptureTheFlag.Create(map, craft, hud);
            if (chosen.mode != GameMode.Training)
            {
                match = Match.Create(chosen, map, craft, hud, flagGame);
                match.Ended += ShowResults;
            }
        }

        void TearDownMode()
        {
            if (trainingBots) Destroy(trainingBots.gameObject);
            if (flagGame) Destroy(flagGame.gameObject);
            if (match) Destroy(match.gameObject);
            trainingBots = null;
            flagGame = null;
            match = null;
            hud.HideMatch();
        }

        /// <summary>Training or a match was chosen on the menu: load its map if needed, set it up, then fly in.</summary>
        IEnumerator Begin(MatchSetup chosen)
        {
            if (state != State.Menu || loading) yield break;
            menu.Lock();
            yield return LoadMap(chosen.map);
            SetUpMode(chosen);
            GoToSpawn();
            LeaveMenu();
        }

        void ShowResults(string heading, List<ResultsPanel.Line> lines)
        {
            state = State.Results;
            craft.controlsEnabled = false;
            if (flagGame) flagGame.enabled = false;
            if (hud.Controls.IsOpen) hud.Controls.Close();
            hud.SetMarkers(null, Color.white);
            hud.Results.Show(heading, setup.Title + " · " + Maps.Label(mapScene), lines, setup.mode == GameMode.CaptureTheFlag);
        }

        /// <summary>The same match again, straight away, on the same map.</summary>
        void PlayAgain()
        {
            if (state != State.Results) return;
            hud.Results.Hide();
            SetUpMode(setup);
            GoToSpawn();
            follow.Snap();
            craft.controlsEnabled = true;
            input.Clear();
            state = State.Playing;
        }

        /// <summary>
        /// Make this map the one you're in: unload the old map's scene and load the new one on top of this scene.
        /// Training bots are set up again for the new place. Does nothing if it's already loaded.
        /// </summary>
        IEnumerator LoadMap(string sceneName)
        {
            if (map && mapScene == sceneName) yield break;
            if (!Maps.All().Contains(sceneName))
            {
                Debug.LogError($"There's no map called \"{sceneName}\" in {Maps.Folder} (or it isn't in the build list: " +
                               "Space Cowboy ▸ Maps ▸ Update Build List).", this);
                if (map) yield break;
                var maps = Maps.All();
                if (maps.Count == 0) yield break;
                sceneName = maps[0];
            }

            loading = true;
            TearDownMode();
            if (!string.IsNullOrEmpty(mapScene) && SceneManager.GetSceneByName(mapScene).isLoaded)
                yield return SceneManager.UnloadSceneAsync(mapScene);
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            mapScene = sceneName;
            map = ArenaMap.In(SceneManager.GetSceneByName(sceneName));
            if (!map)
            {
                // Still playable: a map without an ArenaMap gets one at its origin.
                Debug.LogWarning($"The map \"{sceneName}\" has no ArenaMap, so the ship starts at its center. " +
                                 "Add one (see Space Cowboy ▸ Maps ▸ New Map for an example).", this);
                var holder = new GameObject("Arena (added by the game)");
                SceneManager.MoveGameObjectToScene(holder, SceneManager.GetSceneByName(sceneName));
                map = holder.AddComponent<ArenaMap>();
            }
            if (timeOfDay) timeOfDay.UseField(map.transform);
            loading = false;
        }

        void Update()
        {
            if (state == State.Results)
            {
                hud.Results.Tick();
                return;
            }
            if (state != State.Playing) return;

            // The settings open over the game without pausing it. The ship coasts while they're up.
            var controls = hud.Controls;
            bool wrecked = destruction && destruction.Wrecked;
            craft.controlsEnabled = !controls.IsOpen && !wrecked;
            if (controls.IsOpen)
            {
                controls.Tick();
                return;
            }
            if (SettingsPressed())
            {
                hud.OpenControls();
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Testing only: K blows up your ship. (Using the Inspector's menu for this pauses the game while the menu is open.)
            if (destruction && !wrecked && Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
                destruction.DestroyForTest();
#endif

            // Training safety valve. A destroyed ship is already on its way back.
            if (input.TakeResetCraft() && !wrecked)
            {
                if (flagGame) flagGame.PlayerReset();
                GoToSpawn();
                follow.Snap();
            }
        }

        void LateUpdate()
        {
            if (state == State.Playing || state == State.Results || !map) return;
            menuTime += Time.deltaTime;

            // Circle the map, looking at its middle (or wherever the map says, as far out as it says).
            float angle = menuTime * orbitSpeed;
            Vector3 centre = map.menuFocus ? map.menuFocus.position : map.Centre;
            float radius = map.menuOrbitRadius > 0f ? map.menuOrbitRadius : orbitRadius;
            float height = map.menuOrbitHeight > 0f ? map.menuOrbitHeight : orbitHeight;
            cam.transform.position = centre + new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);
            cam.transform.LookAt(centre + Vector3.up * 6f);
            PlaceScreen();

            if (state != State.Leaving) return;
            leavingTime += Time.deltaTime;
            if (leavingTime >= flyAwaySeconds) StartPlaying(fromMenu: true);
        }

        /// <summary>A map was chosen for Training on the menu: the screen flies away, then Training starts.</summary>
        void LeaveMenu()
        {
            if (state != State.Menu) return;
            menu.Lock();
            state = State.Leaving;
            leavingTime = 0f;
        }

        /// <summary>
        /// MAIN MENU (from the settings, or the results): back to the training area, where the floating screen
        /// comes back and the ship waits at its start spot.
        /// </summary>
        void BackToMenu()
        {
            if (state != State.Playing && state != State.Results) return;
            StartCoroutine(ReturnToMenu());
        }

        IEnumerator ReturnToMenu()
        {
            state = State.Menu;
            TearDownMode();
            hud.Show(false);
            follow.enabled = false;
            craft.controlsEnabled = false;
            input.Clear();
            menu.gameObject.SetActive(true);
            menu.Lock();
            yield return LoadMap(menuMap); // nothing to do if you were already in the training area
            craft.ResetTo(map.PlayerStartPosition, map.PlayerStartRotation);
            menu.Unlock();
        }

        /// <summary>Start playing (Training or a match): craft at its spawn.</summary>
        void StartPlaying(bool fromMenu)
        {
            Vector3 menuPosition = cam.transform.position;
            Quaternion menuRotation = cam.transform.rotation;
            Vector3 menuLook = (map.menuFocus ? map.menuFocus.position : map.Centre) + Vector3.up * 6f;

            state = State.Playing;
            menu.gameObject.SetActive(false);
            GoToSpawn();
            craft.controlsEnabled = true;
            input.Clear(); // drop presses made in the menu
            follow.enabled = true;
            follow.Snap();
            if (fromMenu)
            {
                // Stay on the overhead shot this frame; the swoop down starts from there.
                follow.GlideFrom(menuPosition, menuLook, glideSeconds);
                cam.transform.SetPositionAndRotation(menuPosition, menuRotation);
            }
            hud.Show(true);
        }

        /// <summary>
        /// Hold the floating screen in front of the camera, bobbing on its thrusters. Once a mode is
        /// chosen it dips for a moment as its thrusters spool up, then launches straight up out of view, thrusters roaring.
        /// </summary>
        void PlaceScreen()
        {
            float t = menuTime;
            // At distance d the view is 2·d·tan(fov/2) tall, so this distance makes the screen fill
            // screenFill of the view, whether the window is wide or tall.
            float halfFov = cam.fieldOfView * Mathf.Deg2Rad / 2f;
            float fitHeight = StartMenu.ScreenSize.y / screenFill / (2f * Mathf.Tan(halfFov));
            float fitWidth = StartMenu.ScreenSize.x / screenFill / (2f * Mathf.Tan(halfFov) * cam.aspect);
            float distance = Mathf.Max(fitHeight, fitWidth);

            // Lifted a little so the thrusters underneath stay in view too.
            var offset = new Vector3(0f, 0.035f + Mathf.Sin(t * 1.6f) * 0.012f, distance);
            var tilt = new Vector3(Mathf.Sin(t * 0.7f) * 0.015f, Mathf.Sin(t * 0.5f) * 0.02f, Mathf.Sin(t * 0.9f) * 0.02f) * Mathf.Rad2Deg;
            float thrust = 1f; // hovering

            if (state == State.Leaving)
            {
                float f = leavingTime / flyAwaySeconds;
                // The first quarter: a small dip, like a drone loading its thrusters.
                float dip = Mathf.Sin(Mathf.Clamp01(f / 0.25f) * Mathf.PI) * 0.035f;
                // Then it launches: slow at first, then fast, straight up (never away from the camera).
                float launch = Mathf.Clamp01((f - 0.2f) / 0.8f);
                launch *= launch * launch;
                offset += new Vector3(0.12f * launch, 1.3f * launch - dip, 0f);
                tilt += new Vector3(-4f * launch, 0f, -6f * launch); // the top leans a touch toward the camera as it climbs
                // The thrusters spool up during the dip, then roar as it launches.
                thrust = 1f + 2f * Mathf.Clamp01(f / 0.25f) + 3f * launch;
            }

            menu.transform.position = cam.transform.TransformPoint(offset);
            menu.transform.rotation = cam.transform.rotation * Quaternion.Euler(tilt);
            menu.SetThrust(thrust);
        }

        /// <summary>The Menu button on a controller (above the right stick), or Esc.</summary>
        static bool SettingsPressed()
        {
            var keys = Keyboard.current;
            var pad = Gamepad.current;
            return (keys != null && keys.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
        }

        /// <summary>The player's ship can be destroyed too: it blows apart, then is projected back at the start spot.</summary>
        void MakeDestructible()
        {
            if (!craft.GetComponent<Damageable>()) craft.gameObject.AddComponent<Damageable>();
            destruction = craft.GetComponent<ShipDestruction>();
            if (!destruction) destruction = craft.gameObject.AddComponent<ShipDestruction>();
            destruction.Setup(craft.transform.Find("BankPivot"));
            destruction.WreckGone += () =>
            {
                GoToSpawn(); // your team's spawn point in a match
                follow.Snap();
                destruction.Materialize();
            };
        }

        /// <summary>Cream body, red engines (the web game's fighterPartColor). Only while playing; the assets stay as they are.</summary>
        void PaintFighter()
        {
            var body = Palette.TechToon(Palette.Craft);
            var accent = Palette.TechToon(Palette.CraftAccent);
            var animator = craft.GetComponent<FighterAnimator>();
            foreach (var r in craft.GetComponentsInChildren<Renderer>(true))
            {
                bool engine = animator && ((animator.topEngine && r.transform.IsChildOf(animator.topEngine)) ||
                                           (animator.bottomEngine && r.transform.IsChildOf(animator.bottomEngine)));
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = engine ? accent : body;
                r.sharedMaterials = materials;
            }
        }

        /// <summary>The menu needs an EventSystem for the mouse; add one if the scene has none.</summary>
        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>()) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetParent(null);
        }
    }
}
