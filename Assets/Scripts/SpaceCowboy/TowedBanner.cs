using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// A long rectangular banner towed behind a ship on a rope, like a banner plane over a beach.
    /// The rope runs from the ship's tail to a short bridle that holds the banner's front edge; the banner
    /// streams out behind. Every point is dragged along by the one in front of it, keeping its distance, so the
    /// whole thing traces the path you flew: it swings wide in turns and snakes out behind you, and never folds
    /// over on itself. The banner ripples as it goes.
    /// It lives in the world (not on the ship), so it can lag behind.
    /// </summary>
    public class TowedBanner : MonoBehaviour
    {
        const int RopePoints = 9, BannerPoints = 26;
        const float TurnFollow = 0.25f; // how quickly the banner's "up" turns with the ship, per 1/60 s (scaled by frame time)

        Transform ship;
        Vector3 towPoint; // on the ship, in its own space
        float ropeStep, bannerStep, height;
        Vector3[] points, ups;
        Vector3[] vertices; // the banner's corners, reused every frame
        Mesh mesh;
        LineRenderer rope, bridle;
        float time;

        /// <summary>A banner towed from this spot on the ship (in the ship's own space), sized to the ship.</summary>
        public static TowedBanner Create(Transform parent, Transform ship, Vector3 towPoint, Color color, float shipLength)
        {
            var go = new GameObject("Towed Banner");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var banner = go.AddComponent<TowedBanner>();
            banner.ship = ship;
            banner.towPoint = towPoint;
            float ropeLength = shipLength * 2.2f, bannerLength = shipLength * 3.4f;
            banner.ropeStep = ropeLength / (RopePoints - 1);
            banner.bannerStep = bannerLength / (BannerPoints - 1);
            banner.height = bannerLength * 0.22f;
            banner.Build(color);
            return banner;
        }

        int Count => RopePoints + BannerPoints - 1; // the rope's last point is the banner's front edge
        float Step(int i) => i < RopePoints ? ropeStep : bannerStep; // the gap between point i-1 and point i

        void Build(Color color)
        {
            points = new Vector3[Count];
            ups = new Vector3[Count];
            Vector3 start = ship.TransformPoint(towPoint);
            Vector3 back = -ship.forward;
            float along = 0f;
            for (int i = 0; i < Count; i++)
            {
                if (i > 0) along += Step(i);
                points[i] = start + back * along;
                ups[i] = ship.up;
            }

            var lineMaterial = Palette.Unlit(Palette.Hex("#2c2622"));
            rope = Line("Rope", lineMaterial, 0.18f);
            bridle = Line("Bridle", lineMaterial, 0.14f);
            rope.positionCount = RopePoints;
            bridle.positionCount = 3;

            // The banner: two rows of points (top and bottom edge) along its length, with a darker border.
            var colors = new Color[BannerPoints * 2];
            var border = Color.Lerp(color, Color.black, 0.4f);
            var triangles = new int[(BannerPoints - 1) * 6];
            for (int k = 0; k < BannerPoints; k++)
            {
                bool edge = k == 0 || k == BannerPoints - 1;
                colors[k * 2] = colors[k * 2 + 1] = edge ? border : color;
                if (k == BannerPoints - 1) continue;
                int t = k * 6, a = k * 2;
                triangles[t] = a; triangles[t + 1] = a + 1; triangles[t + 2] = a + 2;
                triangles[t + 3] = a + 1; triangles[t + 4] = a + 3; triangles[t + 5] = a + 2;
            }
            mesh = new Mesh { name = "Towed Banner" };
            vertices = new Vector3[BannerPoints * 2];
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var look = gameObject.AddComponent<MeshRenderer>();
            look.sharedMaterial = Palette.Unlit(Color.white); // two-sided, colored by the vertex colors
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Shape(1f);
        }

        LineRenderer Line(string name, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.numCapVertices = 2;
            return line;
        }

        // After the ship has moved this frame.
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (!ship || dt <= 0f) return;
            time += dt;

            // The first point is tied to the ship's tail. Each one after it is pulled straight toward the
            // point in front until it's back at its distance. (No momentum of its own: points that keep their
            // speed fling outward in turns and fold the banner back over itself.)
            points[0] = ship.TransformPoint(towPoint);
            for (int i = 1; i < Count; i++)
            {
                Vector3 fromFront = points[i] - points[i - 1];
                if (fromFront.sqrMagnitude < 1e-6f) fromFront = -ship.forward;
                points[i] = points[i - 1] + fromFront.normalized * Step(i);
            }
            Shape(1f - Mathf.Pow(1f - TurnFollow, dt * 60f));
        }

        /// <summary>Lay the rope and banner along the points. turn: how far the banner's "up" turns this frame.</summary>
        void Shape(float turn)
        {
            for (int i = 0; i < RopePoints; i++) rope.SetPosition(i, points[i]);

            // The banner stands up the way the ship does, kept square to the way it's streaming.
            for (int k = 0; k < BannerPoints; k++)
            {
                int i = RopePoints - 1 + k;
                Vector3 along = (i + 1 < Count ? points[i + 1] - points[i] : points[i] - points[i - 1]).normalized;
                Vector3 up = ship.up - along * Vector3.Dot(ship.up, along);
                if (up.sqrMagnitude < 1e-4f) up = ups[i];
                ups[i] = Vector3.Slerp(ups[i], up.normalized, turn).normalized;
                // A ripple that runs back along the banner, bigger toward its free end.
                Vector3 side = Vector3.Cross(along, ups[i]);
                float free = (float)k / (BannerPoints - 1);
                Vector3 centre = points[i] + side * (Mathf.Sin(time * 9f - k * 0.45f) * height * 0.08f * free);
                vertices[k * 2] = centre + ups[i] * (height / 2f);
                vertices[k * 2 + 1] = centre - ups[i] * (height / 2f);
            }
            mesh.SetVertices(vertices);
            mesh.RecalculateBounds();

            // The bridle: from the banner's top corner, to the rope's end, to its bottom corner.
            bridle.SetPosition(0, vertices[0]);
            bridle.SetPosition(1, points[RopePoints - 2]);
            bridle.SetPosition(2, vertices[1]);
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            var look = GetComponent<MeshRenderer>();
            if (look && look.sharedMaterial) Destroy(look.sharedMaterial);
            if (rope && rope.sharedMaterial) Destroy(rope.sharedMaterial);
        }
    }
}
