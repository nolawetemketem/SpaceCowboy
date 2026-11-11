using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// Ed's ship. With Space Flight on, it's an agile, omnidirectional spaceship: no ground to hover over,
    /// no gravity. The stick (or W/S and A/D) points the nose anywhere, the triggers (or Space and C) drive
    /// it forward and back along wherever the nose points. See Fly().
    ///
    /// With Space Flight off, it's the hovercraft, a blend of the two versions:
    ///   - The hover is the Unity version's: one ray straight down, a soft spring that floats the craft
    ///     high above the ground, and a slow sine bob. That's the floaty feel.
    ///   - The driving is the web version's: throttle up to a top speed, brake then reverse, steering that
    ///     starts and stops crisply, a little grip so it drifts, and a timed boost.
    ///   - Rise and fall (instead of the web's jump): hold Space to float higher. Hold C and the jets cut out:
    ///     the craft simply drops under gravity until you let go (the jets catch it there) or it lands.
    ///     The same soft spring holds it at whatever height it's at, so it moves in all three directions.
    ///   - It never sinks into the ground: its collider is fitted to the whole model, collisions are
    ///     continuous (a fast drop can't skip through the ground), and the lean into turns eases off near it.
    ///   - Staying upright is gentler on crashes: hits still knock you around, but most of the tumbling
    ///     spin is taken away and a damped spring brings you back level without a long wobble.
    /// Everything runs in FixedUpdate, so it feels the same at any frame rate.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class HoverCraft : MonoBehaviour
    {
        [Header("Space flight")]
        [Tooltip("On: an omnidirectional spaceship with no gravity. Off: the hovercraft. Top Speed, Forward Thrust, " +
                 "Brake, Reverse and Boost below apply to both.")]
        public bool spaceFlight = true;
        [Tooltip("How fast the nose swings up and down (degrees/s).")]
        public float flightPitchRate = 130f;
        [Tooltip("How fast the nose swings left and right (degrees/s).")]
        public float flightYawRate = 110f;
        [Tooltip("How snappily the ship reaches those rates and stops (1/s). Lower = floatier, higher = more precise.")]
        public float flightTurnResponse = 7f;
        [Tooltip("Share of sideways drift turned toward the nose each physics step. Low = slides through turns, high = on rails.")]
        [Range(0f, 1f)] public float flightGrip = 0.06f;
        [Tooltip("How quickly the ship settles its roll onto the nearest quarter turn (1/s): level, on its side, or upside down. 0 = no help, true free roll.")]
        public float flightRollLeveling = 2.5f;
        [Tooltip("How quickly it slows down on its own with no throttle (1/s). 0 = coasts forever, like real space.")]
        public float flightDrag = 0.25f;

        [Header("Quick flips (right stick / arrow keys)")]
        [Tooltip("Top speed of a flip (degrees/s). Higher = a quicker flip.")]
        public float flipSpeed = 600f;
        [Tooltip("How sharply a flip starts and stops (degrees/s²).")]
        public float flipSnap = 6000f;
        [Tooltip("Still holding the stick or key after this many seconds turns a 90° flip into a 180°.")]
        public float flipHoldTime = 0.16f;

        [Header("Hover (from the Unity version)")]
        [Tooltip("The ray reaches this far down. The craft floats where the spring holds up its weight, about 2/3 of this " +
                 "(4 units: the web game's 1.1 m hover at this scene's scale).")]
        public float hoverDistance = 6f;
        [Tooltip("Upward push (m/s²) when touching the ground; it fades to 0 at hoverDistance. Lower = softer, floatier.")]
        public float hoverForce = 30f;
        [Tooltip("Calms the bounce. 0 = keeps bobbing; higher = it settles sooner.")]
        public float hoverDamping = 0.6f;
        public float sineOscillationScale = 0.4f;
        public float sineSpeed = 1f;

        [Header("Driving (from the web version)")]
        [Tooltip("Top forward speed from throttle alone.")]
        public float topSpeed = 125f;
        [Tooltip("Forward push (m/s²). Air drag eats into it at speed, so it's set high enough to reach " +
                 "top speed in about a second and a half.")]
        public float forwardThrust = 130f;
        [Tooltip("Slowing push when braking while moving forward (m/s²).")]
        public float brake = 96f;
        [Tooltip("Reverse push once stopped (m/s²), and its top speed.")]
        public float reverseAcceleration = 38f;
        public float maxReverseSpeed = 32f;
        [Tooltip("Top turning rate (degrees/s).")]
        public float agility = 160f;
        [Tooltip("How snappily the turn rate reaches agility (1/s). Lower = floatier, higher = more precise.")]
        public float turnResponse = 8f;
        [Tooltip("Share of sideways sliding cancelled each physics step. Low = slippery, drifty.")]
        [Range(0f, 1f)] public float grip = 0.07f;
        [Tooltip("Share of the throttle you keep while in the air.")]
        [Range(0f, 1f)] public float airControl = 0.25f;

        [Header("Rise and fall")]
        [Tooltip("How fast holding Space raises the hover height, in units per second.")]
        public float climbRate = 30f;
        [Tooltip("The highest the craft can float above the ground (on top of its normal hover).")]
        public float maxAltitude = 100f;
        [Tooltip("While C is held the jets cut out and the craft drops: gravity × this, with no air drag. " +
                 "1 = the world's gravity; 3 ≈ real gravity at this scene's scale; 10 = a quick, snappy drop.")]
        public float dropGravity = 8f;
        [Tooltip("How far the nose tips up while rising (degrees). The model only; the body stays level.")]
        public float risePitch = 12f;
        [Tooltip("How far the nose dips while dropping (degrees): just a hint. 0 = stays level.")]
        public float dropPitch = 8f;

        [Header("Body")]
        [Tooltip("Fit the box collider around the whole model when the game starts, so no part (the bottom " +
                 "engines, the wingtips) can sink into the ground.")]
        public bool fitColliderToModel = true;
        [Tooltip("At normal hover, the bottom of the model stays at least this high above the ground " +
                 "(3.5 = the web game's 1.1 m). The hover is raised to fit if it's set lower.")]
        public float minGroundClearance = 3.5f;

        [Header("Boost")]
        [Tooltip("Seconds the boost keeps pushing.")]
        public float boostDuration = 1.2f;
        [Tooltip("While boosting: push = forwardThrust × this.")]
        public float boostAccelerationMultiplier = 2.5f;
        [Tooltip("While boosting: top speed = topSpeed × this.")]
        public float boostTopSpeedMultiplier = 1.5f;
        public float boostCooldown = 5f;

        [Header("Staying upright")]
        [Tooltip("How hard the craft turns back toward level (1/s²).")]
        public float righting = 6f;
        [Tooltip("0 = rocks back and forth for a while; 1 = comes back level without overshooting.")]
        [Range(0f, 1.5f)] public float rightingDamping = 0.8f;
        [Tooltip("Share of the tumbling spin from a crash that the craft keeps. 0 = none, 1 = all of it.")]
        [Range(0f, 1f)] public float crashSpin = 0.3f;

        [Header("Look")]
        [Tooltip("How far the model leans into turns (degrees). Only the model leans; the physics body stays level.")]
        public float maxBank = 25f;

        [Tooltip("Off on the start menu: the craft floats in place and ignores the controls.")]
        public bool controlsEnabled = true;

        /// <summary>Flat (yaw-only) forward direction, used for thrust and by the camera.</summary>
        public Vector3 Heading { get; private set; } = Vector3.forward;
        /// <summary>True while the hover ray touches the ground.</summary>
        public bool Grounded { get; private set; }
        /// <summary>How far above its normal hover the craft is set to float (0 = normal hover).</summary>
        public float Altitude { get; private set; }
        /// <summary>Seconds until boost works again (0 = ready).</summary>
        public float BoostCooldownLeft { get; private set; }

        Rigidbody rb;
        CraftInput input;
        Transform bankPivot;
        float sineX;
        float bank, pitch;
        float boostTimeLeft;
        readonly RaycastHit[] hits = new RaycastHit[8];

        // The flip in progress, if any.
        bool flipping;
        Vector2Int flipDirection;
        Quaternion flipStart;
        Vector3 flipAxis;
        float flipSign, flipTarget, flipAngle, flipRate, flipHeldFor;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            input = GetComponent<CraftInput>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Continuous collisions: even a fast drop can't skip through the ground between physics steps.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (fitColliderToModel) FitCollider();
            if (spaceFlight)
            {
                rb.useGravity = false;
                rb.linearDamping = flightDrag;
                rb.angularDamping = 0f; // Fly() sets the turn rates itself
            }
            // The spring settles where its push equals gravity: at hoverDistance × (1 - g / hoverForce).
            // Make sure that leaves the model's underside minGroundClearance above the ground.
            float settleShare = 1f + Physics.gravity.y / hoverForce;
            if (settleShare > 0f) hoverDistance = Mathf.Max(hoverDistance, (minGroundClearance - bottom) / settleShare);

            // Lean the model, not the body: move the model's parts under a pivot we can roll.
            bankPivot = new GameObject("BankPivot").transform;
            bankPivot.SetParent(transform, false);
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child != bankPivot) child.SetParent(bankPivot, false);
            }
        }

        /// <summary>One physics step of space flight: point the nose, then thrust along it.</summary>
        void Fly()
        {
            float dt = Time.fixedDeltaTime;
            bool live = controlsEnabled && input;
            float throttle = live ? input.Throttle : 0f;
            float steer = live ? input.Steer : 0f;
            float nose = live ? input.Pitch : 0f;
            bool boostPressed = live && input.TakeBoost();

            Vector3 forward = rb.rotation * Vector3.forward;
            Vector3 right = rb.rotation * Vector3.right;
            Vector3 up = rb.rotation * Vector3.up;
            Vector3 velocity = rb.linearVelocity;
            float forwardSpeed = Vector3.Dot(velocity, forward);

            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude > 0.01f) Heading = flat.normalized;

            // --- Point the nose: drive the spin toward a target, so turns start and stop crisply.
            //     Both turns are around the ship's own axes, so the stick means the same thing at any angle.
            //     (A positive turn around "right" tips the nose down, hence the minus.)
            //     A flip takes over the ship's rotation until it's done.
            bool flipped = UpdateFlip(dt);
            if (!flipped)
            {
                Vector3 targetSpin = right * (-nose * flightPitchRate * Mathf.Deg2Rad) + up * (steer * flightYawRate * Mathf.Deg2Rad);
                // Settle the roll onto the nearest quarter turn (level, on its side or upside down), so a
                // flip's roll stays put and loops stay clean. It fades out pointing straight up or down,
                // where "level" stops meaning anything.
                const float quarter = Mathf.PI / 2f;
                float roll = Mathf.Atan2(right.y, up.y);
                float rollError = roll - Mathf.Round(roll / quarter) * quarter;
                targetSpin += forward * (-rollError * flightRollLeveling * flat.magnitude);
                rb.AddTorque((targetSpin - rb.angularVelocity) * flightTurnResponse, ForceMode.Acceleration);
            }

            // --- Forward and back, along wherever the nose points. A light squeeze is a light push.
            float thrust = 0f;
            if (throttle > 0f && forwardSpeed < topSpeed)
                thrust = forwardThrust * throttle;
            else if (throttle < 0f)
            {
                if (forwardSpeed > 2f) thrust = brake * throttle;
                else if (forwardSpeed > -maxReverseSpeed) thrust = reverseAcceleration * throttle;
            }

            // --- Boost: a push that lasts a set time, then a cooldown.
            BoostCooldownLeft = Mathf.Max(0f, BoostCooldownLeft - dt);
            if (boostPressed && BoostCooldownLeft == 0f)
            {
                boostTimeLeft = boostDuration;
                BoostCooldownLeft = boostCooldown;
            }
            if (boostTimeLeft > 0f)
            {
                boostTimeLeft -= dt;
                if (forwardSpeed < topSpeed * boostTopSpeedMultiplier)
                    thrust += forwardThrust * boostAccelerationMultiplier;
            }
            rb.AddForce(forward * thrust, ForceMode.Acceleration);

            // --- Grip: turn part of the drift toward the nose. The speed is redirected, not lost, so
            //     turning doesn't slow you down. (Knocked mostly sideways by a crash, the slide just fades.)
            //     A flip spins the ship without changing where it's headed, so there's no grip until it ends.
            if (!flipped)
            {
                float along = Vector3.Dot(velocity, forward);
                Vector3 drift = velocity - forward * along;
                Vector3 keptDrift = drift * (1f - flightGrip);
                if (Mathf.Abs(along) > drift.magnitude)
                    along = Mathf.Sign(along) * Mathf.Sqrt(Mathf.Max(0f, velocity.sqrMagnitude - keptDrift.sqrMagnitude));
                rb.linearVelocity = forward * along + keptDrift;
            }

            // --- Lean into turns (the model only), more at speed.
            float speedFactor = Mathf.Min(1f, 0.3f + Mathf.Abs(forwardSpeed) / (topSpeed * 0.5f));
            bank += (-steer * maxBank * speedFactor - bank) * (1f - Mathf.Exp(-8f * dt));
            pitch += -pitch * (1f - Mathf.Exp(-6f * dt));
            // Near the ground, lean only as far as the wingtips have room, so they never dip into it.
            float? ground = GroundDistance(rb.position, halfSpan - bottom + 2f);
            if (ground.HasValue && halfSpan > 0f)
            {
                float room = Mathf.Asin(Mathf.Clamp01((ground.Value + bottom) / halfSpan)) * Mathf.Rad2Deg;
                bank = Mathf.Clamp(bank, -room, room);
            }
        }

        /// <summary>
        /// Start or continue a quick flip. Returns true while one is playing: it always plays out to an exact
        /// 90° or 180°, around the ship's own axes (up/down around X, left/right around Z). A flick is 90°;
        /// still holding after flipHoldTime, or pressing the same direction again during the flip, makes it 180°.
        /// </summary>
        bool UpdateFlip(float dt)
        {
            Vector2Int direction = default;
            bool pressed = controlsEnabled && input && input.TakeFlip(out direction);
            if (pressed && !flipping)
            {
                flipping = true;
                flipDirection = direction;
                flipStart = rb.rotation;
                bool nose = direction.y != 0;
                flipAxis = nose ? Vector3.right : Vector3.forward;
                // Up tips the nose up and right rolls to the right: both are negative turns around their axes.
                flipSign = nose ? -direction.y : -direction.x;
                flipTarget = 90f;
                flipAngle = flipRate = flipHeldFor = 0f;
            }
            else if (pressed && direction == flipDirection) flipTarget = 180f;
            if (!flipping) return false;

            flipHeldFor += dt;
            if (flipHeldFor >= flipHoldTime && input && input.IsFlipHeld(flipDirection)) flipTarget = 180f;

            // Speed up to flipSpeed, and slow in time to land exactly on the target. Extending to 180°
            // partway through just moves the target, so there's no hitch.
            float remaining = flipTarget - flipAngle;
            float brakeSpeed = Mathf.Max(Mathf.Sqrt(2f * flipSnap * remaining), 60f);
            flipRate = Mathf.Min(flipRate + flipSnap * dt, flipSpeed, brakeSpeed);
            flipAngle = Mathf.Min(flipTarget, flipAngle + flipRate * dt);

            rb.angularVelocity = Vector3.zero;
            rb.MoveRotation(flipStart * Quaternion.AngleAxis(flipSign * flipAngle, flipAxis));
            if (flipAngle >= flipTarget) flipping = false;
            return true;
        }

        void FixedUpdate()
        {
            if (spaceFlight)
            {
                Fly();
                return;
            }
            float dt = Time.fixedDeltaTime;
            float throttle = controlsEnabled && input ? input.Throttle : 0f;
            float steer = controlsEnabled && input ? input.Steer : 0f;
            float lift = controlsEnabled && input ? input.Lift : 0f;
            bool boostPressed = controlsEnabled && input && input.TakeBoost();

            Vector3 up = rb.rotation * Vector3.up;
            Vector3 forward = rb.rotation * Vector3.forward;
            Vector3 velocity = rb.linearVelocity;
            Vector3 spin = rb.angularVelocity;
            // Pushes, as accelerations (m/s²), so they don't depend on the craft's mass.
            Vector3 push = Vector3.zero;
            Vector3 turn = Vector3.zero;

            // --- Rise: Space raises the height the spring holds the craft at.
            bool falling = lift < 0f;
            if (!falling) Altitude = Mathf.Clamp(Altitude + lift * climbRate * dt, 0f, maxAltitude);

            // --- Hover: a soft spring along one ray straight down, with a slow sine bob in its reach.
            //     Raised by Altitude, it works the same at any height: the spring just sits higher up.
            sineX = (sineX + dt * sineSpeed) % (2f * Mathf.PI);
            float reach = hoverDistance + Mathf.Sin(sineX) * sineOscillationScale;
            float? ground = GroundDistance(rb.position, reach + (falling ? maxAltitude + reach : Altitude));
            Grounded = ground.HasValue && ground.Value <= reach + Altitude;
            if (falling)
            {
                // --- Drop: the jets cut out. No spring, no push down: only gravity, strong enough to drop quickly.
                // Any rise stops at once, and air drag is undone on the way down, so the drop is crisp.
                if (velocity.y > 0f) rb.linearVelocity = new Vector3(velocity.x, 0f, velocity.z);
                push.y += Physics.gravity.y * (dropGravity - 1f) + rb.linearDamping * Mathf.Min(velocity.y, 0f);
                // Remember how high it is now, so when C is let go the jets catch it right here.
                float settle = reach * (1f + Physics.gravity.y / hoverForce); // where the spring alone would hold it
                Altitude = ground.HasValue ? Mathf.Clamp(ground.Value - settle, 0f, maxAltitude) : maxAltitude;
            }
            else if (Grounded)
            {
                float spring = (1f - (ground.Value - Altitude) / reach) * hoverForce - hoverDamping * velocity.y;
                if (spring > 0f) push.y += spring; // springs push, never pull down
            }

            // --- Upright: turn back toward level, and calm any roll/pitch wobble.
            Vector3 axis = Vector3.Cross(up, Vector3.up);
            if (axis.sqrMagnitude < 1e-6f && up.y < 0f) axis = forward; // exactly upside down: pick a way to roll
            float tilt = Vector3.Angle(up, Vector3.up) * Mathf.Deg2Rad;
            Vector3 wobble = spin - Vector3.up * spin.y;
            turn += axis.normalized * (tilt * righting) - wobble * (2f * Mathf.Sqrt(righting) * rightingDamping);

            // --- Heading: forward flattened onto the ground (kept if the craft points straight up or down).
            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude > 0.01f) Heading = flat.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, Heading);
            float forwardSpeed = Vector3.Dot(velocity, Heading);

            // --- Throttle / brake / reverse. Full push on the ground, some of it in the air.
            float thrust = 0f;
            if (throttle > 0f && forwardSpeed < topSpeed)
                thrust = forwardThrust * throttle;
            else if (throttle < 0f)
            {
                if (forwardSpeed > 2f) thrust = brake * throttle;
                else if (forwardSpeed > -maxReverseSpeed) thrust = reverseAcceleration * throttle;
            }
            push += Heading * (thrust * (Grounded ? 1f : airControl));

            // --- Steering: drive the turn rate toward a target, so turns start and stop crisply.
            float targetYawRate = steer * agility * Mathf.Deg2Rad;
            turn.y += (targetYawRate - spin.y) * turnResponse;

            // --- Grip: each step, turn part of the sideways slide back toward where the nose points.
            //     While carving a turn the speed isn't lost, just redirected, so turning doesn't slow you down.
            //     (Knocked mostly sideways, e.g. by a crash, the slide is simply damped instead.)
            if (Grounded)
            {
                Vector3 travel = new Vector3(velocity.x, 0f, velocity.z);
                float sideways = Vector3.Dot(travel, right);
                float along = Vector3.Dot(travel, Heading);
                float keptSideways = sideways * (1f - grip);
                if (Mathf.Abs(along) > Mathf.Abs(sideways))
                {
                    float newAlong = Mathf.Sign(along) * Mathf.Sqrt(Mathf.Max(0f, travel.sqrMagnitude - keptSideways * keptSideways));
                    rb.linearVelocity = Heading * newAlong + right * keptSideways + Vector3.up * rb.linearVelocity.y;
                }
                else rb.AddForce(-right * (sideways * grip), ForceMode.VelocityChange);
            }

            BoostCooldownLeft = Mathf.Max(0f, BoostCooldownLeft - dt);

            // --- Boost (anywhere): a push that lasts a set time, then a cooldown.
            if (boostPressed && BoostCooldownLeft == 0f)
            {
                boostTimeLeft = boostDuration;
                BoostCooldownLeft = boostCooldown;
            }
            if (boostTimeLeft > 0f)
            {
                boostTimeLeft -= dt;
                if (forwardSpeed < topSpeed * boostTopSpeedMultiplier)
                    push += Heading * (forwardThrust * boostAccelerationMultiplier);
            }

            rb.AddForce(push, ForceMode.Acceleration);
            rb.AddTorque(turn, ForceMode.Acceleration);

            // --- Lean into turns (the model only), more at speed.
            float speedFactor = Mathf.Min(1f, 0.3f + Mathf.Abs(forwardSpeed) / (topSpeed * 0.5f));
            float targetBank = -steer * maxBank * speedFactor;
            bank += (targetBank - bank) * (1f - Mathf.Exp(-8f * dt));
            // --- Nose up while rising, a little down while dropping (the model only).
            float targetPitch = lift > 0f && Altitude < maxAltitude ? risePitch : falling ? -dropPitch : 0f;
            pitch += (targetPitch - pitch) * (1f - Mathf.Exp(-6f * dt));
            // Near the ground, lean only as far as the wingtips have room, so they never dip into it.
            if (ground.HasValue && halfSpan > 0f)
            {
                float clearance = ground.Value + bottom;
                float room = Mathf.Asin(Mathf.Clamp01(clearance / halfSpan)) * Mathf.Rad2Deg;
                bank = Mathf.Clamp(bank, -room, room);
            }
        }

        // The collider's half-width and how far its bottom sits below the craft's origin (negative = below).
        float halfSpan, bottom;

        /// <summary>Fit the box collider around every part of the model (in the craft's own space).</summary>
        void FitCollider()
        {
            var box = GetComponent<BoxCollider>();
            if (!box) return;
            bool any = false;
            var bounds = new Bounds();
            foreach (var filter in GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.sharedMesh) continue;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, (i / 2) % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
                    var local = transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                    else bounds.Encapsulate(local);
                }
            }
            if (!any) return;
            // A little extra underneath, for the bottom engines when they open on the brake.
            bounds.min -= new Vector3(0f, bounds.size.y * 0.08f, 0f);
            box.center = bounds.center;
            box.size = bounds.size;
            halfSpan = bounds.extents.x;
            bottom = bounds.min.y;
        }

        void LateUpdate()
        {
            bankPivot.localRotation = Quaternion.Euler(-pitch, 0f, bank);
        }

        void OnCollisionEnter(Collision collision)
        {
            // Keep the knock (the bounce and the yaw), but take away most of the tumble.
            Vector3 spin = rb.angularVelocity;
            Vector3 yaw = Vector3.up * spin.y;
            rb.angularVelocity = yaw + (spin - yaw) * crashSpin;
        }

        /// <summary>Back to a spot, level and still, at normal hover height, with boost ready (Training reset).</summary>
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            rb.position = position;
            rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Heading = new Vector3((rotation * Vector3.forward).x, 0f, (rotation * Vector3.forward).z).normalized;
            BoostCooldownLeft = 0f;
            Altitude = 0f;
            boostTimeLeft = 0f;
            flipping = false;
            bank = pitch = 0f;
            if (input) input.Clear();
        }

        /// <summary>
        /// Distance down to the first solid, unmoving thing (ground, rocks, goal posts), or null if
        /// nothing is within reach. Moving things are skipped, so the craft never floats on another ship.
        /// </summary>
        float? GroundDistance(Vector3 origin, float reach)
        {
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, reach, ~0, QueryTriggerInteraction.Ignore);
            float? nearest = null;
            for (int i = 0; i < count; i++)
            {
                var body = hits[i].rigidbody;
                if (body != null && !body.isKinematic) continue;
                if (nearest == null || hits[i].distance < nearest) nearest = hits[i].distance;
            }
            return nearest;
        }
    }
}
