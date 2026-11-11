using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// Destroying and bringing back a ship, for any ship model:
    ///   1. Health runs out: the ship vanishes and a Wreck takes its place (pieces burst, fall, disintegrate).
    ///   2. WreckGone: whoever owns the ship moves it to its respawn spot and calls Materialize.
    ///   3. The ship is projected as a hologram from the bottom up, stutters, and turns solid.
    ///   4. Materialized: the ship has full health and can be hit again.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class ShipDestruction : MonoBehaviour
    {
        [Header("Breaking apart")]
        [Tooltip("About how many pieces the ship breaks into.")]
        public int pieces = 28;
        [Tooltip("How hard the pieces fly apart. 1 = normal.")]
        public float burstStrength = 1f;
        [Tooltip("How fast the pieces fall.")]
        public float fallGravity = 60f;

        [Header("Disintegrating")]
        [Tooltip("About how long a piece lies on the ground before it disintegrates.")]
        public float restSeconds = 1.3f;
        [Tooltip("How long a piece takes to disintegrate.")]
        public float dissolveSeconds = 1.1f;
        [Tooltip("A pause after the last piece is gone, before the ship comes back.")]
        public float waitAfterWreck = 0.4f;

        [Header("Coming back")]
        [Tooltip("How long the holographic projection takes.")]
        public float materializeSeconds = 1.6f;

        public Color hologramColor = Palette.LEDGreen;

        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int SweepAmountId = Shader.PropertyToID("_SweepAmount");
        static readonly int SweepBottomId = Shader.PropertyToID("_SweepBottom");
        static readonly int SweepHeightId = Shader.PropertyToID("_SweepHeight");

        /// <summary>The last piece of the wreck has disintegrated: time to move the ship and call Materialize.</summary>
        public event Action WreckGone;
        /// <summary>The ship is solid again, at full health.</summary>
        public event Action Materialized;

        /// <summary>True from the explosion until the ship is solid again.</summary>
        public bool Wrecked { get; private set; }

        readonly List<Renderer> parts = new();
        readonly Dictionary<Renderer, int> pieceCounts = new();
        Damageable health;
        Rigidbody body;
        Collider[] colliders;
        RigidbodyConstraints keptConstraints;
        Bounds modelBounds; // the whole model, relative to the ship
        float totalPartSize;

        Material projection;
        Material[][] solidMaterials;
        Material[][] projectionMaterials;
        Transform scanPlate;
        Material scanPlateMaterial;
        bool projecting, showingSolid = true;
        float projected, moteTimer;

        /// <summary>Call once, after the ship's model and colliders are in place. The model is the object holding the ship's visible parts.</summary>
        public void Setup(Transform model)
        {
            health = GetComponent<Damageable>();
            body = GetComponent<Rigidbody>();
            colliders = GetComponents<Collider>();
            health.Died += _ => Explode();

            bool any = false;
            foreach (var part in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!MeshOf(part)) continue;
                parts.Add(part);

                // The part's box, measured in the ship's own space.
                Bounds local = part.localBounds;
                Matrix4x4 toShip = transform.worldToLocalMatrix * part.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var offset = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                    Vector3 point = toShip.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, offset));
                    if (any) modelBounds.Encapsulate(point);
                    else { modelBounds = new Bounds(point, Vector3.zero); any = true; }
                }
                totalPartSize += part.bounds.size.magnitude;
            }

            // Cut the meshes now, so the first explosion doesn't hitch.
            foreach (var part in parts)
            {
                var mesh = MeshOf(part);
                if (!mesh.isReadable)
                    Debug.LogWarning($"{mesh.name}: turn on Read/Write in the model's import settings so it can break into small pieces.", this);
                pieceCounts[part] = Mathf.Max(1, Mathf.RoundToInt(pieces * part.bounds.size.magnitude / Mathf.Max(totalPartSize, 0.001f)));
                ShipShatter.Split(mesh, pieceCounts[part]);
            }
        }

        /// <summary>The mesh a renderer draws, if it draws one.</summary>
        public static Mesh MeshOf(Renderer part)
        {
            if (part is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
            if (part is MeshRenderer && part.TryGetComponent<MeshFilter>(out var filter)) return filter.sharedMesh;
            return null;
        }

        /// <summary>Bigger parts break into more pieces.</summary>
        public int PiecesFor(Renderer part) => pieceCounts[part];

        [ContextMenu("Destroy this ship (test)")]
        void DestroyForTest()
        {
            if (Application.isPlaying && !Wrecked) health.TakeDamage(health.Health, gameObject);
        }

        void Explode()
        {
            if (Wrecked) return;
            Wrecked = true;
            Vector3 velocity = body && !body.isKinematic ? body.linearVelocity : Vector3.zero;
            Vector3 centre = transform.TransformPoint(modelBounds.center);
            Wreck.Create(this, parts, centre, modelBounds.extents.magnitude, velocity, () => WreckGone?.Invoke());

            SetVisible(false);
            SetCollidersOn(false);
            Hold(true);
        }

        /// <summary>Project the ship back into the world wherever it is now. It can't be hit until it's solid.</summary>
        public void Materialize()
        {
            if (!Wrecked || projecting) return;
            if (!projection) BuildProjection();

            projection.SetFloat(SweepAmountId, 1f);
            projection.SetFloat(RevealId, 0f);
            solidMaterials = new Material[parts.Count][];
            projectionMaterials = new Material[parts.Count][];
            for (int i = 0; i < parts.Count; i++)
            {
                solidMaterials[i] = parts[i].sharedMaterials;
                projectionMaterials[i] = new Material[solidMaterials[i].Length];
                Array.Fill(projectionMaterials[i], projection);
            }
            ShowSolid(false);
            SetVisible(true);

            // scanPlate.gameObject.SetActive(true);
            projected = 0f;
            projecting = true;
        }

        void BuildProjection()
        {
            projection = Palette.Hologram(hologramColor);
            Color glow = hologramColor;
            glow.a = 0.2f;
            // scanPlateMaterial = Palette.Unlit(glow);
            // var plate = Palette.Shape(PrimitiveType.Cube, "Scan Plate", transform, Vector3.zero,
            //     new Vector3(modelBounds.size.x * 1.1f, 0.08f, modelBounds.size.z * 1.1f), scanPlateMaterial);
            // plate.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // scanPlate = plate.transform;
            // scanPlate.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!projecting) return;
            projected += Time.deltaTime;
            float f = projected / materializeSeconds;

            // The first part of the time builds the ship from the bottom up.
            float build = Mathf.Clamp01(f / 0.68f);
            Vector3 lowest = transform.TransformPoint(modelBounds.center) - Vector3.up * modelBounds.extents.y;
            projection.SetFloat(SweepBottomId, lowest.y);
            projection.SetFloat(SweepHeightId, modelBounds.size.y);
            projection.SetFloat(RevealId, build);

            // scanPlate.gameObject.SetActive(build < 1f);
            // scanPlate.position = lowest + Vector3.up * (modelBounds.size.y * build);
            EmitBuildMotes(build);

            // At the end it stutters between hologram and solid, like a signal locking on.
            // if (f > 0.84f) ShowSolid(Mathf.FloorToInt(projected * 24f) % 2 == 0);
            if (f >= 1f) FinishMaterializing();
        }

        void EmitBuildMotes(float build)
        {
            if (build >= 1f) return;
            moteTimer -= Time.deltaTime;
            if (moteTimer > 0f) return;
            moteTimer = 0.02f;
            float size = modelBounds.extents.magnitude;
            Vector3 local = new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f) * modelBounds.size.x, 0f, UnityEngine.Random.Range(-0.6f, 0.6f) * modelBounds.size.z);
            // ShipFx.Emit(scanPlate.position + transform.rotation * local, Vector3.up * (size * UnityEngine.Random.Range(0.5f, 2f)),
            //     size * UnityEngine.Random.Range(0.01f, 0.03f), UnityEngine.Random.Range(0.3f, 0.7f), hologramColor);
        }

        void FinishMaterializing()
        {
            projecting = false;
            Wrecked = false;
            ShowSolid(true);
            // scanPlate.gameObject.SetActive(false);
            health.Restore();
            SetCollidersOn(true);
            Hold(false);

            float size = modelBounds.extents.magnitude;
            Vector3 centre = transform.TransformPoint(modelBounds.center);
            for (int i = 0; i < 40; i++)
                ShipFx.Emit(centre, UnityEngine.Random.onUnitSphere * (size * UnityEngine.Random.Range(2f, 5f)),
                    size * UnityEngine.Random.Range(0.012f, 0.03f), UnityEngine.Random.Range(0.25f, 0.55f), hologramColor);

            Materialized?.Invoke();
        }

        void ShowSolid(bool solid)
        {
            if (solid == showingSolid) return;
            showingSolid = solid;
            for (int i = 0; i < parts.Count; i++) parts[i].sharedMaterials = solid ? solidMaterials[i] : projectionMaterials[i];
        }

        void SetVisible(bool visible)
        {
            foreach (var part in parts) part.forceRenderingOff = !visible;
        }

        void SetCollidersOn(bool on)
        {
            foreach (var box in colliders) box.enabled = on;
        }

        /// <summary>Pin a physics ship in place while it's wrecked, so it can't drift off out of sight.</summary>
        void Hold(bool hold)
        {
            if (!body || body.isKinematic) return;
            if (hold)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                keptConstraints = body.constraints;
                body.constraints = RigidbodyConstraints.FreezeAll;
            }
            else
            {
                body.constraints = keptConstraints;
            }
        }

        void OnDestroy()
        {
            if (projection) Destroy(projection);
            // if (scanPlateMaterial) Destroy(scanPlateMaterial);
        }
    }
}
