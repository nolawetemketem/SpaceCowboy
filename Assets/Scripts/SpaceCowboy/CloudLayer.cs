using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// A sea of soft toon clouds: puffy clumps of balls scattered in a wide band around this object.
    /// They're only for looks: ships fly straight through them.
    /// </summary>
    public class CloudLayer : GeneratedShape
    {
        public int clouds = 60;
        [Tooltip("Clouds are scattered out to this distance from this object.")]
        public float spread = 1200f;
        [Tooltip("And between these heights, relative to this object.")]
        public float lowest = -320f, highest = -120f;
        [Tooltip("How big a cloud's balls are.")]
        public float puffSize = 60f;
        public Color color = Palette.Hex("#f4efe8");
        public Color shade = Palette.Hex("#d9d2cc");
        public int seed = 3;

        protected override void Build(Transform root)
        {
            var random = new System.Random(seed);
            float Next() => (float)random.NextDouble();
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var light = Keep(Palette.Toon(color));
            var dark = Keep(Palette.Toon(shade));

            for (int c = 0; c < clouds; c++)
            {
                float angle = Next() * Mathf.PI * 2f;
                float distance = Mathf.Sqrt(Next()) * spread;
                var cloud = new GameObject("Cloud").transform;
                cloud.SetParent(root, false);
                cloud.localPosition = new Vector3(Mathf.Sin(angle) * distance, Mathf.Lerp(lowest, highest, Next()), Mathf.Cos(angle) * distance);

                int puffs = 4 + random.Next(5);
                for (int p = 0; p < puffs; p++)
                {
                    float size = puffSize * Mathf.Lerp(0.6f, 1.3f, Next());
                    var puff = new GameObject("Puff");
                    puff.transform.SetParent(cloud, false);
                    puff.transform.localPosition = new Vector3((Next() - 0.5f) * puffSize * 3f, (Next() - 0.3f) * puffSize * 0.6f, (Next() - 0.5f) * puffSize * 1.6f);
                    puff.transform.localScale = new Vector3(size * 1.3f, size * 0.8f, size);
                    puff.AddComponent<MeshFilter>().sharedMesh = sphere;
                    var look = puff.AddComponent<MeshRenderer>();
                    look.sharedMaterial = p % 3 == 2 ? dark : light;
                    look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }
    }
}
