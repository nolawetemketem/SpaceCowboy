using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// Builds a glowing wireframe of a ship model: a thin line along every edge of its mesh and a small
    /// spark at every corner. Works for any model, so each ship's grid matches its own shape.
    ///
    /// Each point of the wireframe knows when the trace reaches it. The trace starts at one point and
    /// travels along the edges, corner to corner (the shortest way round the mesh), so the grid
    /// grows over the ship like it's being drawn. The SpaceCowboy/Wireframe shader does the growing.
    /// </summary>
    public static class ShipWireframe
    {
        static readonly Dictionary<string, List<Mesh>> known = new();

        /// <summary>
        /// Make a wireframe for each part, as a hidden child of that part. Thickness is in world units.
        /// The trace starts from <paramref name="start"/>, a point in the ship's own space.
        /// </summary>
        public static List<Renderer> Build(Transform ship, List<Renderer> parts, Vector3 start, float thickness, Material material)
        {
            var readable = new List<Renderer>();
            var key = new System.Text.StringBuilder($"{start}|{thickness}");
            foreach (var part in parts)
            {
                var mesh = ShipDestruction.MeshOf(part);
                if (!mesh || !mesh.isReadable) continue;
                readable.Add(part);
                key.Append('|').Append(mesh.GetInstanceID());
            }

            // Ships with the same model share one set of grids: they'd come out identical anyway.
            if (!known.TryGetValue(key.ToString(), out var meshes) || meshes.Exists(m => !m))
            {
                // First gather every part's corners and edges, so the trace can be timed across the whole ship.
                var shapes = new List<Shape>();
                foreach (var part in readable)
                {
                    Matrix4x4 toShip = ship.worldToLocalMatrix * part.transform.localToWorldMatrix;
                    shapes.Add(new Shape(part, ShipDestruction.MeshOf(part), toShip));
                }
                float latest = 0f;
                foreach (var shape in shapes) latest = Mathf.Max(latest, shape.TimeTrace(start));
                meshes = shapes.ConvertAll(shape => shape.ToMesh(latest, thickness));
                known[key.ToString()] = meshes;
            }

            var wires = new List<Renderer>();
            for (int i = 0; i < readable.Count; i++)
            {
                var go = new GameObject("Wireframe");
                go.transform.SetParent(readable[i].transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.SetActive(false);
                wires.Add(renderer);
            }
            return wires;
        }

        /// <summary>One part's corners (with matching positions merged) and the edges between them.</summary>
        class Shape
        {
            public readonly Renderer part;
            readonly List<Vector3> corners = new(); // in the part's own space
            readonly List<Vector3> cornersInShip = new();
            readonly List<(int a, int b)> edges = new();
            readonly List<List<(int to, float length)>> links = new();
            readonly Matrix4x4 toShip;
            float[] arrival;

            public Shape(Renderer part, Mesh mesh, Matrix4x4 toShip)
            {
                this.part = part;
                this.toShip = toShip;

                // Meshes repeat a corner wherever its faces have different normals or UVs; merge those.
                var vertices = mesh.vertices;
                var merged = new int[vertices.Length];
                var byPosition = new Dictionary<Vector3Int, int>();
                for (int i = 0; i < vertices.Length; i++)
                {
                    var key = Vector3Int.RoundToInt(vertices[i] * 10000f);
                    if (!byPosition.TryGetValue(key, out int id))
                    {
                        id = corners.Count;
                        byPosition[key] = id;
                        corners.Add(vertices[i]);
                        cornersInShip.Add(toShip.MultiplyPoint3x4(vertices[i]));
                        links.Add(new List<(int, float)>());
                    }
                    merged[i] = id;
                }

                // Every side of every triangle is an edge; shared sides only count once.
                var seen = new HashSet<long>();
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
                    var triangles = mesh.GetTriangles(sub);
                    for (int t = 0; t < triangles.Length; t += 3)
                        for (int side = 0; side < 3; side++)
                        {
                            int a = merged[triangles[t + side]], b = merged[triangles[t + (side + 1) % 3]];
                            if (a == b) continue;
                            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                            if (!seen.Add(key)) continue;
                            edges.Add((a, b));
                            float length = Vector3.Distance(cornersInShip[a], cornersInShip[b]);
                            links[a].Add((b, length));
                            links[b].Add((a, length));
                        }
                }
            }

            /// <summary>
            /// How far the trace has travelled when it reaches each corner, going along the edges.
            /// A loose piece the edges can't reach starts on its own, at its straight-line distance from the start.
            /// Returns the latest moment anything in this part is reached.
            /// </summary>
            public float TimeTrace(Vector3 start)
            {
                arrival = new float[corners.Count];
                for (int i = 0; i < arrival.Length; i++) arrival[i] = float.MaxValue;
                var done = new bool[corners.Count];
                var queue = new SortedSet<(float time, int corner)>();
                float latest = 0f;

                for (int left = corners.Count; left > 0;)
                {
                    // The nearest corner not yet reached starts the next spread.
                    int seed = -1;
                    float best = float.MaxValue;
                    for (int i = 0; i < corners.Count; i++)
                    {
                        if (done[i]) continue;
                        float d = Vector3.Distance(cornersInShip[i], start);
                        if (d < best) { best = d; seed = i; }
                    }
                    arrival[seed] = best;
                    queue.Add((best, seed));

                    while (queue.Count > 0)
                    {
                        var (time, corner) = queue.Min;
                        queue.Remove(queue.Min);
                        if (done[corner]) continue;
                        done[corner] = true;
                        left--;
                        latest = Mathf.Max(latest, time);
                        foreach (var (to, length) in links[corner])
                        {
                            float reach = time + length;
                            if (done[to] || reach >= arrival[to]) continue;
                            queue.Remove((arrival[to], to));
                            arrival[to] = reach;
                            queue.Add((reach, to));
                        }
                    }
                }

                // An edge finishes when it's drawn all the way along, which can be after its far corner is reached another way.
                foreach (var (a, b) in edges)
                {
                    float length = Vector3.Distance(cornersInShip[a], cornersInShip[b]);
                    latest = Mathf.Max(latest, Mathf.Min(arrival[a], arrival[b]) + length);
                }
                return latest;
            }

            /// <summary>
            /// Each edge becomes two thin crossed strips, so it looks like a line from every angle.
            /// Each corner becomes a small three-way cross of light. uv.x is when the trace gets there, from 0 to 1.
            /// </summary>
            public Mesh ToMesh(float latest, float thickness)
            {
                // Thickness is in world units; the mesh is in the part's own (possibly scaled) space.
                float scale = toShip.lossyScale.magnitude / Mathf.Sqrt(3f);
                float half = thickness / Mathf.Max(scale, 0.0001f) / 2f;
                float spark = half * 2.2f;
                float total = Mathf.Max(latest, 0.0001f);

                var points = new List<Vector3>();
                var times = new List<Vector2>();
                var indices = new List<int>();

                void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t0, float t1, float t2, float t3)
                {
                    int i = points.Count;
                    points.Add(p0); points.Add(p1); points.Add(p2); points.Add(p3);
                    times.Add(new Vector2(t0 / total, 0f)); times.Add(new Vector2(t1 / total, 0f));
                    times.Add(new Vector2(t2 / total, 0f)); times.Add(new Vector2(t3 / total, 0f));
                    indices.Add(i); indices.Add(i + 1); indices.Add(i + 2);
                    indices.Add(i); indices.Add(i + 2); indices.Add(i + 3);
                }

                foreach (var (first, second) in edges)
                {
                    // Draw from whichever end the trace reaches first.
                    int a = arrival[first] <= arrival[second] ? first : second;
                    int b = a == first ? second : first;
                    Vector3 from = corners[a], to = corners[b];
                    Vector3 along = to - from;
                    if (along.sqrMagnitude < 1e-12f) continue;
                    float tA = arrival[a];
                    float tB = tA + Vector3.Distance(cornersInShip[a], cornersInShip[b]);

                    Vector3 side = Vector3.Cross(along, Mathf.Abs(along.normalized.y) < 0.9f ? Vector3.up : Vector3.right).normalized * half;
                    Vector3 up = Vector3.Cross(along, side).normalized * half;
                    Quad(from - side, from + side, to + side, to - side, tA, tA, tB, tB);
                    Quad(from - up, from + up, to + up, to - up, tA, tA, tB, tB);
                }

                for (int c = 0; c < corners.Count; c++)
                {
                    Vector3 p = corners[c];
                    float t = arrival[c];
                    Quad(p + new Vector3(-spark, -spark, 0), p + new Vector3(spark, -spark, 0), p + new Vector3(spark, spark, 0), p + new Vector3(-spark, spark, 0), t, t, t, t);
                    Quad(p + new Vector3(0, -spark, -spark), p + new Vector3(0, spark, -spark), p + new Vector3(0, spark, spark), p + new Vector3(0, -spark, spark), t, t, t, t);
                    Quad(p + new Vector3(-spark, 0, -spark), p + new Vector3(spark, 0, -spark), p + new Vector3(spark, 0, spark), p + new Vector3(-spark, 0, spark), t, t, t, t);
                }

                var mesh = new Mesh { name = part.name + " Wireframe" };
                if (points.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(points);
                mesh.SetUVs(0, times);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
