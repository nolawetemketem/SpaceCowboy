using System;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// Anything that can be shot and destroyed. Put it on a ship (or a target) and bullets take health from it.
    /// What dying means, like an explosion, a respawn or a score, is up to whatever listens to Died.
    ///   Team:   ships on the same team can't hurt each other. -1 = no team (anyone can hit it).
    ///   Shield: extra health on top, used up first (the Tank's special). It lasts until it's broken.
    /// </summary>
    public class Damageable : MonoBehaviour
    {
        public float maxHealth = 100f;
        [Tooltip("Ships on the same team can't hurt each other. -1 = no team.")]
        public int team = -1;
        [Tooltip("Health comes back this fast (per second) once it hasn't been hit for regenDelay seconds. 0 = never.")]
        public float regenPerSecond = 0f;
        public float regenDelay = 5f;

        float sinceHit;
        Collider shape;

        public float Health { get; private set; }
        public float Shield { get; private set; }
        public bool IsDead => Health <= 0f;
        /// <summary>Can it be aimed at: alive, switched on, and solid (a Phantom ghost has its collider off).</summary>
        public bool Targetable => !IsDead && isActiveAndEnabled && (!shape || shape.enabled);

        /// <summary>Called after a hit: how much damage, and who did it.</summary>
        public event Action<float, GameObject> Damaged;
        /// <summary>Called once when health runs out, with who landed the last hit.</summary>
        public event Action<GameObject> Died;
        /// <summary>Called when anything dies anywhere, with what died and who did it (for scores and killstreaks).</summary>
        public static event Action<Damageable, GameObject> AnyDied;

        void Awake()
        {
            Health = maxHealth;
            shape = GetComponent<Collider>();
        }

        void Update()
        {
            if (regenPerSecond <= 0f) return;
            sinceHit += Time.deltaTime;
            if (!IsDead && sinceHit > regenDelay && Health < maxHealth)
                Health = Mathf.Min(maxHealth, Health + regenPerSecond * Time.deltaTime);
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (IsDead) return;
            sinceHit = 0f;
            float absorbed = Mathf.Min(Shield, amount);
            Shield -= absorbed;
            Health = Mathf.Max(0f, Health - (amount - absorbed));
            Damaged?.Invoke(amount, source);
            if (!IsDead) return;
            Shield = 0f;
            Died?.Invoke(source);
            AnyDied?.Invoke(this, source);
        }

        /// <summary>Extra health on top that's used up first.</summary>
        public void AddShield(float amount) => Shield += amount;

        /// <summary>Back to full health, no shield (for a respawn).</summary>
        public void Restore()
        {
            Health = maxHealth;
            Shield = 0f;
        }

        /// <summary>Would this hit be friendly fire? (Both on the same team.)</summary>
        public bool SameTeamAs(GameObject other)
        {
            if (team < 0 || !other) return false;
            var them = other.GetComponentInParent<Damageable>();
            return them && them != this && them.team == team;
        }
    }
}
