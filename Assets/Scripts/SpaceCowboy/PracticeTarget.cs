using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// A ship to shoot at. It hangs in the air, drifting a little, and reddens as it takes damage.
    /// ShipDestruction blows it apart; once the wreck is gone it's projected back somewhere new. TargetRange builds these.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(Rigidbody), typeof(ShipDestruction))]
    public class PracticeTarget : MonoBehaviour
    {
        const float FlashSeconds = 0.08f;

        /// <summary>How far it drifts from its spot, side to side and up and down.</summary>
        public float driftSide = 18f, driftUp = 6f;

        Damageable health;
        Rigidbody body;
        ShipDestruction destruction;
        Material hull, accent;
        Color hullColor, accentColor;
        Vector3 home, side;
        float phase, flash;

        public void Setup(TargetRange owner, Material hullMaterial, Material accentMaterial)
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
            destruction.WreckGone += () => owner.Respawn(this);
            destruction.Materialized += Paint;
            phase = Random.value * 10f;
        }

        /// <summary>Go to a spot, facing a point. If it was destroyed, it's projected back there.</summary>
        public void PlaceAt(Vector3 spot, Vector3 lookAt)
        {
            home = spot;
            Quaternion rotation = Quaternion.LookRotation(lookAt - spot, Vector3.up);
            side = rotation * Vector3.right;
            body.position = spot;
            body.rotation = rotation;
            transform.SetPositionAndRotation(spot, rotation);
            if (destruction.Wrecked) destruction.Materialize();
            else health.Restore();
        }

        void FixedUpdate()
        {
            float t = Time.time + phase;
            body.MovePosition(home + side * (Mathf.Sin(t * 0.5f) * driftSide) + Vector3.up * (Mathf.Sin(t * 0.8f) * driftUp));
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
