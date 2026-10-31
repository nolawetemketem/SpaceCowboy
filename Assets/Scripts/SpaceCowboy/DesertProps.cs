using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// "Mushroom Samba" desert dressing (ported from the web game's props.ts), built when the game starts:
    /// cacti and rocks scattered around the field, a watermelon stand beside it, and a train out on the horizon.
    ///
    /// The layout follows the web game: a seeded scatter (the same every time), kept off the field, the
    /// railway and the mesa tops. The variety comes from the Unity desert packs: their cacti, aloe and
    /// rocks are mixed in with the web game's own saguaros, all drawn with the toon shader.
    /// Put this next to the DesertTerrain; the web game's metres are scaled by its scale.
    /// </summary>
    [RequireComponent(typeof(DesertTerrain))]
    public class DesertProps : MonoBehaviour
    {
        [Header("What to scatter")]
        [Tooltip("Cacti and other plants from the desert packs.")]
        public List<GameObject> plants = new();
        [Tooltip("Rocks from the desert packs.")]
        public List<GameObject> rocks = new();
        [Tooltip("Optional skins for rocks that use a color-sheet texture (the low-poly pack's Sand, Orange...). One is picked per rock.")]
        public List<Material> rockSkins = new();
        [Tooltip("Share of the plants that are the web game's own saguaros (tall trunk, upturned arms).")]
        [Range(0f, 1f)] public float saguaroShare = 0.35f;

        [Header("How many (the web game used 60 and 45)")]
        public int plantCount = 60;
        public int rockCount = 45;
        [Tooltip("Change it for a different scatter; the same number always gives the same desert.")]
        public int seed = 17;

        [Header("Mushroom Samba nods")]
        public bool watermelonStand = true;
        public bool train = true;

        // Web colors (props.ts).
        static readonly Color CactusGreen = Palette.Hex("#5b7f3a");
        static readonly Color Wood = Palette.Hex("#8a5a36");
        static readonly Color AwningRed = Palette.Hex("#c8352a");
        static readonly Color AwningCream = Palette.Hex("#f1e6cf");
        static readonly Color Melon = Palette.Hex("#3f7a35");
        static readonly Color MelonFlesh = Palette.Hex("#e0473c");
        static readonly Color TrainBody = Palette.Hex("#3b2f36");
        static readonly Color TrainAccent = Palette.Hex("#7a3a2c");
        static readonly Color Rail = Palette.Hex("#5a4a44");

        DesertTerrain desert;
        Transform root;
        readonly Dictionary<Material, Material> toonFor = new();
        readonly Dictionary<Color, Material> toonColor = new();

        void Start()
        {
            desert = GetComponent<DesertTerrain>();
            root = new GameObject("Props").transform;
            root.SetParent(transform, false);
            Scatter();
            if (watermelonStand) BuildWatermelonStand();
            if (train) BuildTrain();
        }

        float S => desert.scale;

        /// <summary>A spot given in the web game's metres, on the ground, in this object's space.</summary>
        Vector3 Local(float wx, float wz, float lift = 0f) => new Vector3(wx, DesertTerrain.HeightAt(wx, wz) + lift, -wz) * S;

        /// <summary>Keep props off the field, off the railway, inside the desert and off the mesa tops (web metres).</summary>
        static bool ClearSpot(float x, float z)
        {
            bool nearField = Mathf.Abs(x) < 25f + 14f && Mathf.Abs(z) < 40f + 16f;
            bool nearRail = Mathf.Abs(z - DesertTerrain.RailZ) < DesertTerrain.RailHalfWidth + 6f;
            bool inside = Mathf.Abs(x) < DesertTerrain.Size / 2f - 10f && Mathf.Abs(z) < DesertTerrain.Size / 2f - 10f;
            return !nearField && !nearRail && inside && DesertTerrain.HeightAt(x, z) < 7f;
        }

        // --- Scatter (props.ts scatterCactusesAndRocks) ---------------------------------------------

        void Scatter()
        {
            var rand = new Mulberry32((uint)seed);
            int plantsPlaced = 0, rocksPlaced = 0;
            for (int tries = 0; tries < 4000 && (plantsPlaced < plantCount || rocksPlaced < rockCount); tries++)
            {
                float x = (rand.Next() - 0.5f) * DesertTerrain.Size;
                float z = (rand.Next() - 0.5f) * DesertTerrain.Size;
                if (!ClearSpot(x, z)) continue;
                Vector3 ground = Local(x, z, -0.15f);
                float turn = rand.Next() * 360f;

                if (plantsPlaced < plantCount && rand.Next() < 0.6f)
                {
                    float height = (4f + rand.Next() * 3f) * S; // the web game's saguaros were 4–7 m tall
                    if (plants.Count == 0 || rand.Next() < saguaroShare) Saguaro(ground, turn, height, rand);
                    else
                    {
                        var prefab = plants[(int)(rand.Next() * plants.Count) % plants.Count];
                        // Pack plants vary: aloe and small cacti read better a bit shorter.
                        Place(prefab, ground, turn, height * (0.45f + rand.Next() * 0.55f), byHeight: true, capsule: true, rand);
                    }
                    plantsPlaced++;
                }
                else if (rocksPlaced < rockCount && rocks.Count > 0)
                {
                    float size = (0.8f + rand.Next() * 1.8f) * 2f * S; // the web game's rocks: 0.8–2.6 m radius
                    var prefab = rocks[(int)(rand.Next() * rocks.Count) % rocks.Count];
                    Place(prefab, ground, turn, size, byHeight: false, capsule: false, rand);
                    rocksPlaced++;
                }
            }
        }

        /// <summary>Put a pack prefab on the ground: sized, turned, toon-shaded, and solid.</summary>
        void Place(GameObject prefab, Vector3 ground, float turn, float size, bool byHeight, bool capsule, Mulberry32 rand)
        {
            var go = Instantiate(prefab, root);
            go.name = prefab.name;
            go.transform.localPosition = ground;
            go.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c); // we add our own, below

            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(go); return; }
            // Re-skin only rocks that use a color-sheet texture (the low-poly pack); flat-colored rocks keep their color.
            bool sheet = renderers[0].sharedMaterial && renderers[0].sharedMaterial.mainTexture;
            Material skin = !byHeight && sheet && rockSkins.Count > 0 ? rockSkins[(int)(rand.Next() * rockSkins.Count) % rockSkins.Count] : null;
            foreach (var r in renderers)
            {
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = Toon(skin ? skin : materials[i]);
                r.sharedMaterials = materials;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            // Scale to size: plants by height, rocks by their widest side. Then sit the bottom on the ground.
            Bounds b = WorldBounds(renderers);
            float current = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (current > 1e-3f) go.transform.localScale *= size / current;
            b = WorldBounds(renderers);
            go.transform.position += Vector3.up * (transform.TransformPoint(ground).y - b.min.y);
            b = WorldBounds(renderers);

            // Solid, like the web game's colliders: a capsule up a plant's trunk, a box around a rock.
            var solid = new GameObject("Collider");
            solid.transform.SetParent(go.transform, true);
            solid.transform.SetPositionAndRotation(b.center, Quaternion.identity);
            solid.transform.localScale = new Vector3(1f / go.transform.lossyScale.x, 1f / go.transform.lossyScale.y, 1f / go.transform.lossyScale.z);
            if (capsule)
            {
                var c = solid.AddComponent<CapsuleCollider>();
                c.radius = Mathf.Min(b.size.x, b.size.z) * 0.3f;
                c.height = b.size.y;
            }
            else solid.AddComponent<BoxCollider>().size = b.size * 0.85f;
        }

        static Bounds WorldBounds(Renderer[] renderers)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>The toon version of a pack material: same color and texture.</summary>
        Material Toon(Material source)
        {
            if (!source || source.shader.name == "SpaceCowboy/Toon") return source;
            if (toonFor.TryGetValue(source, out var toon)) return toon;
            Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            Texture texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            toon = Palette.Toon(color, texture);
            toonFor[source] = toon;
            return toon;
        }

        Material Toon(Color color)
        {
            if (!toonColor.TryGetValue(color, out var m)) toonColor[color] = m = Palette.Toon(color);
            return m;
        }

        // --- Built from simple shapes (sizes in web metres, inside a holder scaled up) --------------

        /// <summary>A holder at a spot, scaled so the parts inside can be sized in the web game's metres.</summary>
        Transform Holder(string name, Vector3 local, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = local;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            t.localScale = Vector3.one * S;
            return t;
        }

        GameObject Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 size, Color color, Material material = null)
        {
            // Unity's capsule and cylinder are 2 tall and 1 across; cube and sphere are 1.
            Vector3 scale = type == PrimitiveType.Capsule || type == PrimitiveType.Cylinder ? new Vector3(size.x, size.y / 2f, size.z) : size;
            var go = Palette.Shape(type, type.ToString(), parent, position, scale, material ? material : Toon(color));
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        /// <summary>A saguaro (props.ts buildCactus): a tall trunk with one or two upturned arms.</summary>
        void Saguaro(Vector3 ground, float turn, float heightUnits, Mulberry32 rand)
        {
            var g = Holder("Saguaro", ground, turn);
            float height = heightUnits / S;
            const float r = 0.45f;
            Part(PrimitiveType.Capsule, g, new Vector3(0f, height / 2f + r, 0f), new Vector3(2 * r, height + 2 * r, 2 * r), CactusGreen);
            int arms = rand.Next() < 0.5f ? 1 : 2;
            for (int a = 0; a < arms; a++)
            {
                float side = a == 0 ? 1f : -1f;
                float armY = height * (0.4f + rand.Next() * 0.25f);
                var outArm = Part(PrimitiveType.Capsule, g, new Vector3(side * 0.9f, armY, 0f), new Vector3(1.5f * r, 0.9f + 1.5f * r, 1.5f * r), CactusGreen);
                outArm.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                float upLength = 1.2f + rand.Next() * 1.4f;
                Part(PrimitiveType.Capsule, g, new Vector3(side * 1.45f, armY + upLength / 2f, 0f), new Vector3(1.5f * r, upLength + 1.5f * r, 1.5f * r), CactusGreen);
            }
            var solid = new GameObject("Collider").AddComponent<CapsuleCollider>();
            solid.transform.SetParent(g, false);
            solid.radius = 0.5f;
            solid.height = 5f;
            solid.center = new Vector3(0f, 2.5f, 0f);
        }

        /// <summary>The roadside watermelon stand, beside the field, facing it (props.ts buildWatermelonStand).</summary>
        void BuildWatermelonStand()
        {
            const float x = -(25f + 9f), z = 12f;
            var stand = Holder("Watermelon Stand", Local(x, z), 90f); // its front (+Z) faces the field

            Part(PrimitiveType.Cube, stand, new Vector3(0f, 1.1f, 0f), new Vector3(5f, 0.25f, 2f), Wood); // table
            foreach (var (lx, lz) in new[] { (-2.3f, -0.8f), (2.3f, -0.8f), (-2.3f, 0.8f), (2.3f, 0.8f) })
                Part(PrimitiveType.Cube, stand, new Vector3(lx, 0.55f, lz), new Vector3(0.18f, 1.1f, 0.18f), Wood);
            foreach (float px in new[] { -2.3f, 2.3f })
                Part(PrimitiveType.Cube, stand, new Vector3(px, 2.3f, -0.9f), new Vector3(0.15f, 2.4f, 0.15f), Wood);

            var awning = Part(PrimitiveType.Cube, stand, new Vector3(0f, 3.3f, -0.1f), new Vector3(5.6f, 0.08f, 2.6f), Color.white,
                Palette.Toon(Color.white, Stripes(AwningRed, AwningCream, 10)));
            awning.transform.localRotation = Quaternion.Euler(14f, 0f, 0f); // tipped down toward the front

            var rind = Palette.Toon(Color.white, Stripes(Melon, Palette.Hex("#2a5a26"), 12));
            foreach (var (mx, mz, s) in new[] { (-1.6f, 0.2f, 1f), (-0.5f, -0.3f, 0.9f), (0.6f, 0.25f, 1.05f), (1.7f, -0.2f, 0.95f) })
                Part(PrimitiveType.Sphere, stand, new Vector3(mx, 1.62f, mz), new Vector3(1.35f, 1f, 1f) * (0.84f * s), Color.white, rind);
            // One cut slice on the front edge.
            // (A whole disc standing on edge, half sunk into the table, reads as the web game's half-round slice.)
            var slice = Part(PrimitiveType.Cylinder, stand, new Vector3(0.1f, 1.25f, 0.75f), new Vector3(1f, 0.12f, 1f), MelonFlesh);
            slice.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var solid = new GameObject("Collider").AddComponent<BoxCollider>();
            solid.transform.SetParent(stand, false);
            solid.center = new Vector3(0f, 1.7f, 0f);
            solid.size = new Vector3(5.2f, 3.4f, 2.2f);
        }

        /// <summary>A steam train standing on the levelled railbed, far out on the horizon (props.ts buildTrain).</summary>
        void BuildTrain()
        {
            var t = Holder("Train", new Vector3(-40f, DesertTerrain.RailHeight, -DesertTerrain.RailZ) * S, 0f);
            foreach (float offset in new[] { -0.75f, 0.75f })
                Part(PrimitiveType.Cube, t, new Vector3(40f, 0.08f, offset), new Vector3(DesertTerrain.Size - 20f, 0.15f, 0.15f), Rail);

            Part(PrimitiveType.Cube, t, new Vector3(-3f, 2.1f, 0f), new Vector3(3f, 3.2f, 2.6f), TrainBody);      // cab
            var boiler = Part(PrimitiveType.Cylinder, t, new Vector3(1.5f, 1.7f, 0f), new Vector3(2.2f, 6f, 2.2f), TrainBody);
            boiler.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(PrimitiveType.Cylinder, t, new Vector3(3.4f, 3.2f, 0f), new Vector3(1f, 1.4f, 1f), TrainAccent); // chimney
            Part(PrimitiveType.Cube, t, new Vector3(0f, 0.6f, 0f), new Vector3(10f, 0.6f, 2.4f), TrainAccent);     // base
            for (int i = 1; i <= 3; i++)
                Part(PrimitiveType.Cube, t, new Vector3(-4f - i * 9f, 2f, 0f), new Vector3(8f, 2.8f, 2.6f), i % 2 == 1 ? TrainAccent : TrainBody);
        }

        /// <summary>Stripes for the awning and the dark-green stripes on a watermelon's rind.</summary>
        static Texture2D Stripes(Color a, Color b, int count)
        {
            var p = new TexturePainter(256, 16, a);
            for (int i = 1; i < count; i += 2) p.FillRect(new Rect(i * 256f / count, 0f, 256f / count + 1f, 16f), b);
            var tex = p.ToTexture(filter: FilterMode.Point);
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        /// <summary>
        /// The web game's seeded random numbers (mulberry32), so the scatter is the same every time the game loads.
        /// </summary>
        class Mulberry32
        {
            uint state;
            public Mulberry32(uint seed) => state = seed;

            public float Next()
            {
                unchecked
                {
                    state += 0x6D2B79F5;
                    uint t = (state ^ (state >> 15)) * (1u | state);
                    t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
                    return (float)((t ^ (t >> 14)) / 4294967296.0);
                }
            }
        }
    }
}
