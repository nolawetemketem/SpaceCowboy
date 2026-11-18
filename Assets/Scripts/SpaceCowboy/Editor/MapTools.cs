using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceCowboy.EditorTools
{
    /// <summary>
    /// Space Cowboy ▸ Maps in the menu bar:
    ///   New Map…                         a new map scene in Assets/Maps, with a ground, an ArenaMap and its markers.
    ///   Move Desert Arena Into Its Own Map   one time: moves the desert, the terrain and the Training field out of
    ///                                    the game scene into Assets/Maps/Desert Training.unity, unchanged.
    ///   Update Build List                puts the game scene first and every map after it (also done automatically).
    /// Maps only load if they're in the build list, so any scene saved in Assets/Maps is added to it by itself.
    /// </summary>
    public static class MapTools
    {
        const string GameScenePath = "Assets/Scenes/" + Maps.GameScene + ".unity";
        const string DesertMapName = "Desert Training";
        static readonly string[] DesertMapObjects = { "Terrain", "Training Arena", "Desert Terrain" };

        [MenuItem("Space Cowboy/Maps/New Map…", priority = 1)]
        static void NewMap()
        {
            Directory.CreateDirectory(Maps.Folder);
            string path = EditorUtility.SaveFilePanelInProject("New map", "New Map", "unity", "Name the map. It's saved in Assets/Maps.", Maps.Folder);
            if (string.IsNullOrEmpty(path)) return;
            if (!path.StartsWith(Maps.Folder))
            {
                EditorUtility.DisplayDialog("New map", "Maps need to be saved in Assets/Maps, so the game can find them.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateMap(path);
        }

        /// <summary>Make and save a new map scene at this path, with a ground, an ArenaMap and its markers, and open it.</summary>
        public static void CreateMap(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // A big flat ground to start from. Replace it with your own terrain and props.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2600f, 2f, 2600f);
            ground.transform.position = new Vector3(0f, -1f, 0f);
            ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();

            var arena = new GameObject("Arena");
            var map = arena.AddComponent<ArenaMap>();
            map.trainingBots = false;
            Teams(map, flagZ: 1000f, flagY: 120f, spawnZ: 1150f, spawnY: 100f);
            map.playAreaSize = new Vector3(2600f, 1200f, 2600f);

            EditorSceneManager.SaveScene(scene, path);
            UpdateBuildList();
            Debug.Log($"New map saved: {path}. Press Play in this scene to fly it in Training. " +
                      "It's lit by the game scene's sun, so it looks dark until then.");
        }

        /// <summary>A sandy toon material for new maps' ground, made once and shared (Assets/Maps/Materials/Ground.mat).</summary>
        static Material GroundMaterial()
        {
            const string path = Maps.Folder + "Materials/Ground.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            Directory.CreateDirectory(Maps.Folder + "Materials");
            material = new Material(Shader.Find("SpaceCowboy/Toon"));
            material.SetColor("_BaseColor", new Color(0.85f, 0.62f, 0.4f));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Transform Marker(string name, Transform parent, Vector3 position, float facing)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.localPosition = position;
            marker.localRotation = Quaternion.Euler(0f, facing, 0f);
            return marker;
        }

        // --- Capture the flag starter maps --------------------------------------------------------

        [MenuItem("Space Cowboy/Maps/Make Capture-the-Flag Maps", priority = 2)]
        static void MakeFlagMaps() => MakeFlagMaps(replace: false);

        /// <summary>Make them again from scratch, replacing the three scenes (anything changed in them is lost).</summary>
        [MenuItem("Space Cowboy/Maps/Remake Capture-the-Flag Maps (replaces them)", priority = 3)]
        static void RemakeFlagMaps() => MakeFlagMaps(replace: true);

        static void MakeFlagMaps(bool replace)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning($"Save or discard the changes in {SceneManager.GetSceneAt(i).name} first. Nothing changed.");
                    return;
                }
            var wasOpen = EditorSceneManager.GetSceneManagerSetup(); // put back whatever you had open afterward
            var made = new List<string>();
            if (MakeFlagMap("Sky Island", SkyIsland, replace)) made.Add("Sky Island");
            if (MakeFlagMap("Underground City", UndergroundCity, replace)) made.Add("Underground City");
            if (MakeFlagMap("Hollow Ring", HollowRing, replace)) made.Add("Hollow Ring");
            if (wasOpen.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(wasOpen);
            UpdateBuildList();
            Debug.Log(made.Count > 0 ? $"Made {string.Join(", ", made)} in {Maps.Folder}." : "Those maps already exist. Nothing changed.");
        }

        /// <summary>A new map scene with an Arena (ArenaMap, no practice targets), filled in by build. Skipped if it exists.</summary>
        static bool MakeFlagMap(string name, System.Action<ArenaMap> build, bool replace)
        {
            string path = Maps.Folder + name + ".unity";
            if (File.Exists(path) && !replace) return false;
            Directory.CreateDirectory(Maps.Folder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var arena = new GameObject("Arena");
            var map = arena.AddComponent<ArenaMap>();
            map.trainingBots = false;
            build(map);
            EditorSceneManager.SaveScene(scene, path);
            return true;
        }

        /// <summary>Teams line up along Z: amber at -Z facing +Z, teal at +Z facing -Z.</summary>
        static void Teams(ArenaMap map, float flagZ, float flagY, float spawnZ, float spawnY)
        {
            map.flagA = Marker("Flag A", map.transform, new Vector3(0f, flagY, -flagZ), 0f);
            map.flagB = Marker("Flag B", map.transform, new Vector3(0f, flagY, flagZ), 180f);
            map.teamASpawns = new[] { -40f, 0f, 40f }.Select((x, i) => Marker($"Team A Spawn {i + 1}", map.transform, new Vector3(x, spawnY, -spawnZ), 0f)).ToArray();
            map.teamBSpawns = new[] { -40f, 0f, 40f }.Select((x, i) => Marker($"Team B Spawn {i + 1}", map.transform, new Vector3(x, spawnY, spawnZ), 180f)).ToArray();
            map.playerStart = Marker("Player Start", map.transform, new Vector3(0f, spawnY, -spawnZ), 0f);
        }

        // Sizes: the ship's top speed is 125 units a second, so goals about 2000 apart take ~15 seconds to cross.

        /// <summary>A big floating island above the clouds, a goal floating in the open sky past each end, all inside a force field.</summary>
        static void SkyIsland(ArenaMap map)
        {
            var island = new GameObject("Island").AddComponent<FloatingIsland>();
            island.radius = 700f;
            island.depth = 450f;
            island.hills = 30f;
            var clouds = new GameObject("Clouds").AddComponent<CloudLayer>();
            clouds.clouds = 160;
            clouds.spread = 3000f;
            clouds.lowest = -950f;
            clouds.highest = -450f;
            clouds.puffSize = 110f;
            var barrier = new GameObject("Barrier").AddComponent<BarrierSphere>();
            barrier.radius = 1600f;
            barrier.look = BarrierSphere.Look.SeeThrough;
            map.playAreaSize = new Vector3(3200f, 3200f, 3200f);
            Teams(map, flagZ: 1000f, flagY: 200f, spawnZ: 1150f, spawnY: 180f);
        }

        /// <summary>A city under the ground: a solid rock dome with a block city on its floor, a flag at each end.</summary>
        static void UndergroundCity(ArenaMap map)
        {
            const float radius = 1250f, floorDepth = 450f;
            var dome = new GameObject("Cave").AddComponent<BarrierSphere>();
            dome.radius = radius;
            dome.look = BarrierSphere.Look.Rock;
            dome.color = Palette.Hex("#4a3b30");
            var city = new GameObject("City").AddComponent<CityBlocks>();
            city.transform.position = new Vector3(0f, -floorDepth, 0f);
            city.domeRadius = radius;
            city.domeCentreHeight = floorDepth;
            city.floorRadius = Mathf.Sqrt(radius * radius - floorDepth * floorDepth) - 10f;
            city.blockSpacing = 80f;
            city.blockWidth = 50f;
            city.tallest = 420f;
            city.laneHalfWidth = 60f;
            city.plazaRadius = 170f;
            city.endPlazaDistance = 950f;
            map.transform.position = new Vector3(0f, -floorDepth, 0f);
            map.playAreaSize = new Vector3(2500f, 2500f, 2500f);
            Teams(map, flagZ: 950f, flagY: 180f, spawnZ: 1080f, spawnY: 200f);
            map.menuOrbitHeight = 450f; // above the rooftops
        }

        /// <summary>Inside a giant hollow donut: the bases face each other across the ring, two ways round.</summary>
        static void HollowRing(ArenaMap map)
        {
            // Around the ring from one base to the other is ~2500 units (~20 seconds), so its size stays;
            // the tube is wider, for more room to pass and dodge.
            var tube = new GameObject("Ring Tube").AddComponent<RingTube>();
            tube.tubeRadius = 200f;
            tube.Rebuild();
            Transform At(string name, float degrees)
            {
                var marker = new GameObject(name).transform;
                marker.SetParent(map.transform, false);
                marker.position = tube.MiddleAt(degrees);
                marker.rotation = Quaternion.LookRotation(tube.AlongAt(degrees), Vector3.up);
                return marker;
            }
            map.flagA = At("Flag A", 0f);
            map.flagB = At("Flag B", 180f);
            map.playerStart = At("Player Start", -25f);
            map.teamASpawns = new[] { At("Team A Spawn 1", -25f), At("Team A Spawn 2", -30f), At("Team A Spawn 3", -35f) };
            map.teamBSpawns = new[] { At("Team B Spawn 1", 155f), At("Team B Spawn 2", 150f), At("Team B Spawn 3", 145f) };
            // The menu's camera circles inside the tube, around the amber base.
            map.menuFocus = map.flagA;
            map.menuOrbitRadius = 90f;
            map.menuOrbitHeight = 15f;
            map.playAreaSize = new Vector3(2400f, 500f, 2400f);
        }

        [MenuItem("Space Cowboy/Maps/Move Desert Arena Into Its Own Map", priority = 20)]
        static void MoveDesertArena()
        {
            string mapPath = Maps.Folder + DesertMapName + ".unity";
            if (File.Exists(mapPath))
            {
                Debug.LogWarning($"{mapPath} already exists, so the desert arena has already been moved. Nothing changed.");
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning($"Save or discard the changes in {SceneManager.GetSceneAt(i).name} first. Nothing changed.");
                    return;
                }

            var game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var roots = game.GetRootGameObjects().Where(go => DesertMapObjects.Contains(go.name)).ToList();
            if (roots.Count == 0)
            {
                Debug.LogWarning("Couldn't find the desert arena in the game scene. Nothing changed.");
                return;
            }

            // Anything in the game scene that points at these would lose the link once they're in another
            // scene. The game finds the map by itself now, so clear those links first.
            foreach (var time in Object.FindObjectsByType<TimeOfDay>(FindObjectsSortMode.None)) time.field = null;

            string moved = string.Join(", ", roots.Select(r => r.name));
            Directory.CreateDirectory(Maps.Folder);
            var map = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            foreach (var root in roots) SceneManager.MoveGameObjectToScene(root, map);

            var field = roots.FirstOrDefault(go => go.name == "Training Arena");
            if (field && !field.GetComponent<ArenaMap>()) field.AddComponent<ArenaMap>();

            EditorSceneManager.SaveScene(map, mapPath);
            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            EditorSceneManager.CloseScene(map, true);
            UpdateBuildList();
            Debug.Log($"Moved {moved} into {mapPath}. The game scene loads it when you play.");
        }

        [MenuItem("Space Cowboy/Maps/Update Build List", priority = 40)]
        public static void UpdateBuildList()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            if (File.Exists(GameScenePath)) scenes.Add(new EditorBuildSettingsScene(GameScenePath, true));
            if (Directory.Exists(Maps.Folder))
                foreach (var path in Directory.GetFiles(Maps.Folder, "*.unity").OrderBy(p => p))
                    scenes.Add(new EditorBuildSettingsScene(path.Replace('\\', '/'), true));

            // Keep any other scenes someone added by hand, after ours.
            foreach (var existing in EditorBuildSettings.scenes)
                if (!scenes.Any(s => s.path == existing.path) && File.Exists(existing.path) && !existing.path.StartsWith(Maps.Folder))
                    scenes.Add(existing);

            if (!scenes.Select(s => s.path).SequenceEqual(EditorBuildSettings.scenes.Select(s => s.path)))
                EditorBuildSettings.scenes = scenes.ToArray();
            WriteMapList();
            AssetDatabase.SaveAssets(); // write the list to ProjectSettings now, not just when the project is next saved
            Debug.Log("Build list updated: " + string.Join(", ", scenes.Select(s => Path.GetFileNameWithoutExtension(s.path))));
        }

        const string MapListPath = "Assets/Scripts/SpaceCowboy/Resources/Maps.txt";

        /// <summary>
        /// Note which maps have both flag spots (so the menu can offer them for capture the flag before loading them).
        /// Read straight from the scene files: an ArenaMap's flagA and flagB point at real objects.
        /// </summary>
        static void WriteMapList()
        {
            if (!Directory.Exists(Maps.Folder)) return;
            var lines = new List<string>();
            foreach (var path in Directory.GetFiles(Maps.Folder, "*.unity").OrderBy(p => p))
            {
                string text = File.ReadAllText(path);
                bool HasSpot(string field)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(text, field + @": \{fileID: (-?\d+)");
                    return match.Success && match.Groups[1].Value != "0";
                }
                lines.Add(Path.GetFileNameWithoutExtension(path) + "|" + (HasSpot("flagA") && HasSpot("flagB") ? "flags" : "plain"));
            }
            string list = string.Join("\n", lines);
            if (File.Exists(MapListPath) && File.ReadAllText(MapListPath) == list) return;
            Directory.CreateDirectory(Path.GetDirectoryName(MapListPath));
            File.WriteAllText(MapListPath, list);
            AssetDatabase.ImportAsset(MapListPath);
        }

        /// <summary>Whenever a scene in Assets/Maps is saved, added, moved or deleted, update the build list.</summary>
        class WatchMaps : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                bool touched = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                    .Any(p => p.EndsWith(".unity") && (p.StartsWith(Maps.Folder) || p == GameScenePath));
                if (touched) EditorApplication.delayCall += UpdateBuildList;
            }
        }
    }
}
