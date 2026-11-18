using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// A piece of a map that builds itself from a few numbers: a floating island, a ring tube, a barrier sphere.
    /// It builds in the editor too, so you see it while you place things, and rebuilds when you change a number
    /// in the Inspector. What it builds is never saved into the scene; it's made fresh every time the map loads.
    /// </summary>
    [ExecuteAlways]
    public abstract class GeneratedShape : MonoBehaviour
    {
        Transform built;
        readonly List<Object> made = new();

        protected abstract void Build(Transform root);

        void OnEnable() => Rebuild();

#if UNITY_EDITOR
        void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this && isActiveAndEnabled) Rebuild(); };
#endif

        void OnDisable() => Clear();

        public void Rebuild()
        {
            Clear();
            built = new GameObject("Built").transform;
            Keep(built.gameObject);
            built.SetParent(transform, false);
            Build(built);
        }

        void Clear()
        {
            foreach (var thing in made)
                if (thing)
                {
                    if (Application.isPlaying) Destroy(thing);
                    else DestroyImmediate(thing);
                }
            made.Clear();
            built = null;
        }

        /// <summary>Remember something this built, so it's cleaned up on the next rebuild and never saved in the scene.</summary>
        protected T Keep<T>(T thing) where T : Object
        {
            if (!Application.isPlaying) thing.hideFlags = HideFlags.DontSave;
            made.Add(thing);
            return thing;
        }

        /// <summary>A child that shows this mesh, optionally solid for ships and for finding the ground.</summary>
        protected GameObject Show(string name, Transform root, Mesh mesh, Material material, bool solid, bool castShadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var look = go.AddComponent<MeshRenderer>();
            look.sharedMaterial = material;
            look.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            if (solid) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        /// <summary>A toon material colored by the mesh's vertex colors (like the desert ground).</summary>
        protected Material ToonByVertex()
        {
            var m = Keep(Palette.Toon(Color.white));
            m.EnableKeyword("_VERTEX_COLORS");
            m.SetFloat("_UseVertexColors", 1f);
            return m;
        }

        protected Mesh NewMesh(string name) => Keep(new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 });
    }
}
