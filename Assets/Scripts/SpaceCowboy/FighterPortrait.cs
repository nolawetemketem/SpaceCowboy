using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// A picture of a fighter's ship for the fighter grid on the menu. It's taken once, by a little camera that
    /// photographs a copy of the ship's model on its own (on a layer nothing else uses), against a clear background.
    ///
    /// For now every fighter flies the same ship, so they share one picture. When fighters get their own models,
    /// take one picture per model.
    /// </summary>
    public static class FighterPortrait
    {
        /// <summary>A layer no scene object is on, so the camera sees only the copy.</summary>
        const int PhotoLayer = 31;
        /// <summary>Far below everything, so the copy is never in the game's view during the one frame it exists.</summary>
        static readonly Vector3 Studio = new Vector3(0f, -20000f, 0f);

        /// <summary>Photograph a ship model from the front, a little to the side and above. width × height pixels.</summary>
        public static Texture2D Take(Transform model, int width = 320, int height = 160)
        {
            if (!model) return null;

            // Copy the model into an inactive holder, so none of its scripts wake up. Keep only what's drawn.
            var holder = new GameObject("Portrait Studio");
            holder.SetActive(false);
            holder.transform.position = Studio;
            var copy = Object.Instantiate(model.gameObject, holder.transform);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
            foreach (var part in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(part);
            foreach (Transform extra in copy.transform)
                if (extra.name == "Carried Flag" || extra.name == "Shield") Object.DestroyImmediate(extra.gameObject);

            Bounds bounds = default;
            bool first = true;
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                r.gameObject.layer = PhotoLayer;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var materials = r.sharedMaterials;

            }
            holder.SetActive(true);
            foreach (var r in copy.GetComponentsInChildren<Renderer>())
            {
                if (first) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                first = false;
            }

            // The camera: in front of the nose (the ship faces +Z), off to its right and a little above, looking back at it.
            var camGo = new GameObject("Portrait Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = 1 << PhotoLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.fieldOfView = 22f;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
            float distance = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.48f;
            Vector3 from = new Vector3(0.95f, 0.5f, 1.05f).normalized;
            cam.transform.position = bounds.center + from * distance;
            cam.transform.LookAt(bounds.center);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = distance * 3f;

            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 1;
            cam.targetTexture = target;
            cam.aspect = width / (float)height;
            // Its own sun for the one frame, so the picture looks the same at any time of day
            // (the toon shader is lit by the scene's main light, which is dim at dusk and night).
            var lightGo = new GameObject("Portrait Light");
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Color.white;
            sun.intensity = 2f; // the toon shader's plain-color brightness
            sun.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.LookRotation(new Vector3(-0.4f, -0.8f, -0.45f));
            var oldSun = RenderSettings.sun;
            RenderSettings.sun = sun;
            cam.Render();
            RenderSettings.sun = oldSun;
            Object.Destroy(lightGo);

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var picture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Fighter Portrait" };
            picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            picture.Apply();
            RenderTexture.active = previous;

            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(camGo);
            Object.Destroy(holder);
            return picture;
        }
    }
}
