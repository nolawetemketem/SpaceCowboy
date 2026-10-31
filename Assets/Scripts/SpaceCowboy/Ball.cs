using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>The ball: a bouncy sphere, drawn as a yellow toon sphere with Ed's grin on it.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class Ball : MonoBehaviour
    {
        // Sizes are the web game's × 3.2, the scale of this scene (the Fighter is about 9 units across).
        public float radius = 3.5f;
        [Tooltip("The craft weighs 1, so it can shove the ball, but the ball has some heft.")]
        public float mass = 0.2f;
        [Tooltip("0 = thud, 1 = rubber ball.")]
        [Range(0f, 1f)] public float bounciness = 0.6f;
        public float friction = 0.5f;
        [Tooltip("How quickly it slows down rolling and spinning on its own.")]
        public float linearDamping = 0.25f;
        public float angularDamping = 0.4f;
        [Tooltip("Extra pull down, so the ball drops and bounces with some weight instead of drifting like on the moon. " +
                 "1 = the world's gravity; the web game's ball fell about 3× as hard for its size.")]
        public float gravityMultiplier = 3f;

        public Rigidbody Body { get; private set; }

        /// <summary>Make the ball, with its collider, physics and emoticon texture.</summary>
        public static Ball Create(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Ball";
            go.transform.SetParent(parent, false);
            go.AddComponent<Rigidbody>();
            return go.AddComponent<Ball>();
        }

        void Awake()
        {
            transform.localScale = Vector3.one * radius * 2f; // the built-in sphere is 1 across
            Body = GetComponent<Rigidbody>();
            Body.mass = mass;
            Body.linearDamping = linearDamping;
            Body.angularDamping = angularDamping;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            // Continuous collision detection: a fast ball can't skip through a post between steps.
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            GetComponent<SphereCollider>().sharedMaterial = new PhysicsMaterial("Ball")
            {
                bounciness = bounciness,
                dynamicFriction = friction,
                staticFriction = friction,
                bounceCombine = PhysicsMaterialCombine.Average,
            };
            GetComponent<Renderer>().sharedMaterial = Palette.Toon(Color.white, Emoticon.BallTexture());
        }

        void FixedUpdate()
        {
            Body.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }

        /// <summary>Put the ball at a spot, not moving (at the start, after a goal, and on B).</summary>
        public void Respawn(Vector3 position)
        {
            Body.position = position;
            Body.rotation = Quaternion.identity;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }
    }
}
