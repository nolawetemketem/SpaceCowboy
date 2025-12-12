using UnityEngine;
using System.Collections.Generic;

public class RandomObjectInSection : MonoBehaviour
{

    public GameObject assignedParent = null;
    public Terrain terrain = null;
    public List<GameObject> objects = null;

    public int objectsToGenerate = 10;

    public float minScale = 1;
    public float maxScale = 3;

    public float minYRotation = 0f;
    public float maxYRotation = 360f;

    public int layerMaskToDetect = 0;        // used to ignore collisions like terrain in our case
    public float collisionRadius = 10f;      // how close objects can be
    public int maxAttemptsPerObject = 20;   // prevent infinite loops


    float xMin = 0, xMax = 0, zMin = 0, zMax = 0;
   

    void Start()
    {

        xMin = terrain.transform.position.x + 60;
        xMax = xMin + terrain.terrainData.size.x -110;

        zMin = terrain.transform.position.z;
        zMax = zMin + terrain.terrainData.size.z;

        System.Random rand = new System.Random();

        if (objects == null || assignedParent == null || objects.Count == 0)
        {
            Debug.Log("Uninitialized Objects. Check Inspector");
            return;
        }

        for (int i = 0; i < objectsToGenerate; i++)
        {
            bool placed = false;

            for (int attempts = 0; !placed && attempts < maxAttemptsPerObject; attempts++)
            {
               
                float newX = (float)rand.NextDouble() * (xMax - xMin) + xMin;
                float newZ = (float)rand.NextDouble() * (zMax - zMin) + zMin;
                float newScale = (float)rand.NextDouble() * (maxScale - minScale) + minScale;
                float newYRotation = (float)rand.NextDouble() * (maxYRotation - minYRotation) + minYRotation;

                Vector3 position = new Vector3(newX, 0, newZ);
                float terrainY = Terrain.activeTerrain.SampleHeight(position) + Terrain.activeTerrain.GetPosition().y;
                position.y = terrainY;

                Collider[] hits = Physics.OverlapSphere(position, collisionRadius, layerMaskToDetect);

                if (hits.Length == 0)
                {
                    int randomIndex = rand.Next(objects.Count);
                    Quaternion newRotation = Quaternion.Euler(0, newYRotation, 0);

                    GameObject obj = Instantiate(objects[randomIndex], position, newRotation, assignedParent.transform);
                    obj.transform.localScale = Vector3.one * newScale;  

               
                    MeshCollider mc = obj.AddComponent<MeshCollider>();
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
