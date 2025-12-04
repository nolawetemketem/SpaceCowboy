using UnityEngine;
using EditorAttributes;
using Unity.Mathematics;
using UnityEngine.Splines;
using System.Collections.Generic;

namespace Scripts
{
    [ExecuteInEditMode, RequireComponent(typeof(SplineContainer), typeof(MeshFilter), typeof(MeshRenderer))]
    public class SplineRoad : MonoBehaviour
    {
                [Header("Road Config:")]
        [SerializeField, Range(10f, 200f)] private int roadResolution = 10;
                [SerializeField, Range(1f, 10f)] private float roadWidth = 1f;
                [SerializeField] private bool worldSpaceUV;

        private MeshFilter meshFilter;
                private SplineContainer splineContainer;

                private List<Vector3> vertPoints01;
        private List<Vector3> vertPoints02;

                void Awake() => meshFilter = GetComponent<MeshFilter>();

                void OnEnable()
                {
                        splineContainer = GetComponent<SplineContainer>();

                        Spline.Changed += OnSplineChanged;

                        BuildMesh();
                }

                void OnDisable() => Spline.Changed -= OnSplineChanged;

                private void OnSplineChanged(Spline arg1, int arg2, SplineModification arg3) => BuildMesh();

                [Button]
                public void RemoveSplineComponents()
                {
                        DestroyImmediate(this);
                        DestroyImmediate(splineContainer);
                }

                [Button]
                public void BuildMesh()
        {
                        GetVerts();

                        var mesh = new Mesh();
            var triangleList = new List<int>();

                        var normalList = new List<Vector3>();
            var vertexList = new List<Vector3>();
            var uvList = new List<Vector2>();

                        for (int currentSplineIndex = 0; currentSplineIndex < splineContainer.Splines.Count; currentSplineIndex++)
            {
                int splineOffset = roadResolution * currentSplineIndex;

                                splineOffset += currentSplineIndex;
                                float uvOffset = 0f;

                for (int currentSplinePoint = 1; currentSplinePoint < roadResolution + 1; currentSplinePoint++)
                {
                    int vertexOffset = splineOffset + currentSplinePoint;

                    var p1 = vertPoints01[vertexOffset - 1];
                    var p2 = vertPoints02[vertexOffset - 1];
                    var p3 = vertPoints01[vertexOffset];
                    var p4 = vertPoints02[vertexOffset];

                    int offset = 4 * roadResolution * currentSplineIndex;
                    offset += 4 * (currentSplinePoint - 1);

                                        int t1 = offset + 0;
                                        int t2 = offset + 2;
                                        int t3 = offset + 3;
                                        int t4 = offset + 3;
                                        int t5 = offset + 1;
                                        int t6 = offset + 0;

                                        var faceNormal = Vector3.Cross(p3 - p1, p2 - p1).normalized;

                                        normalList.AddRange(new List<Vector3> { faceNormal, faceNormal, faceNormal, faceNormal });
                                        vertexList.AddRange(new List<Vector3> { p1, p2, p3, p4 });
                                        triangleList.AddRange(new List<int> { t1, t2, t3, t4, t5, t6 });

                                        if (worldSpaceUV)
                                        {
                                                uvList.AddRange(new List<Vector2> { new Vector2(p1.x, p1.z), new Vector2(p2.x, p2.z), new Vector2(p3.x, p3.z), new Vector2(p4.x, p4.z) });
                                        }
                                        else
                                        {
                                                var distance = Vector3.Distance(p1, p3) / 4f;
                                                var uvDistance = uvOffset + distance;

                                                uvList.AddRange(new List<Vector2> { new Vector2(uvOffset, 0), new Vector2(uvOffset, 1) , new Vector2(uvDistance, 0) , new Vector2(uvDistance, 1) });

                                                uvOffset += distance;
                                        }
                                }
                        }

            mesh.name = "Road";

            mesh.SetVertices(vertexList);
            mesh.SetTriangles(triangleList, 0);
                        mesh.SetNormals(normalList);
                        mesh.SetUVs(0, uvList);

            meshFilter.mesh = mesh;
        }

                private void GetVerts()
                {
                        vertPoints01 = new List<Vector3>();
                        vertPoints02 = new List<Vector3>();

                        Vector3 point1;
                        Vector3 point2;

                        var step = 1f / roadResolution;

                        for (int j = 0; j < splineContainer.Splines.Count; j++)
                        {
                                for (int i = 0; i < roadResolution; i++)
                                {
                                        var time = step * i;

                                        SampleSplineWidth(time, j, out point1, out point2);
                                        vertPoints01.Add(point1);
                                        vertPoints02.Add(point2);
                                }

                                SampleSplineWidth(1f, j, out point1, out point2);
                                vertPoints01.Add(point1);
                                vertPoints02.Add(point2);
                        }
                }

                private void SampleSplineWidth(float time, int splineIndex, out Vector3 point1, out Vector3 point2)
                {
                        splineContainer.Evaluate(splineIndex, time, out float3 position, out float3 forward, out float3 upVector);

                        float3 right = Vector3.Cross(forward, upVector).normalized;

                        point1 = position + (right * roadWidth);
                        point2 = position - (right * roadWidth);
                }
        }
}