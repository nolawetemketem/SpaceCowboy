using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpaceCowboy
{
    /// <summary>
    /// The air between the camera and everything else: how the weather looks. On a bright day, heat and humidity
    /// scatter the light, so the picture is hazy and sun-bleached, shadows are filled in, the sun glares and
    /// bright things bloom, like an old animated film shot through warm air.
    ///   Haze:    the world fades into the horizon color with distance, and the sky pales toward it.
    ///   Bleach:  everything that isn't tech loses some of its color (ships and gadgets keep theirs, so they read).
    ///   Glare:   the sun has a glowing halo; bloom spreads anything brighter than white, with lens flares off the sun.
    ///   Film:    a warm (day) or cool (night) cast, a light vignette and film grain. Contrast stays full: close
    ///            things keep deep shadows and dark lines; only the haze fades things, with distance.
    /// TimeOfDay owns the sun, the moon and the sky's colors, and calls Apply whenever it switches.
    /// Every number is a field here, so the look can be tuned in the Inspector (changes show on the next switch).
    /// </summary>
    public class Atmosphere : MonoBehaviour
    {
        [System.Serializable]
        public class Weather
        {
            [Header("Haze")]
            [Tooltip("Distance where the haze starts, and where everything has faded into the horizon color.")]
            public float hazeStart, hazeEnd;
            [Range(0f, 1f), Tooltip("How far the sky fades toward its hazy color (0 = the sky's own colors).")]
            public float skyHaze;
            [Tooltip("The sky's color through the haze: a pale, sun-washed blue by day.")]
            public Color hazySky = Color.white;
            [Range(0f, 1f), Tooltip("How much color non-tech things lose (0 = full color, 1 = grey).")]
            public float bleach;
            [Range(0f, 1f), Tooltip("How much the haze fills in shadows (0 = the toon shader's own, full-depth shadows).")]
            public float shadowLift;
            [Tooltip("Dust in the air: the haze and the horizon are tinted this color (white = the sky's own horizon).")]
            public Color dust = Color.white;

            [Header("Glare")]
            [Tooltip("The glow around the sun (or moon) in the sky. 0 = no sun drawn.")]
            public float sunGlow;
            [Tooltip("The sun's disc, in degrees across.")]
            public float sunSize;
            [Tooltip("Bloom: how bright a thing must be to glow, and how strong the glow is.")]
            public float bloomThreshold, bloomIntensity;
            [Tooltip("Lens flares off the brightest spots (the sun).")]
            public float flare;

            [Header("Film")]
            [Range(-100f, 100f)] public float contrast;
            [Range(-100f, 100f)] public float saturation;
            [Tooltip("A color cast over everything: warm by day, cool by night.")]
            public Color filter = Color.white;
            [Range(0f, 1f)] public float vignette;
            [Range(0f, 1f)] public float grain;
        }

        public Weather brightDay = new()
        {
            hazeStart = 40f, hazeEnd = 1400f, skyHaze = 0.8f, hazySky = Palette.Hex("#3fb4dc"), bleach = 0.35f, shadowLift = 0f,
            dust = Palette.Hex("#f2d3b6"),
            sunGlow = 1.3f, sunSize = 3f, bloomThreshold = 0.92f, bloomIntensity = 0.85f, flare = 0.4f,
            contrast = 0f, saturation = 0f, filter = Palette.Hex("#fff6e6"), vignette = 0.2f, grain = 0.22f,
        };

        public Weather night = new()
        {
            hazeStart = 120f, hazeEnd = 1700f, skyHaze = 0.15f, hazySky = Palette.Hex("#6b4a5e"), bleach = 0.25f, shadowLift = 0f,
            sunGlow = 0.6f, sunSize = 2f, bloomThreshold = 0.85f, bloomIntensity = 0.55f, flare = 0.12f,
            contrast = 0f, saturation = -8f, filter = Palette.Hex("#e4ebff"), vignette = 0.28f, grain = 0.28f,
        };

        static readonly int WorldMuteId = Shader.PropertyToID("_WorldMute");
        static readonly int ShadowLiftId = Shader.PropertyToID("_ShadowLift");

        VolumeProfile profile;
        Bloom bloom;
        ScreenSpaceLensFlare flare;
        ColorAdjustments grade;
        Vignette vignette;
        FilmGrain grain;

        /// <summary>
        /// Set the air for day or night. sky is the sky material (or null), with the sky's own colors for this time
        /// of day; the haze is laid over them.
        /// </summary>
        public void Apply(bool isNight, Material sky, Color top, Color mid, Color horizon)
        {
            var w = isNight ? night : brightDay;
            if (!profile) BuildVolume();
            horizon *= w.dust; // dust in the air tints the haze and the horizon

            Shader.SetGlobalFloat(WorldMuteId, w.bleach);
            Shader.SetGlobalFloat(ShadowLiftId, w.shadowLift);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = w.hazeStart;
            RenderSettings.fogEndDistance = w.hazeEnd;
            RenderSettings.fogColor = horizon;

            if (sky)
            {
                sky.SetColor("_Top", Color.Lerp(top, w.hazySky, w.skyHaze));
                sky.SetColor("_Mid", Color.Lerp(mid, Color.Lerp(w.hazySky, horizon, 0.5f), w.skyHaze));
                sky.SetColor("_Horizon", horizon);
                sky.SetFloat("_SunGlow", w.sunGlow);
                sky.SetFloat("_SunSize", w.sunSize);
            }

            bloom.threshold.value = w.bloomThreshold;
            bloom.intensity.value = w.bloomIntensity;
            bloom.tint.value = w.filter;
            flare.intensity.value = w.flare;
            flare.tintColor.value = w.filter;
            grade.contrast.value = w.contrast;
            grade.saturation.value = w.saturation;
            grade.colorFilter.value = w.filter;
            vignette.intensity.value = w.vignette;
            grain.intensity.value = w.grain;
        }

        /// <summary>A global post-processing volume, made in code, that the weather drives.</summary>
        void BuildVolume()
        {
            var go = new GameObject("Atmosphere (post-processing)");
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Atmosphere";
            volume.sharedProfile = profile;

            bloom = profile.Add<Bloom>(true);
            bloom.scatter.value = 0.8f;        // wide, soft glow: light spreading through humid air
            flare = profile.Add<ScreenSpaceLensFlare>(true);
            grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.value = 0f;
            grade.hueShift.value = 0f;
            vignette = profile.Add<Vignette>(true);
            vignette.smoothness.value = 0.45f;
            vignette.color.value = Color.black;
            grain = profile.Add<FilmGrain>(true);
            grain.type.value = FilmGrainLookup.Thin1;
            grain.response.value = 0.8f;
        }

        void OnDestroy()
        {
            // Leave the editor looking as the scene was built.
            Shader.SetGlobalFloat(WorldMuteId, 0f);
            Shader.SetGlobalFloat(ShadowLiftId, 0f);
            if (profile) Destroy(profile);
        }
    }
}
