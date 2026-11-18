using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// A huge hollow sphere around a map that ships can't leave. Its inside is solid to ships, so they bump off it.
    ///   See-through: a faint glowing grid, like a force field (Sky Island).
    ///   Rock: a solid cave wall, for a world underground (Underground City).
    /// It never casts shadows, so the sun still lights everything inside.
    /// </summary>
    public class BarrierSphere : GeneratedShape, IPlayArea
    {
        public bool Contains(Vector3 spot) => Vector3.Distance(spot, transform.position) < radius - 15f;
        public Vector3 Inside(Vector3 spot) => transform.position;

        public enum Look { SeeThrough, Rock }

        [Tooltip("How far the wall is from this object's center.")]
        public float radius = 750f;
        public Look look = Look.SeeThrough;
        [Tooltip("The force field's color (see-through), or the rock's color.")]
        public Color color = Palette.HologramBlue;

        protected override void Build(Transform root)
        {
            var mesh = InsideOfSphere(NewMesh("Barrier"), radius, 48, 96);
            Material material;
            if (look == Look.SeeThrough)
            {
                material = Keep(new Material(Shader.Find("SpaceCowboy/Barrier")));
                material.SetColor("_BarrierColor", color);
            }
            else material = Keep(Palette.TechToon(color));
            Show("Barrier", root, mesh, material, solid: true);
        }

        /// <summary>A sphere whose faces point inward, so it's seen (and felt) from inside.</summary>
        public static Mesh InsideOfSphere(Mesh mesh, float radius, int rings, int segments)
        {
            var points = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int r = 0; r <= rings; r++)
            {
                float lat = Mathf.PI * r / rings; // 0 at the top
                for (int s = 0; s <= segments; s++)
                {
                    float lon = 2f * Mathf.PI * s / segments;
                    var dir = new Vector3(Mathf.Sin(lat) * Mathf.Sin(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Cos(lon));
                    points.Add(dir * radius);
                    normals.Add(-dir);
                    uvs.Add(new Vector2((float)s / segments, 1f - (float)r / rings));
                }
            }
            var triangles = new List<int>();
            int row = segments + 1;
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * row + s, b = a + 1, c = a + row, d = c + 1;
                    // Wound to face inward.
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            mesh.SetVertices(points);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
