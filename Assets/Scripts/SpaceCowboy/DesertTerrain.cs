using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// The "Mushroom Samba" desert from the web game (terrain.ts): rolling dunes, a flat worn patch
    /// for the field, flat-topped mesas on the horizon and a levelled strip for the railway. The drawn
    /// ground and its collider both come from one height function, so what you see is what you hit.
    ///
    /// It's built from code whenever it's enabled (in the editor too), so nothing is saved but the
    /// numbers. The web game measured in metres; this scene is 3.2× bigger, so everything is scaled.
    /// The web game's field ran along -Z toward goal A; here, like the Training Arena, +Z faces goal A.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class DesertTerrain : MonoBehaviour
    {
        [Tooltip("Web metres → Unity units. The Fighter is about 3.2× bigger here than in the web game.")]
        public float scale = 3.2f;
        [Tooltip("Grid cells per side. The web game used 160 (2.5 m each).")]
        [Range(32, 250)] public int cells = 160;

        // --- The shape, in the web game's metres (as in terrain.ts) ---------------------------------

        /// <summary>The desert is a square this many metres across, centred on the field.</summary>
        public const float Size = 400f;
        /// <summary>The flat, worn patch around the field (a little bigger than the field itself).</summary>
        const float FlatHalfWidth = 25f + 6f, FlatHalfLength = 40f + 8f;
        /// <summary>How far beyond the flat patch the dunes take to rise to full size (m).</summary>
        const float Blend = 25f;
        /// <summary>Flat-topped mesas on the horizon: x, z, radius of the flat top, height (m).</summary>
        static readonly Vector4[] Mesas =
        {
            new(-140f, -110f, 28f, 18f),
            new(150f, -60f, 22f, 14f),
            new(130f, 115f, 30f, 20f),
            new(-165f, 115f, 25f, 16f),
            new(15f, -150f, 20f, 12f),
        };
        /// <summary>The railway runs along X at this Z, on a levelled railbed this high (m).</summary>
        public const float RailZ = 165f, RailHeight = 2f, RailHalfWidth = 6f;

        // Ground colors: variety comes from where you are, not one flat sand.
        static readonly Color Sand = Palette.Hex("#c98b4f");
        static readonly Color SandPale = Palette.Hex("#ddb27a");  // dune crests, bleached by the sun
        static readonly Color SandShade = Palette.Hex("#b47a49"); // dune hollows
        static readonly Color Worn = Palette.Hex("#a87245");      // the old, trampled field
        static readonly Color Rock = Palette.Hex("#9b5434");      // steep mesa sides
        static readonly Color MesaTop = Palette.Hex("#b9773f");
        static readonly Color Railbed = Palette.Hex("#8f6a4c");

        Mesh mesh;

        void OnEnable() => Build();
#if UNITY_EDITOR
        // Rebuild when a number changes in the Inspector (a moment later: colliders can't change mid-validate).
        void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this && isActiveAndEnabled) Build(); };
