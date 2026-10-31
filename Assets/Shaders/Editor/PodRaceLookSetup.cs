using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One click to give the StaticPodRace scene Space Cowboy Web's look: toon shading, ink outlines,
/// and the gradient sky. Safe to run again; it reuses what it made last time.
///
/// Like applyToonToModel() in the web game, every material is swapped for a toon one that keeps
/// its color and texture. The original materials and prefabs aren't touched, so other scenes
/// (the title screen) look the same as before.
/// </summary>
public static class PodRaceLookSetup
{
    const string ScenePath = "Assets/Scenes/StaticPodRace.unity";
    const string Root = "Assets/Shaders";
    const string ToonMaterialFolder = Root + "/Materials/Toon";
    const string ToonPrefabFolder = Root + "/Prefabs";
    const string SkyMaterialPath = Root + "/Materials/GradientSky.mat";
    const string InkMaterialPath = Root + "/Materials/InkOutline.mat";
    const string PipelinePath = "Assets/Settings/PC_RPAsset.asset";
    const string BaseRendererPath = "Assets/Settings/PC_Renderer.asset";
    // A copy of the normal renderer with the ink pass added; only the pod race camera uses it.
    const string PodRaceRendererPath = Root + "/PodRace_Renderer.asset";

    static readonly Dictionary<Material, Material> toonFor = new();

