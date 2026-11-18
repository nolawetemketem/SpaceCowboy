using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// A placeholder city: a round floor with a grid of plain tower blocks on it, with open lanes left clear
    /// so ships can fly through. Its floor is centered on this object. Only the basics; swap in a real city later.
    ///   The main lane runs along Z through the middle; a cross street runs along X.
    ///   A plaza is kept clear at the middle, and around each end (where the flags go).
    /// Towers are kept under the dome over the city (domeRadius, measured from domeCentre). Ships bump into them.
    /// </summary>
    public class CityBlocks : GeneratedShape
    {
        public float floorRadius = 580f;
        [Tooltip("The dome over the city: towers stay under it.")]
        public float domeRadius = 650f;
        [Tooltip("Where the dome's center is, relative to this floor (the floor sits below it).")]
        public float domeCentreHeight = 260f;
        [Tooltip("Distance between the centers of neighbouring blocks.")]
        public float blockSpacing = 70f;
        public float blockWidth = 44f;
        public float shortest = 30f, tallest = 240f;
        [Tooltip("Half the width of the open lanes.")]
        public float laneHalfWidth = 45f;
        [Tooltip("Clear areas: one in the middle, and one at each end of the main lane, this far out.")]
        public float plazaRadius = 110f, endPlazaDistance = 420f;
        public Color floor = Palette.Hex("#3a3631");
        public Color towerA = Palette.Hex("#6e6a66"), towerB = Palette.Hex("#57534f"), towerC = Palette.Hex("#837b70");
        public int seed = 11;

        protected override void Build(Transform root)
        {
            var random = new System.Random(seed);
            float Next() => (float)random.NextDouble();

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Keep(ground);
            ground.name = "City Floor";
            ground.transform.SetParent(root, false);
            ground.transform.localScale = new Vector3(floorRadius * 2f, 2f, floorRadius * 2f);
            ground.transform.localPosition = new Vector3(0f, -2f, 0f);
            ground.GetComponent<Renderer>().sharedMaterial = Keep(Palette.Toon(floor));
            // The cylinder's own collider is a capsule; a box is the right shape for a flat floor.
            DestroyNow(ground.GetComponent<Collider>());
            var box = ground.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 2f, 1f); // the cylinder mesh is 1 wide and 2 tall

            var colors = new[] { Keep(Palette.Toon(towerA)), Keep(Palette.Toon(towerB)), Keep(Palette.Toon(towerC)) };
            int count = Mathf.CeilToInt(floorRadius / blockSpacing);
            for (int gx = -count; gx <= count; gx++)
                for (int gz = -count; gz <= count; gz++)
                {
                    float x = gx * blockSpacing, z = gz * blockSpacing;
                    float fromMiddle = new Vector2(x, z).magnitude;
                    if (fromMiddle + blockWidth > floorRadius) continue;
                    if (Mathf.Abs(x) < laneHalfWidth + blockWidth / 2f) continue;        // main lane
                    if (Mathf.Abs(z) < laneHalfWidth + blockWidth / 2f) continue;        // cross street
                    if (fromMiddle < plazaRadius) continue;
                    if (new Vector2(x, Mathf.Abs(z) - endPlazaDistance).magnitude < plazaRadius) continue;

                    // As tall as allowed, but no taller than the dome above this spot.
                    float roomUnderDome = domeCentreHeight + Mathf.Sqrt(Mathf.Max(0f, domeRadius * domeRadius - fromMiddle * fromMiddle)) - 25f;
                    float height = Mathf.Min(Mathf.Lerp(shortest, tallest, Next() * Next()), roomUnderDome);
                    if (height < shortest) continue;

                    var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Keep(tower);
                    tower.name = "Tower";
                    tower.transform.SetParent(root, false);
                    float width = blockWidth * Mathf.Lerp(0.7f, 1f, Next());
                    tower.transform.localPosition = new Vector3(x, height / 2f, z);
                    tower.transform.localScale = new Vector3(width, height, width * Mathf.Lerp(0.7f, 1f, Next()));
                    var look = tower.GetComponent<Renderer>();
                    look.sharedMaterial = colors[random.Next(colors.Length)];
                    look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
        }

        static void DestroyNow(Object thing)
        {
            if (Application.isPlaying) Destroy(thing);
            else DestroyImmediate(thing);
        }
    }
}
