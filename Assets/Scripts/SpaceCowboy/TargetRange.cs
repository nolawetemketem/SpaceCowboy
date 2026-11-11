using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// Practice ships to shoot at: teal copies of the player's ship, spread out in front of the start spot
    /// at different distances and heights. A destroyed one breaks apart and is projected back somewhere new.
    /// </summary>
    public class TargetRange : MonoBehaviour
    {
        const float NearestDistance = 130f, FarthestDistance = 360f, WidestAngle = 70f;
        const float LowestHeight = 15f, HighestHeight = 100f, GroundClearance = 15f;
        const float TargetHealth = 50f;

        TrainingArena arena;

        public static TargetRange Create(TrainingArena arena, HoverCraft craft, int count)
        {
            var range = new GameObject("Practice Targets").AddComponent<TargetRange>();
            range.arena = arena;
            var model = craft.transform.Find("BankPivot");
            var shape = craft.GetComponent<BoxCollider>();
            for (int i = 0; i < count; i++) range.Build(model, shape, i);
            return range;
        }

        void Build(Transform model, BoxCollider shape, int index)
        {
            var go = new GameObject($"Target {index + 1}");
            go.transform.SetParent(transform, false);

            // The same look as the player's ship, minus its scripts and colliders.
            var copy = Instantiate(model.gameObject, go.transform);
            copy.name = "Model";
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(script);
            foreach (var part in copy.GetComponentsInChildren<Collider>(true)) Destroy(part);

            var hull = Palette.Toon(Palette.Teal);
            var accent = Palette.Toon(Palette.Amber);
            foreach (var part in copy.GetComponentsInChildren<Renderer>(true))
            {
                var materials = part.sharedMaterials;
                bool engine = part.name.Contains("Engine");
                for (int m = 0; m < materials.Length; m++) materials[m] = engine ? accent : hull;
                part.sharedMaterials = materials;
            }

            var box = go.AddComponent<BoxCollider>();
            box.center = shape.center;
            box.size = shape.size;
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var health = go.AddComponent<Damageable>();
            health.maxHealth = TargetHealth;

            go.AddComponent<ShipDestruction>().Setup(copy.transform);
            var target = go.AddComponent<PracticeTarget>();
            target.Setup(this, hull, accent);
            Respawn(target);
        }

        /// <summary>Put a target at a fresh spot ahead of the start position.</summary>
        public void Respawn(PracticeTarget target)
        {
            Vector3 start = arena.CraftStartPosition;
            Vector3 ahead = Quaternion.AngleAxis(Random.Range(-WidestAngle, WidestAngle), Vector3.up) * arena.transform.forward;
            Vector3 spot = start + ahead * Random.Range(NearestDistance, FarthestDistance) + Vector3.up * Random.Range(LowestHeight, HighestHeight);

            // Never inside a dune or mesa.
            Vector3 local = arena.transform.InverseTransformPoint(spot);
            spot.y = Mathf.Max(spot.y, arena.OnGround(local.x, local.z).y + GroundClearance);
            target.PlaceAt(spot, start);
        }
    }
}