#endif

        void OnDisable()
        {
            if (!mesh) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }

        /// <summary>Ground height (Unity units) at a point in this object's local space.</summary>
        public float LocalHeightAt(float x, float z) => HeightAt(x / scale, -z / scale) * scale;

        void Build()
        {
            if (!mesh)
            {
                mesh = new Mesh { name = "Desert", hideFlags = HideFlags.DontSave };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            int n = cells + 1; // grid points per side
            var vertices = new Vector3[n * n];
            var colors = new Color[n * n];
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int row = 0; row < n; row++)
            for (int col = 0; col < n; col++)
            {
                // Web metres, with z flipped: the web game's +Z is this scene's -Z.
                float wx = -Size / 2f + col / (float)cells * Size;
                float wz = -(-Size / 2f + row / (float)cells * Size);
                int i = row * n + col;
                vertices[i] = new Vector3(wx, HeightAt(wx, wz), -wz) * scale;
                Color c = ColorAt(wx, wz);
                colors[i] = linear ? c.linear : c; // vertex colors aren't converted for us
            }

            var triangles = new int[cells * cells * 6];
            int t = 0;
            for (int row = 0; row < cells; row++)
            for (int col = 0; col < cells; col++)
            {
                int i = row * n + col;
                triangles[t++] = i; triangles[t++] = i + n; triangles[t++] = i + 1;
                triangles[t++] = i + 1; triangles[t++] = i + n; triangles[t++] = i + n + 1;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var collider = GetComponent<MeshCollider>();
            collider.sharedMesh = null; // re-cook the collider from the new shape
            collider.sharedMesh = mesh;
        }

        // --- The height and color functions, straight from terrain.ts (web metres) ------------------

        /// <summary>Like GLSL's smoothstep; a can be bigger than b, which flips it.</summary>
        static float Smoothstep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>How far (m) a point is outside the flat patch; 0 inside it.</summary>
        static float OutsideFlat(float x, float z)
        {
            float dx = Mathf.Max(0f, Mathf.Abs(x) - FlatHalfWidth);
            float dz = Mathf.Max(0f, Mathf.Abs(z) - FlatHalfLength);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Rolling dunes: a few overlapping waves at different sizes and angles, always above 0.</summary>
        static float Dunes(float x, float z) =>
            2.4f
            + 2.2f * Mathf.Sin(x * 0.045f + 1.3f) * Mathf.Cos(z * 0.038f)
            + 1.3f * Mathf.Sin((x + z) * 0.07f)
            + 0.6f * Mathf.Sin(x * 0.13f - z * 0.09f);

        /// <summary>Ground height (m) at any point, in the web game's coordinates.</summary>
        public static float HeightAt(float x, float z)
        {
            float h = Dunes(x, z) * Smoothstep(0f, Blend, OutsideFlat(x, z));
            foreach (var m in Mesas)
            {
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(m.x, m.y));
                h = Mathf.Max(h, m.w * Smoothstep(m.z + 7f, m.z, d)); // steep sides, flat top
            }
            // Level a strip for the railway.
            float rail = Smoothstep(RailHalfWidth + 10f, RailHalfWidth, Mathf.Abs(z - RailZ));
            return Mathf.Lerp(h, RailHeight, rail);
        }

        /// <summary>Steepness at a point: 0 = flat, 1 = 45°, more = steeper.</summary>
        static float SlopeAt(float x, float z)
        {
            const float e = 1f;
            float dx = HeightAt(x + e, z) - HeightAt(x - e, z);
            float dz = HeightAt(x, z + e) - HeightAt(x, z - e);
            return Mathf.Sqrt(dx * dx + dz * dz) / (2f * e);
        }

        static Color ColorAt(float x, float z)
        {
            float h = HeightAt(x, z);
            float outside = Smoothstep(0f, Blend, OutsideFlat(x, z));
            // Dunes: pale on the crests, a little darker in the hollows.
            Color c = Color.Lerp(SandShade, SandPale, Smoothstep(1.5f, 5.5f, h));
            c = Color.Lerp(c, Sand, 0.35f);
            // Slow, large patches so the sand isn't one even color.
            float patch = 0.5f + 0.5f * Mathf.Sin(x * 0.021f + 0.7f) * Mathf.Sin(z * 0.017f - 1.1f);
            c = Color.Lerp(c, SandShade, patch * 0.25f);
            // The worn field in the middle.
            c = Color.Lerp(c, Worn, 1f - outside);
            // Mesas: reddish rock on the steep sides, a warmer top.
            if (h > 7f) c = Color.Lerp(c, MesaTop, Smoothstep(7f, 11f, h));
            c = Color.Lerp(c, Rock, Smoothstep(0.6f, 1.4f, SlopeAt(x, z)));
            // The railbed.
            c = Color.Lerp(c, Railbed, Smoothstep(RailHalfWidth + 1f, RailHalfWidth - 1f, Mathf.Abs(z - RailZ)) * 0.8f);
            return c;
        }
    }
}
