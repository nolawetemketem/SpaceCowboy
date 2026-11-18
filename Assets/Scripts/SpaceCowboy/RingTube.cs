using System.Collections.Generic;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// The inside of a giant hollow donut, lying flat around this object: you fly in the tube, round and round.
    /// The walls are solid to ships. Bands of color around the tube show how fast you're going.
    /// A point in the tube's middle at angle a (degrees, 0 = toward -Z, going toward +X) is MiddleAt(a).
    /// </summary>
    public class RingTube : GeneratedShape, IPlayArea
    {
        /// <summary>Is this spot inside the tube?</summary>
        public bool Contains(Vector3 spot)
        {
            Vector3 local = transform.InverseTransformPoint(spot);
            Vector2 flat = new Vector2(local.x, local.z);
            float fromMiddle = new Vector2(flat.magnitude - ringRadius, local.y).magnitude;
            return fromMiddle < tubeRadius - 12f;
        }

        /// <summary>The nearest point on the tube's middle line.</summary>
        public Vector3 Inside(Vector3 spot)
        {
            Vector3 local = transform.InverseTransformPoint(spot);
            Vector3 flat = new Vector3(local.x, 0f, local.z);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.back;
            return transform.TransformPoint(flat.normalized * ringRadius);
        }

        [Tooltip("From the donut's center to the middle of the tube.")]
        public float ringRadius = 800f;
        [Tooltip("How wide the tube is, from its middle to the wall.")]
        public float tubeRadius = 150f;
        public Color wall = Palette.Hex("#5b6470");
        public Color band = Palette.Hex("#3f4650");
        [Tooltip("How many bands of color go round the donut.")]
        public int bands = 48;

        /// <summary>The middle of the tube at this angle, in world space.</summary>
        public Vector3 MiddleAt(float degrees) => transform.TransformPoint(MiddleLocal(degrees * Mathf.Deg2Rad, ringRadius));

        /// <summary>The way along the tube at this angle (toward bigger angles), in world space.</summary>
        public Vector3 AlongAt(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return transform.TransformDirection(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
        }

        static Vector3 MiddleLocal(float a, float radius) => new Vector3(Mathf.Sin(a) * radius, 0f, -Mathf.Cos(a) * radius);

        protected override void Build(Transform root)
        {
            const int around = 256, across = 40;
            var points = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            for (int i = 0; i <= around; i++)
            {
                float a = 2f * Mathf.PI * i / around;
                Vector3 middle = MiddleLocal(a, ringRadius);
                Vector3 outward = middle.normalized;
                bool banded = (i * bands / around) % 2 == 0;
                for (int j = 0; j <= across; j++)
                {
                    float b = 2f * Mathf.PI * j / across;
                    Vector3 dir = outward * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    points.Add(middle + dir * tubeRadius);
                    normals.Add(-dir); // facing into the tube
                    // A darker floor and ceiling stripe helps tell up from down.
                    float floor = Mathf.Abs(Mathf.Sin(b));
                    colors.Add(Color.Lerp(banded ? wall : band, band * 0.8f, floor * 0.35f));
                }
            }
            var triangles = new List<int>();
            int row = across + 1;
            for (int i = 0; i < around; i++)
                for (int j = 0; j < across; j++)
                {
                    int p = i * row + j, q = p + 1, r = p + row, s = r + 1;
                    // Wound to face into the tube.
                    triangles.Add(p); triangles.Add(r); triangles.Add(q);
                    triangles.Add(q); triangles.Add(r); triangles.Add(s);
                }
            var mesh = NewMesh("Ring Tube");
            mesh.SetVertices(points);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Show("Tube", root, mesh, ToonByVertex(), solid: true);
        }
    }
}
