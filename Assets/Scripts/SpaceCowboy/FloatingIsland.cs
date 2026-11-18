using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// An island floating in the sky: a grassy top with gentle bumps, and a jagged rock underside that
    /// narrows to a point far below, like a chunk torn out of the ground. Its top is centered on this object.
    /// Ships bump into it.
    /// </summary>
    public class FloatingIsland : GeneratedShape
    {
        [Tooltip("How far the island's top reaches from its center.")]
        public float radius = 260f;
        [Tooltip("How far the rock hangs below the top.")]
        public float depth = 220f;
        [Tooltip("How tall the bumps on top are.")]
        public float hills = 14f;
        public Color grass = Palette.Hex("#7aa04a");
        public Color grassLight = Palette.Hex("#9cc062");
        public Color rock = Palette.Hex("#8a6a4e");
        public Color rockDark = Palette.Hex("#5d4636");
        public int seed = 7;

        protected override void Build(Transform root)
        {
            const int around = 96, topRings = 14, sideRings = 18;
            var points = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            float noise = seed * 13.7f;

            // The edge wobbles a little, so it isn't a perfect circle.
            float Edge(float a) => radius * (1f + 0.08f * (Mathf.PerlinNoise(noise + Mathf.Cos(a) * 1.5f, noise + Mathf.Sin(a) * 1.5f) - 0.5f) * 2f);

            // Top: rings from the center out to the edge.
            points.Add(Vector3.up * Hill(0f, 0f));
            colors.Add(grass);
            for (int r = 1; r <= topRings; r++)
            {
                float f = (float)r / topRings;
                for (int i = 0; i < around; i++)
                {
                    float a = 2f * Mathf.PI * i / around;
                    float x = Mathf.Sin(a) * Edge(a) * f, z = Mathf.Cos(a) * Edge(a) * f;
                    float y = Hill(x, z) * (1f - f * f * 0.7f); // flatter toward the edge
                    points.Add(new Vector3(x, y, z));
                    colors.Add(Color.Lerp(grass, grassLight, Mathf.PerlinNoise(noise + x * 0.02f, z * 0.02f)));
                }
            }
            for (int i = 0; i < around; i++)
            {
                int next = (i + 1) % around;
                triangles.Add(0); triangles.Add(1 + i); triangles.Add(1 + next);
            }
            for (int r = 1; r < topRings; r++)
            {
                int inner = 1 + (r - 1) * around, outer = 1 + r * around;
                for (int i = 0; i < around; i++)
                {
                    int next = (i + 1) % around;
                    triangles.Add(inner + i); triangles.Add(outer + i); triangles.Add(outer + next);
                    triangles.Add(inner + i); triangles.Add(outer + next); triangles.Add(inner + next);
                }
            }

            // Underside: from the edge down to a point, narrowing, with jagged rock.
            int edgeStart = 1 + (topRings - 1) * around;
            int previous = edgeStart;
            for (int r = 1; r <= sideRings; r++)
            {
                float f = (float)r / sideRings;
                float shrink = Mathf.Pow(1f - f, 0.8f);
                int start = points.Count;
                for (int i = 0; i < around; i++)
                {
                    float a = 2f * Mathf.PI * i / around;
                    float jag = 0.75f + 0.5f * Mathf.PerlinNoise(noise + i * 0.35f, r * 0.6f);
                    float reach = Edge(a) * shrink * jag;
                    points.Add(new Vector3(Mathf.Sin(a) * reach, -depth * f, Mathf.Cos(a) * reach));
                    colors.Add(Color.Lerp(rock, rockDark, f * 0.8f + 0.2f * Mathf.PerlinNoise(i * 0.4f, r)));
                }
                for (int i = 0; i < around; i++)
                {
                    int next = (i + 1) % around;
                    triangles.Add(previous + i); triangles.Add(start + i); triangles.Add(start + next);
                    triangles.Add(previous + i); triangles.Add(start + next); triangles.Add(previous + next);
                }
                previous = start;
            }

            var mesh = NewMesh("Floating Island");
            mesh.SetVertices(points);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Show("Island", root, mesh, ToonByVertex(), solid: true, castShadows: true);

            float Hill(float x, float z) => (Mathf.PerlinNoise(noise + x * 0.008f, noise + z * 0.008f) - 0.3f) * hills;
        }
    }
}
