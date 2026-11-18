using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// A computer pilot for the empty seats in a match. It flies its ship, picks the nearest enemy it can see,
    /// chases it, leads its shots, and fires in bursts when its nose is on target. It keeps off the ground and walls,
    /// breaks away instead of ramming, and patrols when there's nobody to fight.
    ///
    /// It doesn't use the player's flight model: the ship is kinematic and moved directly, turning at a fixed rate,
    /// which is simpler and easy to tune. It can't see a Phantom ghost (its colliders are off while it's invisible).
    /// The difficulty numbers are at the top.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(ShipWeapon), typeof(Damageable))]
    public class BotPilot : MonoBehaviour
    {
        [Header("Flying")]
        [Tooltip("Units per second, cruising. The player's top speed is 125.")]
        public float cruiseSpeed = 95f;
        [Tooltip("Units per second while chasing someone far away.")]
        public float chaseSpeed = 118f;
        [Tooltip("How fast it turns, in degrees per second.")]
        public float turnRate = 80f;
        [Tooltip("It stays at least this high above the ground.")]
        public float minHeight = 25f;
        const float ShipRadius = 5f; // about half the ship's width, for keeping it out of walls

        [Header("Fighting")]
        [Tooltip("It notices enemies this close.")]
        public float sightRange = 1100f;
        [Tooltip("It fires when its nose is within this many degrees of where it's aiming.")]
        public float fireCone = 4f;
        [Tooltip("Seconds its aim must be on target before it starts firing (its reaction time).")]
        public float reaction = 0.35f;
        [Tooltip("Seconds of firing, then seconds of rest.")]
        public float burst = 1.1f, rest = 0.7f;
        [Tooltip("Closer than this it breaks away rather than ramming.")]
        public float breakAwayDistance = 55f;

        Rigidbody body;
        ShipWeapon weapon;
        Damageable health;
        Func<IEnumerable<Damageable>> enemies;
        ArenaMap map;

        Damageable target;
        Vector3 targetLastSeen, targetVelocity;
        Vector3 patrolSpot;
        float lookAgain, onTarget, burstClock, breakAwayLeft;
        Vector3 breakAwayDirection;

        /// <summary>enemies: everyone this pilot fights. map: for keeping inside it.</summary>
        public void Setup(Func<IEnumerable<Damageable>> whoToFight, ArenaMap arena)
        {
            enemies = whoToFight;
            map = arena;
            body = GetComponent<Rigidbody>();
            weapon = GetComponent<ShipWeapon>();
            health = GetComponent<Damageable>();
            // Lasers stay the gun's default amber, like yours: one color for every shot in the game.
            // A little weaker and less accurate than you, so a 1v1 is fair.
            weapon.fireRate = 7f;
            weapon.damage = 6f;
            weapon.spread = 1.6f;
            PickPatrolSpot();
            lookAgain = UnityEngine.Random.value * 0.5f;
        }

        void FixedUpdate()
        {
            if (enemies == null) return;
            float dt = Time.fixedDeltaTime;
            if (health.IsDead) // wrecked, waiting to come back
            {
                weapon.triggerHeld = false;
                target = null;
                return;
            }

            lookAgain -= dt;
            if (lookAgain <= 0f)
            {
                target = Nearest();
                lookAgain = 0.5f;
            }
            if (target && !CanSee(target)) target = null;
            if (target) TrackTarget(dt);

            Vector3 here = transform.position;
            Vector3 wanted = Steer(here, out float speed, out Vector3 aimAt);
            wanted = Avoid(here, wanted, speed);

            // Turn toward the wanted heading at the turn rate, banking into the turn.
            Quaternion facing = Quaternion.LookRotation(wanted, Vector3.up);
            float turnSide = Vector3.Dot(transform.right, wanted);
            facing *= Quaternion.Euler(0f, 0f, -turnSide * 50f);
            Quaternion turned = Quaternion.RotateTowards(body.rotation, facing, turnRate * dt);
            body.MoveRotation(turned);
            body.MovePosition(here + KeepOutOfWalls(here, turned * Vector3.forward * speed * dt));

            Shoot(here, aimAt, dt);
        }

        // --- Choosing a target --------------------------------------------------------------------

        Damageable Nearest()
        {
            Damageable best = null;
            float bestDistance = sightRange;
            foreach (var enemy in enemies())
            {
                if (!CanSee(enemy)) continue;
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < bestDistance)
                {
                    best = enemy;
                    bestDistance = distance;
                }
            }
            if (best != target && best) targetLastSeen = best.transform.position;
            if (best != target) targetVelocity = Vector3.zero;
            return best;
        }

        /// <summary>Alive, in range, and not a ghost. (Walls don't hide ships: bots know where you are, like radar.)</summary>
        bool CanSee(Damageable enemy) =>
            enemy && enemy != health && enemy.Targetable
            && (enemy.transform.position - transform.position).sqrMagnitude < sightRange * sightRange;

        /// <summary>How fast the target is going, worked out from where it was (works for any ship, kinematic or not).</summary>
        void TrackTarget(float dt)
        {
            Vector3 now = target.transform.position;
            Vector3 velocity = (now - targetLastSeen) / dt;
            if (velocity.sqrMagnitude > 400f * 400f) velocity = Vector3.zero; // it was put back at a spawn
            targetVelocity = Vector3.Lerp(targetVelocity, velocity, 0.2f);
            targetLastSeen = now;
        }

        // --- Flying ---------------------------------------------------------------------------------

        /// <summary>Which way to go and how fast, and where the guns should point.</summary>
        Vector3 Steer(Vector3 here, out float speed, out Vector3 aimAt)
        {
            speed = cruiseSpeed;
            aimAt = here + transform.forward * 100f;

            if (breakAwayLeft > 0f)
            {
                breakAwayLeft -= Time.fixedDeltaTime;
                speed = chaseSpeed;
                return breakAwayDirection;
            }

            if (!target)
            {
                if ((patrolSpot - here).sqrMagnitude < 90f * 90f) PickPatrolSpot();
                return (patrolSpot - here).normalized;
            }

            // Lead the shot: where the target will be when a bullet gets there.
            Vector3 there = target.transform.position;
            float distance = Vector3.Distance(here, there);
            float flight = distance / Mathf.Max(weapon.bulletSpeed, 1f);
            aimAt = there + targetVelocity * flight;

            if (distance < breakAwayDistance)
            {
                // Too close: peel off to the side and up, then come round again.
                Vector3 away = (here - there).normalized;
                breakAwayDirection = (away + transform.right * (UnityEngine.Random.value < 0.5f ? -1f : 1f) + Vector3.up * 0.4f).normalized;
                breakAwayLeft = 1.4f;
                return breakAwayDirection;
            }
            speed = distance > 300f ? chaseSpeed : Mathf.Lerp(cruiseSpeed * 0.8f, cruiseSpeed, distance / 300f);
            return (aimAt - here).normalized;
        }

        /// <summary>Bend the heading away from the ground, walls and anything straight ahead, and back into the map.</summary>
        Vector3 Avoid(Vector3 here, Vector3 wanted, float speed)
        {
            // Something solid ahead: turn along its surface (and up).
            float look = Mathf.Max(80f, speed * 1.6f);
            if (Physics.SphereCast(here, 6f, transform.forward, out var hit, look, ~0, QueryTriggerInteraction.Ignore)
                && hit.rigidbody != body && !hit.collider.GetComponentInParent<Damageable>()) // ships: see breaking away
            {
                float urgency = 1f - hit.distance / look;
                wanted = (wanted + hit.normal * (1f + urgency * 3f) + Vector3.up * urgency).normalized;
            }

            // The ground: climb when low.
            float height = here.y - ArenaMap.GroundBelow(here).y;
            if (height < minHeight * 2f)
            {
                float push = Mathf.Clamp01(1f - (height - minHeight) / minHeight);
                wanted = (wanted + Vector3.up * push * 2f).normalized;
            }

            // The map's edge: head back toward the middle of the space (in a ring tube, the tube's middle line).
            if (map && !map.Contains(here + transform.forward * look))
                wanted = (wanted + (map.Inside(here) - here).normalized * 2f).normalized;

            if (wanted.sqrMagnitude < 1e-4f) wanted = transform.forward;
            return wanted;
        }

        /// <summary>
        /// The ship is moved directly (not by physics), so nothing else stops it going through a wall: before each
        /// step, anything solid in the way, or the map's edge, takes away the part of the move that goes into it,
        /// and it slides along instead.
        /// </summary>
        Vector3 KeepOutOfWalls(Vector3 here, Vector3 move)
        {
            float length = move.magnitude;
            if (length < 1e-5f) return move;
            if (Physics.SphereCast(here, ShipRadius, move / length, out var hit, length + ShipRadius, ~0, QueryTriggerInteraction.Ignore)
                && hit.rigidbody != body && !hit.collider.GetComponentInParent<Damageable>())
                move = Vector3.ProjectOnPlane(move, hit.normal);

            // Never further out: if this step ends outside the map, it can only go along the edge or back in.
            if (map && !map.Contains(here + move))
            {
                Vector3 outward = (here - map.Inside(here)).normalized;
                move -= outward * Mathf.Max(0f, Vector3.Dot(move, outward));
            }
            return move;
        }

        /// <summary>Somewhere in the map to fly to when there's nobody to fight.</summary>
        void PickPatrolSpot()
        {
            // A random spot within reach; if it's outside the map (in a wall, or the donut's hole in a ring tube),
            // the nearest middle of the map instead.
            Vector3 spot = transform.position + new Vector3(UnityEngine.Random.Range(-500f, 500f), UnityEngine.Random.Range(-60f, 120f), UnityEngine.Random.Range(-500f, 500f));
            patrolSpot = !map || map.Contains(spot) ? spot : map.Inside(spot);
        }

        // --- Shooting -------------------------------------------------------------------------------

        void Shoot(Vector3 here, Vector3 aimAt, float dt)
        {
            bool lined = false;
            if (target)
            {
                Vector3 toAim = aimAt - here;
                lined = toAim.magnitude < weapon.range * 0.85f
                        && Vector3.Angle(transform.forward, toAim) < fireCone
                        && ClearShot(here, target);
            }
            onTarget = lined ? onTarget + dt : 0f;

            // Bursts: fire for a while, rest for a while, while it's on target.
            burstClock += dt;
            if (burstClock > burst + rest) burstClock = 0f;
            weapon.triggerHeld = onTarget >= reaction && burstClock < burst;
        }

        /// <summary>Nothing solid between the guns and the target (teammates count as in the way).</summary>
        bool ClearShot(Vector3 here, Damageable enemy)
        {
            Vector3 to = enemy.transform.position - here;
            if (!Projectile.Cast(here, to.normalized, to.magnitude, body, out var hit)) return true;
            var thing = hit.collider.GetComponentInParent<Damageable>();
            return thing == enemy;
        }
    }
}
