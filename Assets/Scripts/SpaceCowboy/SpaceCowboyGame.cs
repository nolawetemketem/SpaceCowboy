using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace SpaceCowboy
{
    /// <summary>
    /// Runs Training mode, like the web game's Game.ts:
    ///   Menu:     the camera slowly circles the field; Ed's floating screen hovers in front of it.
    ///   Leaving:  TRAINING was chosen; the screen dips as its thrusters spool up, then launches straight up out of view.
    ///   Training: the camera swoops down behind the craft and you play. R resets the craft.
    ///             MAIN MENU on the settings (Esc or the controller's Menu button) goes back to the menu.
    /// </summary>
    public class SpaceCowboyGame : MonoBehaviour
    {
        enum State { Menu, Leaving, Training }

        public HoverCraft craft;
        public CraftInput input;
        public FollowCamera follow;
        public TrainingArena arena;
        public TimeOfDay timeOfDay;

        [Header("Look")]
        [Tooltip("Paint the Fighter like the web game: cream body, red engines.")]
        public bool webFighterColors = true;
        public float fieldOfView = 65f;

        [Header("Practice")]
        [Tooltip("How many practice ships to shoot at in space flight. 0 = none.")]
        public int practiceTargets = 8;

        [Header("Start menu")]
        [Tooltip("Skip the menu and start Training straight away (handy while tuning).")]
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
        float menuTime, leavingTime;

        void Start()
        {
            cam = follow.GetComponent<Camera>();
            cam.fieldOfView = fieldOfView;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 2500f); // see out to the haze at the desert's edge
            EnsureEventSystem();
            if (webFighterColors) PaintFighter();

            hud = Hud.Create();
            hud.Controls.Closed += input.Clear; // presses made in the settings don't reach the ship
            hud.MainMenuChosen += BackToMenu;
            arena.Build();
            if (timeOfDay) timeOfDay.UseField(arena.transform);

            // Any ship can carry a ShipWeapon. If the Fighter doesn't have one in the scene, give it the default.
            // Hovercraft mode (unused: Space Flight is the only mode now). Uncomment to bring it back.
            // if (craft.spaceFlight)
            {
                var weapon = craft.GetComponent<ShipWeapon>();
                if (!weapon) weapon = craft.gameObject.AddComponent<ShipWeapon>();
                hud.ShowAim(weapon, cam);
                MakeDestructible();
                if (practiceTargets > 0) TargetRange.Create(arena, craft, practiceTargets);
            }

            menu = StartMenu.Create(cam, timeOfDay ? timeOfDay.setting : TimeOfDaySetting.Auto);
            menu.TrainingChosen += LeaveMenu;
            if (timeOfDay) menu.TimeOfDayChosen += timeOfDay.Set;

            // The craft waits at its start spot, floating, while the menu is up.
            craft.ResetTo(arena.CraftStartPosition, arena.CraftStartRotation);
            craft.controlsEnabled = false;
            follow.enabled = false;
            state = State.Menu;
            if (skipMenu) StartTraining(fromMenu: false);
        }

        void Update()
        {
            if (state != State.Training) return;

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
                craft.ResetTo(arena.CraftStartPosition, arena.CraftStartRotation);
                follow.Snap();
            }
        }

        void LateUpdate()
        {
            if (state == State.Training) return;
            menuTime += Time.deltaTime;

            // Circle the field, looking at its middle.
            float angle = menuTime * orbitSpeed;
            Vector3 centre = arena.Centre;
            cam.transform.position = centre + new Vector3(Mathf.Sin(angle) * orbitRadius, orbitHeight, Mathf.Cos(angle) * orbitRadius);
            cam.transform.LookAt(centre + Vector3.up * 6f);
            PlaceScreen();

            if (state != State.Leaving) return;
            leavingTime += Time.deltaTime;
            if (leavingTime >= flyAwaySeconds) StartTraining(fromMenu: true);
        }

        /// <summary>TRAINING was chosen on the menu: the screen flies away, then Training starts.</summary>
        void LeaveMenu()
        {
            if (state != State.Menu) return;
            menu.Lock();
            state = State.Leaving;
            leavingTime = 0f;
        }

        /// <summary>MAIN MENU from the settings in Training: the floating screen comes back and the ship waits at its start spot.</summary>
        void BackToMenu()
        {
            if (state != State.Training) return;
            state = State.Menu;
            hud.Show(false);
            follow.enabled = false;
            craft.ResetTo(arena.CraftStartPosition, arena.CraftStartRotation);
            craft.controlsEnabled = false;
            input.Clear();
            menu.gameObject.SetActive(true);
            menu.Unlock();
        }

        /// <summary>Start (or restart) Training: craft at its start spot.</summary>
        void StartTraining(bool fromMenu)
        {
            Vector3 menuPosition = cam.transform.position;
            Quaternion menuRotation = cam.transform.rotation;
            Vector3 menuLook = arena.Centre + Vector3.up * 6f;

            state = State.Training;
            menu.gameObject.SetActive(false);
            craft.ResetTo(arena.CraftStartPosition, arena.CraftStartRotation);
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
        /// chosen it dips for a moment as its thrusters spool up, then launches straight up out of view, jets flaring.
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
            float jet = 0.85f + Mathf.Sin(t * 12f) * 0.1f; // the jets flicker
            float jetSize = 1f;

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
                jet = 1f;
                jetSize = 1f + 1.5f * Mathf.Clamp01(f / 0.25f) + 1.5f * launch;
            }

            menu.transform.position = cam.transform.TransformPoint(offset);
            menu.transform.rotation = cam.transform.rotation * Quaternion.Euler(tilt);
            foreach (var j in menu.Jets)
            {
                var c = j.sharedMaterial.color;
                j.sharedMaterial.color = new Color(c.r, c.g, c.b, jet);
                j.transform.localScale = new Vector3(0.11f, 0.11f, 1f) * jetSize;
            }
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
                craft.ResetTo(arena.CraftStartPosition, arena.CraftStartRotation);
                follow.Snap();
                destruction.Materialize();
            };
        }

        /// <summary>Cream body, red engines (the web game's fighterPartColor). Only while playing; the assets stay as they are.</summary>
        void PaintFighter()
        {
            var body = Palette.Toon(Palette.Craft);
            var accent = Palette.Toon(Palette.CraftAccent);
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
