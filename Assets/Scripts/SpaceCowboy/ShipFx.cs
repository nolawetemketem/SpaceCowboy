using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// One shared particle system for ship effects: explosion sparks, laser impacts and holographic motes.
    /// Anything can call Emit; the particles live on their own after that.
    /// Every particle is a soft glowing point (never a square), stretched into a streak along the way it flies.
    /// </summary>
    public static class ShipFx
    {
        static ParticleSystem system;

        /// <summary>Saturated fire-orange for sparks, from deep to hot. Pick between them with Spark().</summary>
        public static readonly Color SparkDeep = new Color(1f, 0.28f, 0.02f);
        public static readonly Color SparkHot = new Color(1f, 0.55f, 0.08f);

        /// <summary>A random spark color between deep and hot orange.</summary>
        public static Color Spark() => Color.Lerp(SparkDeep, SparkHot, Random.value);

        /// <summary>Make the particle system now, while the game loads, so the first explosion doesn't have to.</summary>
        public static void Prewarm()
        {
            if (!system) system = Build();
        }

        public static void Emit(Vector3 position, Vector3 velocity, float size, float seconds, Color color)
        {
            if (!system) system = Build();
            var particle = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = seconds,
                startColor = color,
            };
            system.Emit(particle, 1);
        }

        static ParticleSystem Build()
        {
            var go = new GameObject("Ship FX");
            var particles = go.AddComponent<ParticleSystem>();

            var main = particles.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3000;

            var emission = particles.emission;
            emission.enabled = false; // only what Emit asks for
            var shape = particles.shape;
            shape.enabled = false;

            // Every particle fades out as it ends.
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var look = go.GetComponent<ParticleSystemRenderer>();
            look.sharedMaterial = new Material(Shader.Find("SpaceCowboy/Spark"));
            // Fast particles stretch into thin streaks along their path, like sparks off a grinder or a fire.
            look.renderMode = ParticleSystemRenderMode.Stretch;
            look.lengthScale = 1f;
            look.velocityScale = 0.025f;
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            look.receiveShadows = false;

            particles.Play();
            return particles;
        }
    }
}
