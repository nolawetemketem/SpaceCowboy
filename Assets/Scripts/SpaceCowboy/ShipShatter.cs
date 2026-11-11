using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// Cuts a ship part's mesh into jagged chunks, whatever its shape, so any ship model can break apart.
    /// Each chunk is a piece of the hull's skin, painted on both sides so it looks like a torn plate.
    /// Results are remembered, so every ship with the same model shares the same chunks.
    /// The model needs Read/Write turned on in its import settings; without it, Split returns null.
    /// </summary>
    public static class ShipShatter
    {
        public struct Chunk
        {
            public Mesh mesh;
            public Vector3 centre; // where this chunk sits in the original mesh
            public int submesh;    // which of the part's materials it uses
        }

        static readonly Dictionary<(Mesh, int), Chunk[]> known = new();

        public static Chunk[] Split(Mesh mesh, int count)
        {
            if (!mesh || !mesh.isReadable) return null;
            if (known.TryGetValue((mesh, count), out var chunks) && chunks.Length > 0 && chunks[0].mesh) return chunks;
            chunks = Build(mesh, count);
            known[(mesh, count)] = chunks;
            return chunks;
        }

        static Chunk[] Build(Mesh mesh, int count)
        {
            Vector3[] positions = mesh.vertices, normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            int totalTriangles = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) totalTriangles += (int)mesh.GetIndexCount(s) / 3;

            var chunks = new List<Chunk>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                int[] triangles = mesh.GetTriangles(s);
                int triangleCount = triangles.Length / 3;
                if (triangleCount == 0) continue;

                var middles = new Vector3[triangleCount];
                for (int t = 0; t < triangleCount; t++)
                    middles[t] = (positions[triangles[t * 3]] + positions[triangles[t * 3 + 1]] + positions[triangles[t * 3 + 2]]) / 3f;

                // Each triangle joins the seed point it's closest to.
                int seedCount = Mathf.Clamp(Mathf.RoundToInt(count * (float)triangleCount / totalTriangles), 1, triangleCount);
                var seeds = SpreadSeeds(middles, seedCount);
                var groups = new List<int>[seedCount];
                for (int g = 0; g < seedCount; g++) groups[g] = new List<int>();
                for (int t = 0; t < triangleCount; t++)
                {
                    int nearest = 0;
                    float best = float.MaxValue;
                    for (int g = 0; g < seedCount; g++)
                    {
                        float distance = (middles[t] - seeds[g]).sqrMagnitude;
                        if (distance < best) { best = distance; nearest = g; }
                    }
                    groups[nearest].Add(t);
                }

                foreach (var group in groups)
                    if (group.Count > 0) chunks.Add(MakeChunk(positions, normals, uvs, triangles, group, s));
            }
            return chunks.ToArray();
        }

        /// <summary>Pick points far from each other, so the chunks come out about the same size.</summary>
        static Vector3[] SpreadSeeds(Vector3[] points, int count)
        {
            var seeds = new Vector3[count];
            var closest = new float[points.Length];
            seeds[0] = points[0];
            for (int i = 0; i < points.Length; i++) closest[i] = (points[i] - seeds[0]).sqrMagnitude;
            for (int s = 1; s < count; s++)
            {
                int farthest = 0;
                for (int i = 1; i < points.Length; i++)
                    if (closest[i] > closest[farthest]) farthest = i;
                seeds[s] = points[farthest];
                for (int i = 0; i < points.Length; i++)
                    closest[i] = Mathf.Min(closest[i], (points[i] - seeds[s]).sqrMagnitude);
            }
            return seeds;
        }

        static Chunk MakeChunk(Vector3[] positions, Vector3[] normals, Vector2[] uvs, int[] triangles, List<int> group, int submesh)
        {
            var newIndex = new Dictionary<int, int>();
            var picked = new List<int>();
            var indices = new List<int>(group.Count * 3);
            foreach (int t in group)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    int old = triangles[t * 3 + corner];
                    if (!newIndex.TryGetValue(old, out int index))
                    {
                        index = picked.Count;
                        newIndex[old] = index;
                        picked.Add(old);
                    }
                    indices.Add(index);
                }
            }

            var centre = Vector3.zero;
            foreach (int old in picked) centre += positions[old];
            centre /= picked.Count;

            // Two copies of every corner: one for the outside, and one facing the other way for the inside.
            int n = picked.Count;
            var newPositions = new Vector3[n * 2];
            var newNormals = new Vector3[n * 2];
            var newUvs = new Vector2[n * 2];
            for (int i = 0; i < n; i++)
            {
                int old = picked[i];
                newPositions[i] = newPositions[n + i] = positions[old] - centre;
                Vector3 normal = normals.Length > old ? normals[old] : Vector3.up;
                newNormals[i] = normal;
                newNormals[n + i] = -normal;
                if (uvs.Length > old) newUvs[i] = newUvs[n + i] = uvs[old];
            }
            var allIndices = new int[indices.Count * 2];
            indices.CopyTo(allIndices);
            for (int i = 0; i < indices.Count; i += 3)
            {
                allIndices[indices.Count + i] = indices[i + 2] + n;
                allIndices[indices.Count + i + 1] = indices[i + 1] + n;
                allIndices[indices.Count + i + 2] = indices[i] + n;
            }

            var mesh = new Mesh { name = "Ship Chunk" };
            if (n * 2 > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = newPositions;
            mesh.normals = newNormals;
            mesh.uv = newUvs;
            mesh.triangles = allIndices;
            mesh.RecalculateBounds();
            return new Chunk { mesh = mesh, centre = centre, submesh = submesh };
        }
    }
}
