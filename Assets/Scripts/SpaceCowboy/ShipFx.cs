using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// One shared particle system for ship effects: explosion sparks and holographic motes.
    /// Anything can call Emit; the particles live on their own after that.
    /// </summary>
    public static class ShipFx
    {
        static ParticleSystem system;

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
            look.sharedMaterial = Palette.Unlit(Color.white);
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            look.receiveShadows = false;

            particles.Play();
            return particles;
        }
    }
}
