using System;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// Anything that can be shot and destroyed. Put it on a ship (or a target) and bullets take health from it.
    /// What dying means, like an explosion, a respawn or a score, is up to whatever listens to Died.
    /// </summary>
    public class Damageable : MonoBehaviour
    {
        public float maxHealth = 100f;

        public float Health { get; private set; }
        public bool IsDead => Health <= 0f;

        /// <summary>Called after a hit: how much damage, and who did it.</summary>
        public event Action<float, GameObject> Damaged;
        /// <summary>Called once when health runs out, with who landed the last hit.</summary>
        public event Action<GameObject> Died;

        void Awake() => Health = maxHealth;

        public void TakeDamage(float amount, GameObject source)
        {
            if (IsDead) return;
            Health = Mathf.Max(0f, Health - amount);
            Damaged?.Invoke(amount, source);
            if (IsDead) Died?.Invoke(source);
        }

        /// <summary>Back to full health (for a respawn).</summary>
        public void Restore() => Health = maxHealth;
    }
}
