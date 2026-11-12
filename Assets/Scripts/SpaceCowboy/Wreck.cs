using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// What's left of a destroyed ship. It bursts into pieces with a flash, the pieces fall and bounce
    /// on the ground, rest for a moment, then each one turns to a hologram and disintegrates.
    /// The pieces are moved by this script instead of the physics engine, so they can't block bullets or other ships.
    /// </summary>
    public class Wreck : MonoBehaviour
    {
        const float HotSeconds = 0.9f;     // how long pieces take to cool from white-hot
        const float OutOfTimeSeconds = 7f; // a piece that never finds the ground dissolves anyway
        const float Bounciness = 0.38f;
        const int MostBounces = 2;

        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color Hot = new Color(1f, 0.85f, 0.55f);

        enum Stage { Falling, Resting, Dissolving }

        class Piece
        {
            public Transform transform;
            public Renderer renderer;
            public Stage stage;
            public Vector3 velocity, spinAxis;
            public float spin, wait, dissolved, moteTimer;
            public int bounces;
        }

        class Flash
        {
            public Transform transform;
            public Material material;
            public float delay, age, seconds, size;
        }

        readonly List<Piece> pieces = new();
        readonly List<Flash> flashes = new();
        readonly List<(Material material, Color cooled)> burning = new();
        readonly Dictionary<Material, Material> copies = new();
        MaterialPropertyBlock block;

        ShipDestruction ship;
        Action finished;
        Material hologram;
        float radius, age, waitAtEnd;

        /// <summary>
        /// Load what an explosion needs (its shaders and the flash's sphere) while the game loads,
        /// instead of the moment something first blows up.
        /// </summary>
        public static void Prewarm()
        {
            var flash = Palette.Shape(PrimitiveType.Sphere, "Prewarm", null, Vector3.zero, Vector3.zero, Palette.Unlit(Color.clear));
            Destroy(flash.GetComponent<Renderer>().sharedMaterial);
            Destroy(flash);
            Destroy(Palette.Hologram(Color.clear));
        }

        /// <summary>Blow these parts apart. finished is called once the last piece has disintegrated.</summary>
        public static Wreck Create(ShipDestruction ship, List<Renderer> parts, Vector3 centre, float radius, Vector3 shipVelocity, Action finished)
        {
            var wreck = new GameObject($"Wreck of {ship.name}").AddComponent<Wreck>();
            wreck.block = new MaterialPropertyBlock();
            wreck.ship = ship;
            wreck.finished = finished;
            wreck.radius = radius;
            wreck.waitAtEnd = ship.waitAfterWreck;
            wreck.hologram = Palette.Hologram(ship.hologramColor);
            wreck.Burst(parts, centre, shipVelocity);
            return wreck;
        }

        void Burst(List<Renderer> parts, Vector3 centre, Vector3 shipVelocity)
        {
            foreach (var part in parts)
            {
                if (!part.gameObject.activeInHierarchy) continue;
                var mesh = ShipDestruction.MeshOf(part);
                if (!mesh) continue;
                var chunks = ShipShatter.Split(mesh, ship.PiecesFor(part));
                var materials = part.sharedMaterials;
                if (chunks == null)
                {
                    // The model isn't readable, so this whole part flies off in one piece.
                    AddPiece(mesh, part, Vector3.zero, materials, centre, shipVelocity);
                    continue;
                }
                foreach (var chunk in chunks)
                    AddPiece(chunk.mesh, part, chunk.centre, new[] { materials[Mathf.Min(chunk.submesh, materials.Length - 1)] }, centre, shipVelocity);
            }

            // One big flash, then a few small ones around it.
            AddFlash(centre, radius * 3.2f, 0.28f, 0f);
            for (int i = 0; i < 4; i++)
                AddFlash(centre + UnityEngine.Random.insideUnitSphere * radius, radius * UnityEngine.Random.Range(0.9f, 1.6f), 0.2f, UnityEngine.Random.Range(0.05f, 0.3f));

            for (int i = 0; i < 110; i++)
            {
                Vector3 velocity = UnityEngine.Random.onUnitSphere * (radius * UnityEngine.Random.Range(3f, 13f)) + shipVelocity * 0.5f;
                ShipFx.Emit(centre, velocity, radius * UnityEngine.Random.Range(0.04f, 0.1f), UnityEngine.Random.Range(0.35f, 0.95f), ShipFx.Spark());
            }
        }

        void AddPiece(Mesh mesh, Renderer part, Vector3 meshCentre, Material[] materials, Vector3 centre, Vector3 shipVelocity)
        {
            var go = new GameObject("Piece");
            var t = go.transform;
            t.SetParent(transform, false);
            t.SetPositionAndRotation(part.transform.TransformPoint(meshCentre), part.transform.rotation);
            t.localScale = part.transform.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var look = go.AddComponent<MeshRenderer>();
            for (int i = 0; i < materials.Length; i++) materials[i] = HotCopy(materials[i]);
            look.sharedMaterials = materials;

            Vector3 outward = t.position - centre;
            outward = outward.sqrMagnitude > 0.01f ? outward.normalized : UnityEngine.Random.onUnitSphere;
            outward = (outward + UnityEngine.Random.insideUnitSphere * 0.6f + Vector3.up * 0.45f).normalized;
            pieces.Add(new Piece
            {
                transform = t,
                renderer = look,
                velocity = outward * (radius * UnityEngine.Random.Range(1.8f, 6.5f) * ship.burstStrength) + shipVelocity * 0.6f,
                spinAxis = UnityEngine.Random.onUnitSphere,
                spin = UnityEngine.Random.Range(180f, 820f),
            });
        }

        /// <summary>This wreck's own copy of a material, so the pieces can glow while the living ship's paint is left alone.</summary>
        Material HotCopy(Material original)
        {
            if (copies.TryGetValue(original, out var copy)) return copy;
            copy = new Material(original);
            copies[original] = copy;
            if (copy.HasProperty(BaseColorId))
            {
                Color scorched = Color.Lerp(copy.GetColor(BaseColorId), Color.black, 0.28f);
                burning.Add((copy, scorched));
                copy.SetColor(BaseColorId, Hot);
            }
            return copy;
        }

        void AddFlash(Vector3 position, float size, float seconds, float delay)
        {
            var material = Palette.Unlit(new Color(1f, 1f, 1f, 0f));
            var go = Palette.Shape(PrimitiveType.Sphere, "Flash", transform, position, Vector3.zero, material);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flashes.Add(new Flash { transform = go.transform, material = material, size = size, seconds = seconds, delay = delay });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            CoolDown();
            UpdateFlashes(dt);

            bool anyLeft = false;
            foreach (var piece in pieces)
            {
                if (!piece.transform) continue;
                Step(piece, dt);
                anyLeft = true;
            }
            if (anyLeft) return;

            waitAtEnd -= dt;
            if (waitAtEnd > 0f) return;
            finished?.Invoke();
            Destroy(gameObject);
        }

        void CoolDown()
        {
            if (age > HotSeconds + 0.1f || burning.Count == 0) return;
            float f = Mathf.Clamp01(age / HotSeconds);
            foreach (var (material, cooled) in burning) material.SetColor(BaseColorId, Color.Lerp(Hot, cooled, f * f));
        }

        void UpdateFlashes(float dt)
        {
            foreach (var flash in flashes)
            {
                if (flash.delay > 0f) { flash.delay -= dt; continue; }
                if (flash.age >= flash.seconds) continue;
                flash.age += dt;
                float f = Mathf.Clamp01(flash.age / flash.seconds);
                flash.transform.localScale = Vector3.one * (flash.size * (1f - (1f - f) * (1f - f)));
                Color color = Color.Lerp(Color.white, Palette.Amber, f);
                color.a = f >= 1f ? 0f : (1f - f) * (1f - f);
                flash.material.color = color;
            }
        }

        void Step(Piece piece, float dt)
        {
            switch (piece.stage)
            {
                case Stage.Falling: Fall(piece, dt); break;
                case Stage.Resting:
                    piece.wait -= dt;
                    if (piece.wait <= 0f) StartDissolving(piece);
                    break;
                case Stage.Dissolving: Dissolve(piece, dt); break;
            }
        }

        void Fall(Piece piece, float dt)
        {
            piece.velocity += Vector3.down * (ship.fallGravity * dt);
            piece.transform.Rotate(piece.spinAxis, piece.spin * dt, Space.World);

            Vector3 step = piece.velocity * dt;
            float length = step.magnitude;
            if (length > 1e-4f && HitGround(piece.transform.position, step / length, length, out var hit))
            {
                piece.transform.position = hit.point + hit.normal * 0.15f;
                piece.bounces++;
                bool sliding = Vector3.Dot(piece.velocity, hit.normal) > -ship.fallGravity * 0.2f;
                if (piece.bounces > MostBounces || sliding)
                {
                    Land(piece);
                    return;
                }
                piece.velocity = Vector3.Reflect(piece.velocity, hit.normal) * Bounciness;
                piece.spin *= 0.55f;
                return;
            }
            piece.transform.position += step;

            // Hot pieces trail sparks on the way down.
            if (age < HotSeconds)
            {
                piece.moteTimer -= dt;
                if (piece.moteTimer <= 0f)
                {
                    piece.moteTimer = 0.05f;
                    ShipFx.Emit(piece.transform.position, UnityEngine.Random.insideUnitSphere * radius, radius * 0.05f, 0.4f, ShipFx.Spark());
                }
            }
            if (age > OutOfTimeSeconds) StartDissolving(piece);
        }

        void Land(Piece piece)
        {
            piece.stage = Stage.Resting;
            piece.wait = ship.restSeconds * UnityEngine.Random.Range(0.75f, 1.35f);
        }

        /// <summary>The ground, a rock or a goal post along this step. Ships don't count.</summary>
        static bool HitGround(Vector3 from, Vector3 direction, float distance, out RaycastHit hit)
        {
            return Physics.Raycast(from, direction, out hit, distance, ~0, QueryTriggerInteraction.Ignore) && !hit.rigidbody;
        }

        void StartDissolving(Piece piece)
        {
            piece.stage = Stage.Dissolving;
            var holograms = new Material[piece.renderer.sharedMaterials.Length];
            for (int i = 0; i < holograms.Length; i++) holograms[i] = hologram;
            piece.renderer.sharedMaterials = holograms;
            piece.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Dissolve(Piece piece, float dt)
        {
            piece.dissolved = Mathf.Min(1f, piece.dissolved + dt / ship.dissolveSeconds);
            // A short moment as a whole hologram, then it comes apart, lifting off the ground as it goes.
            float reveal = 1f - Mathf.InverseLerp(0.2f, 1f, piece.dissolved);
            block.SetFloat(RevealId, reveal);
            piece.renderer.SetPropertyBlock(block);
            piece.transform.position += Vector3.up * (radius * 0.35f * dt);

            piece.moteTimer -= dt;
            if (piece.moteTimer <= 0f)
            {
                piece.moteTimer = 0.045f;
                Bounds area = piece.renderer.bounds;
                var spot = new Vector3(
                    UnityEngine.Random.Range(area.min.x, area.max.x),
                    UnityEngine.Random.Range(area.min.y, area.max.y),
                    UnityEngine.Random.Range(area.min.z, area.max.z));
                ShipFx.Emit(spot, Vector3.up * (radius * UnityEngine.Random.Range(2f, 6f)),
                    radius * UnityEngine.Random.Range(0.012f, 0.03f), UnityEngine.Random.Range(0.5f, 1.1f), ship.hologramColor);
            }

            if (piece.dissolved >= 1f) Destroy(piece.transform.gameObject);
        }

        void OnDestroy()
        {
            foreach (var copy in copies.Values) Destroy(copy);
            foreach (var flash in flashes) Destroy(flash.material);
            Destroy(hologram);
        }
    }
}
