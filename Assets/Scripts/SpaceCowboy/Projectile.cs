using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// One bullet. It flies straight and checks the whole stretch it covers each frame,
    /// so even a very fast shot can't skip through something thin.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        static readonly RaycastHit[] hits = new RaycastHit[16];

        ShipWeapon weapon;
        Rigidbody shooter;
        Vector3 direction;
        float distanceLeft;

        public void Launch(ShipWeapon from, Rigidbody ownBody, Vector3 position, Vector3 heading)
        {
            weapon = from;
            shooter = ownBody;
            direction = heading;
            distanceLeft = from.range;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(heading));
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (!weapon)
            {
                Destroy(gameObject); // the ship that fired it is gone
                return;
            }
            float step = weapon.bulletSpeed * Time.deltaTime;
            if (Cast(transform.position, direction, step, shooter, out var hit))
            {
                Hit(hit);
                return;
            }
            transform.position += direction * step;
            distanceLeft -= step;
            if (distanceLeft <= 0f) weapon.Return(this);
        }

        void Hit(RaycastHit hit)
        {
            var target = hit.collider.GetComponentInParent<Damageable>();
            if (target && !target.IsDead)
            {
                target.TakeDamage(weapon.damage, weapon.gameObject);
                weapon.ReportHit(target);
            }
            if (hit.rigidbody && !hit.rigidbody.isKinematic)
                hit.rigidbody.AddForceAtPosition(direction * weapon.knockback, hit.point, ForceMode.Impulse);
            weapon.Return(this);
        }

        /// <summary>The nearest solid thing along a line, skipping the ship that's shooting and invisible trigger zones.</summary>
        public static bool Cast(Vector3 from, Vector3 direction, float distance, Rigidbody shooter, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            int count = Physics.RaycastNonAlloc(from, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (shooter && hits[i].rigidbody == shooter) continue;
                if (found && hits[i].distance >= nearest.distance) continue;
                nearest = hits[i];
                found = true;
            }
            return found;
        }
    }
}