    [MenuItem("Space Cowboy/Apply Toon Look to Pod Race Scene")]
    public static void Apply()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath);
        toonFor.Clear();
        Directory.CreateDirectory(ToonMaterialFolder);
        Directory.CreateDirectory(ToonPrefabFolder);

        int renderers = ToonifySceneRenderers();
        int terrains = ToonifyTerrains();
        int prefabs = ToonifySpawnedPrefabs();
        SetUpSky();
        SetUpInkAndCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Space Cowboy] Pod race look applied: {renderers} renderers, {terrains} terrains, " +
                  $"{prefabs} spawned prefabs, {toonFor.Count} toon materials.");
    }

    // ---- Toon ------------------------------------------------------------------------------

    /// The toon copy of a material: same color and texture, toon shader. Made once, then reused.
    static Material ToonFor(Material source)
    {
        var toonShader = Shader.Find("SpaceCowboy/Toon");
        if (source == null || source.shader == toonShader) return source;
        if (toonFor.TryGetValue(source, out var cached)) return cached;

        string path = $"{ToonMaterialFolder}/{source.name}_Toon.mat";
        var toon = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (toon == null)
        {
            toon = new Material(toonShader);
            if (source.HasProperty("_BaseColor")) toon.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            else if (source.HasProperty("_Color")) toon.SetColor("_BaseColor", source.GetColor("_Color"));

            string mapName = source.HasProperty("_BaseMap") ? "_BaseMap" : source.HasProperty("_MainTex") ? "_MainTex" : null;
            if (mapName != null && source.GetTexture(mapName) != null)
            {
                toon.SetTexture("_BaseMap", source.GetTexture(mapName));
                toon.SetTextureScale("_BaseMap", source.GetTextureScale(mapName));
                toon.SetTextureOffset("_BaseMap", source.GetTextureOffset(mapName));
            }
            AssetDatabase.CreateAsset(toon, path);
        }
        toonFor[source] = toon;
        return toon;
    }

    static void ToonifyRenderer(Renderer r)
    {
        if (r is not MeshRenderer && r is not SkinnedMeshRenderer) return;
        var materials = r.sharedMaterials;
        for (int i = 0; i < materials.Length; i++) materials[i] = ToonFor(materials[i]);
        Undo.RecordObject(r, "Apply toon materials");
        r.sharedMaterials = materials;
        PrefabUtility.RecordPrefabInstancePropertyModifications(r);
    }

    /// The fighter, TerrainH, and anything else placed in the scene.
    static int ToonifySceneRenderers()
    {
        var all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var r in all) ToonifyRenderer(r);
        return all.Length;
    }

    /// Unity Terrain draws with one material for the whole thing.
    static int ToonifyTerrains()
    {
        var all = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in all)
        {
            Undo.RecordObject(t, "Apply toon terrain material");
            t.materialTemplate = ToonFor(t.materialTemplate);
        }
        return all.Length;
    }

    /// Rocks and cacti are spawned at runtime, so point the spawners at toon variants of their prefabs.
    static int ToonifySpawnedPrefabs()
    {
        int count = 0;
        var spawners = Object.FindObjectsByType<RandomizeObjectsWithinRectangle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var spawner in spawners)
        {
            if (spawner.objects == null) continue;
            Undo.RecordObject(spawner, "Use toon prefabs");
            for (int i = 0; i < spawner.objects.Count; i++)
            {
                spawner.objects[i] = ToonPrefabFor(spawner.objects[i]);
                count++;
            }
            EditorUtility.SetDirty(spawner);
        }
        return count;
    }

    /// A prefab variant of `prefab` with toon materials (so it still follows changes to the original).
    static GameObject ToonPrefabFor(GameObject prefab)
    {
        if (prefab == null || prefab.name.EndsWith("_Toon")) return prefab;
        string path = $"{ToonPrefabFolder}/{prefab.name}_Toon.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        foreach (var r in instance.GetComponentsInChildren<Renderer>(true)) ToonifyRenderer(r);
        var variant = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return variant;
    }

    // ---- Sky -------------------------------------------------------------------------------

    static void SetUpSky()
    {
        var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (sky == null)
        {
            sky = new Material(Shader.Find("SpaceCowboy/GradientSky"));
            AssetDatabase.CreateAsset(sky, SkyMaterialPath);
        }
        else if (sky.shader.name != "SpaceCowboy/GradientSky")
        {
            sky.shader = Shader.Find("SpaceCowboy/GradientSky");
            EditorUtility.SetDirty(sky);
        }
        RenderSettings.skybox = sky;

        // The toon shader lights things with the main light; make sure that's the scene's sun.
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            RenderSettings.sun = light;
            break;
        }
        DynamicGI.UpdateEnvironment();
    }

    // ---- Ink -------------------------------------------------------------------------------

    static void SetUpInkAndCamera()
    {
        var ink = AssetDatabase.LoadAssetAtPath<Material>(InkMaterialPath);
        if (ink == null)
        {
            ink = new Material(Shader.Find("SpaceCowboy/InkOutline"));
            AssetDatabase.CreateAsset(ink, InkMaterialPath);
        }

        // 1. A renderer for this scene: the normal one, plus the ink pass.
        if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PodRaceRendererPath) == null)
            AssetDatabase.CopyAsset(BaseRendererPath, PodRaceRendererPath);
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PodRaceRendererPath);
        AddInkFeature(rendererData, ink);

        // 2. Register it with the pipeline asset so cameras can pick it.
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        int rendererIndex = AddRenderer(pipeline, rendererData);

        // 3. The scene's camera uses it, with post-processing and SMAA to smooth the ink lines.
        var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        var cameraData = camera.GetUniversalAdditionalCameraData();
        Undo.RecordObject(cameraData, "Set pod race renderer");
        cameraData.SetRenderer(rendererIndex);
        cameraData.renderPostProcessing = true;
        cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cameraData.antialiasingQuality = AntialiasingQuality.High;
        EditorUtility.SetDirty(cameraData);
    }

    static void AddInkFeature(UniversalRendererData rendererData, Material ink)
    {
        foreach (var f in rendererData.rendererFeatures)
            if (f is FullScreenPassRendererFeature fs && fs.passMaterial == ink) return; // already there

        var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
        feature.name = "Ink Outline";
        feature.passMaterial = ink;
        feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
        feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
        AssetDatabase.AddObjectToAsset(feature, rendererData);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

        // The feature list is serialized-only, so add to it the way the inspector's "Add Renderer Feature" does.
        var so = new SerializedObject(rendererData);
        var features = so.FindProperty("m_RendererFeatures");
        var map = so.FindProperty("m_RendererFeatureMap");
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        map.arraySize++;
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        so.ApplyModifiedProperties();
        rendererData.SetDirty();
        EditorUtility.SetDirty(rendererData);
    }

    static int AddRenderer(UniversalRenderPipelineAsset pipeline, ScriptableRendererData rendererData)
    {
        var so = new SerializedObject(pipeline);
        var list = so.FindProperty("m_RendererDataList");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == rendererData) return i;

        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = rendererData;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(pipeline);
        return list.arraySize - 1;
    }
}
