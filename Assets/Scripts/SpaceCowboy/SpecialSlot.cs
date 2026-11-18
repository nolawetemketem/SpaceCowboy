using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// Your fighter's special. A streak of 3 kills without dying charges it; press the Special button
    /// (X on a controller, E on the keyboard) to use it whenever you like. Dying loses the streak and the charge,
    /// and cuts short a special that's running. Every 3 kills in one streak charges it again (if it's used up).
    /// The specials themselves are below, one class each.
    /// </summary>
    public class SpecialSlot : MonoBehaviour
    {
        public const int StreakToCharge = 3;

        public Fighter Fighter { get; private set; }
        public int Streak { get; private set; }
        public bool Charged { get; private set; }

        internal HoverCraft craft;
        internal CraftInput input;
        internal Damageable health;
        internal Hud hud;
        internal Func<ArenaMap> map;
        internal Func<IEnumerable<Transform>> enemies;
        Power active;
        GameObject bubble;

        public static SpecialSlot Attach(HoverCraft craft, Fighter fighter, Hud hud, Func<ArenaMap> map, Func<IEnumerable<Transform>> enemies)
        {
            var slot = craft.GetComponent<SpecialSlot>();
            if (!slot) slot = craft.gameObject.AddComponent<SpecialSlot>();
            slot.craft = craft;
            slot.input = craft.GetComponent<CraftInput>();
            slot.health = craft.GetComponent<Damageable>();
            slot.hud = hud;
            slot.map = map;
            slot.enemies = enemies;
            slot.SetFighter(fighter);
            return slot;
        }

        /// <summary>A new fighter (or a new match): no streak, no charge, nothing running.</summary>
        public void SetFighter(Fighter fighter)
        {
            Stop();
            Fighter = fighter;
            Streak = 0;
            Charged = false;
        }

        void OnEnable()
        {
            Damageable.AnyDied += Noticed;
        }

        void OnDisable()
        {
            Damageable.AnyDied -= Noticed;
            Stop();
            if (hud) hud.SetStreakRing(0f, Color.white, false);
        }

        void Start()
        {
            // Damaged comes just before Died, so a special is ended before the ship breaks apart (and the
            // Phantom's see-through look is gone before the wreck copies the ship's materials).
            health.Damaged += (_, _) =>
            {
                if (!health.IsDead) return;
                Stop();
                Streak = 0;
                Charged = false;
            };
        }

        void Noticed(Damageable victim, GameObject source)
        {
            if (Fighter == null || source != craft.gameObject || victim == health) return;
            Streak++;
            if (Streak % StreakToCharge == 0 && !Charged && active == null) Charged = true;
        }

        void Update()
        {
            if (Fighter == null) return;
            if (hud.Results.IsOpen)
            {
                hud.SetStreakRing(0f, Color.white, false);
                return;
            }
            bool live = craft.controlsEnabled && !health.IsDead;
            if (live && input.TakeSpecial())
            {
                if (active != null) active.Pressed();
                else if (Charged)
                {
                    Charged = false;
                    active = Power.For(Fighter.special, this);
                    active.Begin();
                }
                else hud.NudgeStreakRing(); // not ready yet
            }
            if (active != null && !active.Tick(Time.deltaTime)) Stop();

            ShowShield();
            ShowRing();
        }

        /// <summary>
        /// The ring around the aim cursor, no words: a third fills with each kill in a row; full and pulsing
        /// yellow when the special is ready; running down (yellow) while it's going.
        /// </summary>
        void ShowRing()
        {
            if (active != null) hud.SetStreakRing(Mathf.Clamp01(active.Fraction), Palette.Yellow, false);
            else if (Charged) hud.SetStreakRing(1f, Palette.Yellow, true);
            else hud.SetStreakRing((Streak % StreakToCharge) / (float)StreakToCharge, Palette.Amber, false);
        }

        void Stop()
        {
            if (active == null) return;
            active.End();
            active = null;
        }

        /// <summary>A see-through bubble around the ship while it has a shield (the Tank's).</summary>
        void ShowShield()
        {
            bool on = health.Shield > 0f && !health.IsDead;
            if (on && !bubble)
            {
                var box = craft.GetComponent<BoxCollider>();
                float size = box ? box.size.magnitude * 1.1f : 14f;
                var look = Palette.Hologram(Palette.HologramBlue);
                bubble = Palette.Shape(PrimitiveType.Sphere, "Shield", craft.transform, box ? box.center : Vector3.zero, Vector3.one * size, look);
                bubble.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (!on && bubble)
            {
                Destroy(bubble.GetComponent<Renderer>().sharedMaterial);
                Destroy(bubble);
            }
        }

        void OnDestroy()
        {
            if (bubble) Destroy(bubble);
        }
    }

    // --- The specials ----------------------------------------------------------------------------

    /// <summary>One use of a special, from when it's fired until it ends.</summary>
    abstract class Power
    {
        protected const float Duration = 30f;
        protected SpecialSlot slot;
        protected float left = Duration;

        public static Power For(SpecialKind kind, SpecialSlot slot)
        {
            Power power = kind switch
            {
                SpecialKind.Spy => new SpyPower(),
                SpecialKind.Phantom => new PhantomPower(),
                SpecialKind.Hopper => new HopperPower(),
                SpecialKind.Sniper => new SniperPower(),
                SpecialKind.Tank => new TankPower(),
                _ => new RacerPower(),
            };
            power.slot = slot;
            return power;
        }

        /// <summary>How much is left, 1 (just fired) to 0 (over). The streak ring shows it.</summary>
        public virtual float Fraction => left / Duration;
        public virtual void Begin() { }
        /// <summary>Called every frame. Return false when it's over.</summary>
        public virtual bool Tick(float dt) => (left -= dt) > 0f;
        /// <summary>The special button was pressed again while it's running.</summary>
        public virtual void Pressed() { }
        public virtual void End() { }

        protected Transform Ship => slot.craft.transform;
        protected Vector3 ShipCentre
        {
            get
            {
                var box = slot.craft.GetComponent<BoxCollider>();
                return box ? Ship.TransformPoint(box.center) : Ship.position;
            }
        }
    }

    /// <summary>Spy: every enemy is marked on screen, even behind walls, for 30 seconds.</summary>
    class SpyPower : Power
    {
        readonly List<Vector3> spots = new();

        public override bool Tick(float dt)
        {
            spots.Clear();
            foreach (var enemy in slot.enemies()) if (enemy && enemy.gameObject.activeInHierarchy) spots.Add(enemy.position);
            slot.hud.SetMarkers(spots, Palette.Blush);
            return base.Tick(dt);
        }

        public override void End() => slot.hud.SetMarkers(null, Color.white);
    }

    /// <summary>
    /// Phantom: for 30 seconds the ship is a see-through ghost that flies through walls, and bullets pass through it.
    /// It still can't leave the map. If it's inside something when it ends, it's put back where it was last clear.
    /// </summary>
    class PhantomPower : Power
    {
        readonly List<(Renderer part, Material[] materials)> looks = new();
        Collider[] colliders;
        Material ghost;
        Vector3 lastClear, lastInside;
        bool bounded; // started inside the map's edges, so it's kept inside them

        public override void Begin()
        {
            colliders = Ship.GetComponents<Collider>();
            foreach (var c in colliders) c.enabled = false;
            ghost = Palette.Hologram(Palette.HologramBlue);
            ghost.SetFloat("_Reveal", 1f);
            var model = Ship.Find("BankPivot") ?? Ship;
            foreach (var part in model.GetComponentsInChildren<Renderer>())
            {
                if (part.GetComponentInParent<TowedBanner>()) continue;
                looks.Add((part, part.sharedMaterials));
                var swapped = new Material[part.sharedMaterials.Length];
                for (int i = 0; i < swapped.Length; i++) swapped[i] = ghost;
                part.sharedMaterials = swapped;
            }
            lastClear = lastInside = Ship.position;
            // A map whose play area doesn't cover where you are (drawn off to one side) can't hold you in:
            // without this check you'd be snapped back every frame and couldn't move at all.
            var map = slot.map();
            bounded = map && map.Contains(Ship.position);
        }

        public override bool Tick(float dt)
        {
            var map = slot.map();
            Vector3 here = Ship.position;
            if (bounded && map && !map.Contains(here))
            {
                // Through walls, but not out of the map: back to the last spot inside it, still moving
                // (only the part of the speed heading out is dropped).
                var body = Ship.GetComponent<Rigidbody>();
                Vector3 outward = (here - lastInside).normalized;
                body.position = lastInside;
                Ship.position = lastInside;
                body.linearVelocity -= outward * Mathf.Max(0f, Vector3.Dot(body.linearVelocity, outward));
            }
            else lastInside = here;
            if (!Overlapping()) lastClear = Ship.position;
            return base.Tick(dt);
        }

        bool Overlapping()
        {
            var box = slot.craft.GetComponent<BoxCollider>();
            if (!box) return false;
            var hits = Physics.OverlapBox(Ship.TransformPoint(box.center), Vector3.Scale(box.size, Ship.lossyScale) / 2f,
                Ship.rotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits) if (!hit.transform.IsChildOf(Ship)) return true;
            return false;
        }

        public override void End()
        {
            foreach (var (part, materials) in looks) if (part) part.sharedMaterials = materials;
            if (Overlapping())
            {
                var body = Ship.GetComponent<Rigidbody>();
                body.position = lastClear;
                body.linearVelocity = Vector3.zero;
                Ship.position = lastClear;
            }
            foreach (var c in colliders) if (c) c.enabled = true;
            if (ghost) UnityEngine.Object.Destroy(ghost);
        }
    }

    /// <summary>
    /// Hopper: the first press opens a portal just ahead of you; press again to open its partner somewhere else.
    /// Flying through either one comes out of the other, at the same speed. The pair lasts 30 seconds once both are open.
    /// Anyone on your team can use them (for now, that's you).
    /// </summary>
    class HopperPower : Power
    {
        const float Radius = 24f, ReuseSeconds = 1f;
        Transform first, second;
        Vector3 lastSpot;
        float sinceJump = ReuseSeconds;
        readonly List<UnityEngine.Object> made = new();

        public override void Begin()
        {
            first = Open("Portal A");
            lastSpot = Ship.position;
        }

        public override void Pressed()
        {
            if (second) return;
            second = Open("Portal B");
            left = Duration; // the pair lasts 30 seconds from now
        }

        Transform Open(string name)
        {
            var portal = new GameObject(name).transform;
            portal.SetPositionAndRotation(ShipCentre + Ship.forward * 45f, Quaternion.LookRotation(Ship.forward, Vector3.up));
            made.Add(portal.gameObject);
            var ring = new GameObject("Ring");
            ring.transform.SetParent(portal, false);
            var mesh = CaptureTheFlag.Ring(Radius, 2.2f);
            made.Add(mesh);
            ring.AddComponent<MeshFilter>().sharedMesh = mesh;
            var glow = Palette.Unlit(Palette.Hex("#b46cff"));
            made.Add(glow);
            ring.AddComponent<MeshRenderer>().sharedMaterial = glow;
            var veilColor = Palette.Hex("#b46cff");
            veilColor.a = 0.1f;
            var veil = Palette.Unlit(veilColor);
            made.Add(veil);
            var disc = Palette.Shape(PrimitiveType.Cylinder, "Veil", portal, Vector3.zero, new Vector3(Radius * 2f, 0.05f, Radius * 2f), veil);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return portal;
        }

        public override bool Tick(float dt)
        {
            sinceJump += dt;
            Vector3 now = Ship.position;
            if (second && sinceJump >= ReuseSeconds)
            {
                if (Through(first, lastSpot, now)) Jump(second);
                else if (Through(second, lastSpot, now)) Jump(first);
            }
            lastSpot = Ship.position;
            return base.Tick(dt);
        }

        static bool Through(Transform portal, Vector3 from, Vector3 to)
        {
            float before = Vector3.Dot(from - portal.position, portal.forward);
            float after = Vector3.Dot(to - portal.position, portal.forward);
            if (Mathf.Sign(before) == Mathf.Sign(after) || before == after) return false;
            return Vector3.Distance(Vector3.Lerp(from, to, before / (before - after)), portal.position) < Radius;
        }

        void Jump(Transform exit)
        {
            // Far enough out that the camera, behind the ship, isn't still behind the portal.
            slot.craft.Warp(exit.position + exit.forward * 45f, exit.rotation);
            var follow = UnityEngine.Object.FindFirstObjectByType<FollowCamera>();
            if (follow) follow.Snap();
            sinceJump = 0f;
        }

        public override void End()
        {
            foreach (var thing in made) if (thing) UnityEngine.Object.Destroy(thing);
        }
    }

    /// <summary>Sniper: one huge laser shot straight ahead that destroys the first ship it hits.</summary>
    class SniperPower : Power
    {
        const float Range = 5000f, Show = 0.7f;
        GameObject beam;
        Material look;
        public override float Fraction => left / Show;

        public override void Begin()
        {
            left = Show;
            var body = slot.craft.GetComponent<Rigidbody>();
            Vector3 from = ShipCentre + Ship.forward * 6f;
            float distance = Range;
            if (Projectile.Cast(from, Ship.forward, Range, body, out var hit))
            {
                distance = hit.distance;
                var target = hit.collider.GetComponentInParent<Damageable>();
                if (target && !target.IsDead && !target.SameTeamAs(slot.craft.gameObject))
                    target.TakeDamage(1000000f, slot.craft.gameObject);
            }

            beam = new GameObject("Beam Cannon");
            beam.transform.SetPositionAndRotation(from + Ship.forward * distance, Quaternion.LookRotation(Ship.forward));
            beam.transform.localScale = new Vector3(1f, 1f, distance);
            beam.AddComponent<MeshFilter>().sharedMesh = ShipWeapon.BoltMesh();
            look = new Material(Shader.Find("SpaceCowboy/Laser"));
            look.SetColor("_LaserColor", Palette.Hex("#ff3b2f"));
            look.SetFloat("_Width", 10f);
            look.SetFloat("_Intensity", 3f);
            var renderer = beam.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = look;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 20; i++)
                ShipFx.Emit(from + Ship.forward * Mathf.Min(distance, 80f) * UnityEngine.Random.value, UnityEngine.Random.insideUnitSphere * 25f, 0.4f, 0.4f, ShipFx.Spark());
        }

        public override bool Tick(float dt)
        {
            left -= dt;
            if (look) look.SetFloat("_Intensity", 3f * Mathf.Clamp01(left / Show));
            return left > 0f;
        }

        public override void End()
        {
            if (beam) UnityEngine.Object.Destroy(beam);
            if (look) UnityEngine.Object.Destroy(look);
        }
    }

    /// <summary>
    /// Tank: for 30 seconds, ramming another ship destroys it. Firing it also adds a shield as big as your health,
    /// which lasts until it's shot away (even after the ramming ends).
    /// </summary>
    class TankPower : Power
    {

        public override void Begin()
        {
            slot.health.AddShield(slot.health.maxHealth);
            slot.craft.Crashed += Ram;
        }

        void Ram(Collision collision)
        {
            var other = collision.collider.GetComponentInParent<Damageable>();
            if (!other || other == slot.health || other.IsDead || other.SameTeamAs(slot.craft.gameObject)) return;
            other.TakeDamage(1000000f, slot.craft.gameObject);
            ShipFx.Emit(collision.GetContact(0).point, Vector3.zero, 6f, 0.15f, Color.white);
        }

        public override void End() => slot.craft.Crashed -= Ram;
    }

    /// <summary>Racer: a 30-second tank of nitro. Hold the special button to boost; it's over when the tank is empty.</summary>
    class RacerPower : Power
    {

        public override bool Tick(float dt)
        {
            bool burning = slot.input.SpecialHeld && slot.craft.controlsEnabled;
            slot.craft.nitro = burning;
            if (burning)
            {
                left -= dt;
                if (UnityEngine.Random.value < 0.6f)
                    ShipFx.Emit(Ship.position - Ship.forward * 6f, -Ship.forward * 30f, 0.5f, 0.2f, ShipFx.Spark());
            }
            return left > 0f;
        }

        public override void End() => slot.craft.nitro = false;
    }
}
