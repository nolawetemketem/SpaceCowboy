using UnityEngine;
using System.Collections.Generic;


// Randomly spawns objects within a specified terrain section, avoiding collisions with existing objects.
// Allows customization of object scale, rotation, and placement limits.

public class RandomObjectInSection : MonoBehaviour
{
    [Header("Parent & Terrain")]
    public GameObject assignedParent = null;
    public Terrain terrain = null;

    [Header("Objects to Spawn")]
    public List<GameObject> objects = null;
    public int objectsToGenerate = 5;

    [Header("Object Transform Settings")]
    public float minScale = 3;
    public float maxScale = 6;
    public float minYRotation = 0f;
    public float maxYRotation = 360f;

    [Header("Collision & Placement")]
    public int layerMaskToDetect = 0;
    public float collisionRadius = 10f;
    public int maxAttemptsPerObject = 10;

    [Header("Terrain Placement Offsets")]
    public float terrainXMinimumOffsett = 60;
    public float terrainXMaximumOffsett = -110;

    private float zMin, zMax, xMin, xMax;   // Internal boundaries for spawning

 
    void Start()  // Initializes placement boundaries, validates inputs, and spawns objects randomly within terrain section.
    {
        xMin = terrain.transform.position.x + terrainXMinimumOffsett;
        xMax = xMin + terrain.terrainData.size.x + terrainXMaximumOffsett;

        zMin = terrain.transform.position.z;
        zMax = zMin + terrain.terrainData.size.z;

        System.Random rand = new System.Random();

        if (objects == null || assignedParent == null || objects.Count == 0)    
        {
            Debug.Log("Uninitialized Objects. Check Inspector");
            return;
        }

        for (int i = 0; i < objectsToGenerate; i++)   // Loop to generate the specified number of objects
        {
            bool placed = false;


            for (int atmpts = 0; !placed && atmpts < maxAttemptsPerObject; atmpts++)    // Attempt to place object multiple times to avoid collisions
            {
                // Randomized X/Z position scale and Y rotation within bounds 
                float newX = (float)rand.NextDouble() * (xMax - xMin) + xMin;
                float newZ = (float)rand.NextDouble() * (zMax - zMin) + zMin;
                float newScale = (float)rand.NextDouble() * (maxScale - minScale) + minScale;
                float newYRotation = (float)rand.NextDouble() * (maxYRotation - minYRotation) + minYRotation;

                // Calculate Y based on terrain height
                Vector3 position = new Vector3(newX, 0, newZ);
                float terrainY = Terrain.activeTerrain.SampleHeight(position) + Terrain.activeTerrain.GetPosition().y;
                position.y = terrainY;

                Collider[] hits = Physics.OverlapSphere(position, collisionRadius, layerMaskToDetect);    // Check for collisions at desired position


                if (hits.Length == 0)
                {
                    int randomIndex = rand.Next(objects.Count);   // selects a prefab from the list
                    Quaternion newRotation = Quaternion.Euler(0, newYRotation, 0);

                    GameObject obj = Instantiate(objects[randomIndex], position, newRotation, assignedParent.transform);
                    obj.transform.localScale = Vector3.one * newScale;

                    MeshCollider mc = obj.AddComponent<MeshCollider>();                         // Add MeshCollider for physics/collision detection
                    mc.sharedMesh = obj.GetComponentInChildren<MeshFilter>().sharedMesh;
                    mc.convex = false;

                    placed = true; 
                }
            }
            if (!placed)
            {
                Debug.LogWarning("Could not place object " + i + " without collision.");
            }
        }
    }
}
