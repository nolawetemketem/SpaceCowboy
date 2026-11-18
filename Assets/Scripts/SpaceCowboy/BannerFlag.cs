using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// A rectangular banner that ripples like cloth: a team-colored flag with a darker border.
    /// It hangs from its left edge (this object's position), stretching along +X and up +Y, and waves
    /// in Z. The ripples run from the edge it hangs from toward its free end, bigger at the free end.
    /// Used on the flag at each base, and on the back of a ship that's carrying a flag.
    /// </summary>
    public class BannerFlag : MonoBehaviour
    {
        const int Columns = 14, Rows = 5;

        public float length = 10f, height = 5f;
        [Tooltip("How far the free end swings.")]
        public float wave = 0.9f;
        [Tooltip("How quickly the ripples run along it.")]
        public float speed = 6f;

        Mesh mesh;
        Vector3[] points, moved; // the flat banner, and this frame's rippled copy (reused every frame)
        float time;

        /// <summary>A banner hanging from this spot under parent, in this color.</summary>
        public static BannerFlag Create(Transform parent, Vector3 localPosition, Quaternion localRotation, Color color, float length, float height)
        {
            var go = new GameObject("Banner");
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            var banner = go.AddComponent<BannerFlag>();
            banner.length = length;
            banner.height = height;
            banner.Build(color);
            return banner;
        }

        void Build(Color color)
        {
            points = new Vector3[(Columns + 1) * (Rows + 1)];
            moved = new Vector3[points.Length];
            var colors = new Color[points.Length];
            var border = Color.Lerp(color, Color.black, 0.45f);
            for (int r = 0; r <= Rows; r++)
                for (int c = 0; c <= Columns; c++)
                {
                    int i = r * (Columns + 1) + c;
                    points[i] = new Vector3(length * c / Columns, height * r / Rows, 0f);
                    bool edge = r == 0 || r == Rows || c == Columns;
                    colors[i] = edge ? border : color;
                }
            var triangles = new int[Columns * Rows * 6];
            int t = 0;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                {
                    int a = r * (Columns + 1) + c, b = a + 1, d = a + Columns + 1, e = d + 1;
                    triangles[t++] = a; triangles[t++] = d; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = d; triangles[t++] = e;
                }
            mesh = new Mesh { name = "Banner" };
            mesh.vertices = points;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.bounds = new Bounds(new Vector3(length / 2f, height / 2f, 0f), new Vector3(length, height, wave * 4f));

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var look = gameObject.AddComponent<MeshRenderer>();
            // An unlit, two-sided material that uses the vertex colors: it reads clearly from both sides.
            look.sharedMaterial = Palette.Unlit(Color.white);
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            time += Time.deltaTime * speed;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 p = points[i];
                float free = p.x / length; // 0 at the edge it hangs from, 1 at the free end
                float ripple = Mathf.Sin(time - p.x * 0.9f + p.y * 0.25f) * wave * free;
                float droop = -free * free * height * 0.08f;
                moved[i] = new Vector3(p.x, p.y + droop, ripple);
            }
            mesh.SetVertices(moved);
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            var look = GetComponent<MeshRenderer>();
            if (look && look.sharedMaterial) Destroy(look.sharedMaterial);
        }
    }
}
