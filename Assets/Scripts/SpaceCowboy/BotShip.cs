using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// A computer pilot's ship, used the same way in Training and in matches: a copy of your ship's model in its
    /// own color, with health, a BotPilot flying it, the break-apart when destroyed and the hologram return.
    /// It flashes white when hit and reddens as it runs low. Once its wreck is gone, respawn puts it back somewhere.
    /// Build them with BotShip.Build.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(Rigidbody), typeof(ShipDestruction))]
    public class BotShip : MonoBehaviour
    {
        /// <summary>How much a bot can take. You have 100.</summary>
        public const float Health = 60f;
        const float FlashSeconds = 0.08f;

        /// <summary>Bot ship colors, one per seat (free for all, and Training). No red: that's your ship's color.</summary>
        public static readonly Color[] Colors =
            { Palette.Amber, Palette.Hex("#2fc4bd"), Palette.Hex("#b46cff"), Palette.Hex("#4c7fd9"), Palette.Hex("#7ad94a"), Palette.Hex("#f7d51d") };

        Damageable health;
        Rigidbody body;
        ShipDestruction destruction;
        Material hull, accent;
        Color hullColor, accentColor;
        float flash;

        /// <summary>
        /// Make a bot: a copy of a ship's model (minus its scripts and colliders) in this color, flying itself.
        ///   fightWho: everyone it fights. respawn: called once its wreck is gone, to put it back somewhere.
        /// </summary>
        public static BotShip Build(Transform parent, string name, Transform model, BoxCollider shape, Color color, int team,
            ArenaMap map, Func<IEnumerable<Damageable>> fightWho, Action<BotShip> respawn)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var copy = Instantiate(model.gameObject, go.transform);
            copy.name = "Model";
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(script);
            foreach (var part in copy.GetComponentsInChildren<Collider>(true)) Destroy(part);
            // Anything carried on the original (a towed banner, a shield) isn't part of the copy.
            foreach (Transform extra in copy.transform)
                if (extra.name == "Carried Flag" || extra.name == "Shield") Destroy(extra.gameObject);

            var hull = Palette.TechToon(color);
            var accent = Palette.TechToon(Color.Lerp(color, Color.black, 0.4f));
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
            var life = go.AddComponent<Damageable>();
            life.maxHealth = Health;
            life.team = team;

            go.AddComponent<ShipDestruction>().Setup(copy.transform);
            var ship = go.AddComponent<BotShip>();
            ship.Setup(respawn, hull, accent);
            go.AddComponent<BotPilot>().Setup(fightWho, map);
            return ship;
        }

        void Setup(Action<BotShip> respawn, Material hullMaterial, Material accentMaterial)
        {
            hull = hullMaterial;
            accent = accentMaterial;
            hullColor = hull.GetColor("_BaseColor");
            accentColor = accent.GetColor("_BaseColor");
            health = GetComponent<Damageable>();
            body = GetComponent<Rigidbody>();
            destruction = GetComponent<ShipDestruction>();
            health.Damaged += (_, _) =>
            {
                flash = health.IsDead ? 0f : FlashSeconds;
                Paint();
            };
            destruction.WreckGone += () => respawn(this);
            destruction.Materialized += Paint;
        }

        /// <summary>Go to a spot, facing a point. If it was destroyed, it's projected back there.</summary>
        public void PlaceAt(Vector3 spot, Vector3 lookAt)
        {
            Quaternion rotation = Quaternion.LookRotation(lookAt - spot, Vector3.up);
            body.position = spot;
            body.rotation = rotation;
            transform.SetPositionAndRotation(spot, rotation);
            if (destruction.Wrecked) destruction.Materialize();
            else health.Restore();
        }

        void Update()
        {
            if (flash <= 0f) return;
            flash -= Time.deltaTime;
            if (flash <= 0f) Paint();
        }

        /// <summary>White for a moment when hit; otherwise reddening as health runs out. The wreck keeps the ship's own color.</summary>
        void Paint()
        {
            float hurt = health.IsDead ? 0f : 1f - health.Health / health.maxHealth;
            hull.SetColor("_BaseColor", flash > 0f ? Color.white : Color.Lerp(hullColor, Palette.Blush, hurt));
            accent.SetColor("_BaseColor", flash > 0f ? Color.white : accentColor);
        }
    }
}
