using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// A gun you can put on any ship. Each ship gets its own numbers here, so a heavy ship can fire slow,
    /// hard shots and a light one quick, weak ones. Bullets leave the barrels and meet at the aim point
    /// straight ahead of the nose, which is what the HUD's cursor shows.
    ///
    /// A player's ship fires from its CraftInput. Anything else (an AI ship, an online player)
    /// just sets triggerHeld, so the same gun works for every ship.
    /// </summary>
    public class ShipWeapon : MonoBehaviour
    {
        /// <summary>Bullets aim no closer than this, so they don't splay outward when something is right in front.</summary>
        const float MinAimDistance = 40f;

        [Header("Firing")]
        [Tooltip("Shots per second while the trigger is held.")]
        public float fireRate = 10f;
        [Tooltip("Units per second. Well above the ship's top speed, so shots land where the cursor is.")]
        public float bulletSpeed = 650f;
        [Tooltip("How far a shot travels before it disappears.")]
        public float range = 900f;
        [Tooltip("Health a shot takes from anything that has a Damageable component.")]
        public float damage = 10f;
        [Tooltip("How hard a shot shoves what it hits (other ships).")]
        public float knockback = 0.6f;
        [Tooltip("Random inaccuracy, in degrees. 0 = every shot goes exactly to the cursor.")]
        public float spread = 0.35f;

        [Header("Look")]
        public Color bulletColor = Palette.Amber;
        public float bulletLength = 7f;
        public float bulletWidth = 0.45f;

        [Header("Barrels")]
        [Tooltip("Where shots come from; they take turns. Leave empty for two barrels at the front of the ship's collider, " +
                 "so any ship model works without setup.")]
        public Transform[] muzzles;

        [Tooltip("True fires. A ship with a CraftInput sets this itself; anything else can set it from code.")]
        public bool triggerHeld;

        Rigidbody body;
        CraftInput input;
        HoverCraft craft;
        BoxCollider box;
        Vector3[] autoBarrels;
        int nextBarrel;
        float cooldown;
        Transform bulletRoot;
        Material bulletMaterial;
        readonly Stack<Projectile> spare = new();

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            input = GetComponent<CraftInput>();
            craft = GetComponent<HoverCraft>();
            box = GetComponent<BoxCollider>();
        }

        void Update()
        {
            if (input) triggerHeld = input.FireHeld && (!craft || craft.controlsEnabled);

            cooldown -= Time.deltaTime;
            if (!triggerHeld)
            {
                cooldown = Mathf.Max(cooldown, 0f);
                return;
            }
            while (cooldown <= 0f)
            {
                Fire();
                cooldown += 1f / fireRate;
            }
        }

        Vector3 Centre => box ? transform.TransformPoint(box.center) : transform.position;

        /// <summary>A shot of this weapon landed on something with health.</summary>
        public event System.Action<Damageable> LandedHit;
        /// <summary>A shot of this weapon finished something off.</summary>
        public event System.Action<Damageable> Destroyed;

        /// <summary>Called by this weapon's bullets when they damage something.</summary>
        public void ReportHit(Damageable target)
        {
            LandedHit?.Invoke(target);
            if (target.IsDead) Destroyed?.Invoke(target);
        }

        /// <summary>The target right under the cursor, if there is one in range.</summary>
        public Damageable AimTarget =>
            Projectile.Cast(Centre, transform.forward, range, body, out var hit) ? hit.collider.GetComponentInParent<Damageable>() : null;

        /// <summary>Where shots are headed: the first thing straight ahead of the nose, or the end of their range.</summary>
        public Vector3 AimPoint
        {
            get
            {
                Vector3 from = Centre;
                float distance = Projectile.Cast(from, transform.forward, range, body, out var hit)
                    ? Mathf.Max(hit.distance, MinAimDistance)
                    : range;
                return from + transform.forward * distance;
            }
        }

        void Fire()
        {
            Vector3 muzzle = NextMuzzle();
            Vector3 direction = (AimPoint - muzzle).normalized;
            Vector2 miss = Random.insideUnitCircle * Mathf.Tan(spread * Mathf.Deg2Rad);
            direction = (direction + transform.right * miss.x + transform.up * miss.y).normalized;

            var bullet = spare.Count > 0 ? spare.Pop() : MakeBullet();
            bullet.Launch(this, body, muzzle, direction);
        }

        Vector3 NextMuzzle()
        {
            nextBarrel++;
            if (muzzles != null && muzzles.Length > 0) return muzzles[nextBarrel % muzzles.Length].position;
            if (autoBarrels == null)
            {
                float side = box ? box.size.x * 0.2f : 1f;
                Vector3 front = box ? box.center + Vector3.forward * (box.size.z / 2f) : Vector3.zero;
                autoBarrels = new[] { front + Vector3.left * side, front + Vector3.right * side };
            }
            return transform.TransformPoint(autoBarrels[nextBarrel % autoBarrels.Length]);
        }

        Projectile MakeBullet()
        {
            if (!bulletRoot) bulletRoot = new GameObject(name + " Bullets").transform;
            if (!bulletMaterial) bulletMaterial = Palette.Unlit(bulletColor);
            var go = Palette.Shape(PrimitiveType.Cube, "Bullet", bulletRoot, Vector3.zero,
                new Vector3(bulletWidth, bulletWidth, bulletLength), bulletMaterial);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.AddComponent<Projectile>();
        }

        /// <summary>A bullet that's done goes back to be reused.</summary>
        public void Return(Projectile bullet)
        {
            bullet.gameObject.SetActive(false);
            spare.Push(bullet);
        }
    }
}
