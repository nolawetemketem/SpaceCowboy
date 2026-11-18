using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceCowboy
{
    /// <summary>
    /// Marks a scene as a map, and tells the game where things go in it. Every map is its own scene in
    /// Assets/Maps; the game scene (Prototype) loads whichever one is chosen on top of itself, so the ship,
    /// camera, sun, HUD and menu stay the same and only the place changes.
    ///
    /// Put one on an object in the map scene and drag in its markers (empty objects). Everything is optional:
    ///   Player Start   where your ship starts and comes back in Training. Without one, a Training Arena on
    ///                  this object decides, or else this object's own spot.
    ///   Team spawns    where each team starts and respawns, for 1v1 and 3v3 (not used yet).
    ///   Flags          where each team's flag sits, for capture the flag (not used yet).
    ///   Play Area      the edges of the playable space, centered on this object (not enforced yet).
    /// The markers show in the Scene view: white = player start, amber = team A, teal = team B.
    /// Space Cowboy ▸ Maps ▸ New Map makes a scene with all of this set up.
    /// </summary>
    public class ArenaMap : MonoBehaviour
    {
        [Header("Training")]
        [Tooltip("Where your ship starts and comes back. Its blue arrow is the way the ship faces.")]
        public Transform playerStart;
        [Tooltip("Put bots out to fight in Training (the same bots as a free for all).")]
        [UnityEngine.Serialization.FormerlySerializedAs("practiceTargets")] // its old name, so saved maps keep their setting
        public bool trainingBots = true;

        [Header("Start menu")]
        [Tooltip("What the menu's camera circles. Empty = the middle of the map.")]
        public Transform menuFocus;
        [Tooltip("How far out and how high the menu's camera circles. 0 = the game's usual.")]
        public float menuOrbitRadius, menuOrbitHeight;

        [Header("Teams (1v1, 3v3)")]
        public Transform[] teamASpawns = new Transform[0];
        public Transform[] teamBSpawns = new Transform[0];

        [Header("Capture the flag")]
        [Tooltip("Each team's flag spot. With both set, the map plays capture the flag: a ring is built at each, " +
                 "facing the way the spot faces (its blue arrow), with the flag in the middle.")]
        public Transform flagA;
        public Transform flagB;

        [Header("Play area")]
        [Tooltip("The size of the playable space, centered on this object. A map with walls (a barrier sphere, a ring tube) uses its walls instead.")]
        public Vector3 playAreaSize = new Vector3(2600f, 1200f, 2600f);

        TrainingArena field;

        void Awake()
        {
            // A map can keep the old code-built Training field (goals and lines); it's built as the map loads.
            field = GetComponent<TrainingArena>();
            if (field) field.Build();
        }

        /// <summary>Where your ship starts in Training.</summary>
        public Vector3 PlayerStartPosition =>
            playerStart ? playerStart.position : field ? field.CraftStartPosition : GroundBelow(transform.position) + Vector3.up * 6f;

        public Quaternion PlayerStartRotation =>
            playerStart ? Level(playerStart.forward) : field ? field.CraftStartRotation : Level(transform.forward);

        /// <summary>The middle of the map, on the ground. The menu's camera circles it.</summary>
        public Vector3 Centre => field ? field.Centre : GroundBelow(transform.position);

        IPlayArea[] areas;

        /// <summary>
        /// Is this spot inside the map? A map with walls (a barrier sphere, a ring tube) is whatever's inside them;
        /// a map without walls is the play area box. Used to keep a ghost (the Phantom's special) from flying out of the map.
        /// </summary>
        public bool Contains(Vector3 spot)
        {
            if (Areas.Length > 0)
            {
                foreach (var area in Areas) if (!area.Contains(spot)) return false;
                return true;
            }
            Vector3 local = transform.InverseTransformPoint(spot);
            return Mathf.Abs(local.x) <= playAreaSize.x / 2f && Mathf.Abs(local.y) <= playAreaSize.y / 2f && Mathf.Abs(local.z) <= playAreaSize.z / 2f;
        }

        /// <summary>
        /// The safe middle of the map nearest this spot: where to head when you're getting close to its edge.
        /// The tube's middle line in a ring tube, the center of a barrier sphere, the box's middle otherwise.
        /// (Not Centre: in a ring tube that's the hole in the middle of the donut, outside the map.)
        /// </summary>
        public Vector3 Inside(Vector3 spot)
        {
            foreach (var area in Areas) if (!area.Contains(spot)) return area.Inside(spot);
            if (Areas.Length > 0) return Areas[0].Inside(spot);
            return transform.position;
        }

        IPlayArea[] Areas
        {
            get
            {
                if (areas != null) return areas;
                var found = new List<IPlayArea>();
                foreach (var root in gameObject.scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<IPlayArea>());
                return areas = found.ToArray();
            }
        }

        static readonly RaycastHit[] groundHits = new RaycastHit[32]; // reused, so asking every frame makes no garbage

        /// <summary>The ground under a spot: the first solid, unmoving thing straight below (not ships).</summary>
        public static Vector3 GroundBelow(Vector3 spot)
        {
            Vector3 above = new Vector3(spot.x, spot.y + 500f, spot.z);
            int count = Physics.RaycastNonAlloc(above, Vector3.down, groundHits, 2000f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = new Vector3(spot.x, -100000f, spot.z); // nothing below: no ground to keep above
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.rigidbody != null) continue;
                if (hit.collider.GetComponentInParent<CaptureTheFlag>()) continue; // goal rings float; they aren't ground
                if (hit.distance < best) { best = hit.distance; ground = hit.point; }
            }
            return ground;
        }

        /// <summary>The map in a loaded scene, or null if the scene has none.</summary>
        public static ArenaMap In(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var map = root.GetComponentInChildren<ArenaMap>(true);
                if (map) return map;
            }
            return null;
        }

        /// <summary>Facing this way, but level: no tilt up or down (anything pointing straight up faces +Z).</summary>
        public static Quaternion Level(Vector3 forward)
        {
            forward.y = 0f;
            return Quaternion.LookRotation(forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward, Vector3.up);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawWireCube(transform.position, playAreaSize);
            if (playerStart) Marker(playerStart, Color.white);
            foreach (var spawn in teamASpawns) if (spawn) Marker(spawn, Palette.Amber);
            foreach (var spawn in teamBSpawns) if (spawn) Marker(spawn, Palette.Teal);
            if (flagA) Flag(flagA, Palette.Amber);
            if (flagB) Flag(flagB, Palette.Teal);
        }

        static void Marker(Transform spot, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(spot.position, 4f);
            Gizmos.DrawLine(spot.position, spot.position + spot.forward * 12f);
        }

        static void Flag(Transform spot, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(spot.position, spot.position + Vector3.up * 20f);
            Gizmos.DrawCube(spot.position + Vector3.up * 17f + spot.right * 3f, new Vector3(6f, 4f, 0.5f));
        }
    }

    /// <summary>A map piece that walls in the playable space (a barrier sphere, a ring tube).</summary>
    public interface IPlayArea
    {
        bool Contains(Vector3 spot);
        /// <summary>The middle of the space nearest this spot (the tube's middle line, the sphere's center).</summary>
        Vector3 Inside(Vector3 spot);
    }

    /// <summary>
    /// Every map the game knows about: the scenes in Assets/Maps that are in the build list.
    /// (The editor keeps that list up to date, so a new map shows up on the menu by itself.)
    /// </summary>
    public static class Maps
    {
        public const string Folder = "Assets/Maps/";
        /// <summary>The scene with the ship, camera, sun, HUD and menu. Maps load on top of it.</summary>
        public const string GameScene = "Prototype";

        /// <summary>The scene names of all maps, in build-list order.</summary>
        public static List<string> All()
        {
            var names = new List<string>();
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (path.StartsWith(Folder)) names.Add(System.IO.Path.GetFileNameWithoutExtension(path));
            }
            return names;
        }

        /// <summary>The name shown for a map before it's loaded: its scene name in capitals.</summary>
        public static string Label(string sceneName) => sceneName.ToUpperInvariant();

        /// <summary>
        /// Maps with both flag spots, which can play capture the flag. The editor writes this list
        /// (Resources/Maps.txt) whenever it updates the build list, so it's known before a map loads.
        /// </summary>
        public static bool HasFlags(string sceneName)
        {
            var list = Resources.Load<TextAsset>("Maps");
            if (!list) return false;
            foreach (var line in list.text.Split('\n'))
            {
                var parts = line.Trim().Split('|');
                if (parts.Length == 2 && parts[0] == sceneName) return parts[1] == "flags";
            }
            return false;
        }
    }

    /// <summary>
    /// Testing a map: open its scene and press Play. The game scene can't run inside a map, so this
    /// swaps to the game scene and tells it to go straight into Training on that map.
    /// </summary>
    static class MapPlaytest
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void PlayFromMapScene()
        {
            if (Object.FindFirstObjectByType<SpaceCowboyGame>()) return;
            var map = Object.FindFirstObjectByType<ArenaMap>();
            if (!map) return;
            SpaceCowboyGame.MapToTest = map.gameObject.scene.name;
            SceneManager.LoadScene(Maps.GameScene);
        }
    }
}
