using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// PROCEDURAL RANDOM OBJECT PLACEMENT SYSTEM: Dynamically spawns randomized obstacles within terrain sections.
/// 
/// AREAS DEMONSTRATED:
/// - RANDOM NUMBERS: Core algorithm using System.Random to generate:
///   * Random X/Z positions within terrain boundaries
///   * Random Y-axis rotations (0-360 degrees)
///   * Random scales (between minScale and maxScale)
/// - DESIGN: Implements constraint-based randomization:
///   * Collision detection prevents overlapping objects (sophisticated constraint handling)
///   * Configurable spawn parameters allow designers to tune difficulty per section
///   * Repetition with variation: same algorithm, different randomized results each time
/// - ON-SCREEN DYNAMICS: Instantiated objects create obstacles that interact with player movement
/// - CODING: Demonstrates efficient collision detection using Physics.OverlapSphere,
///   object-oriented design with clear separation between randomization and physics setup
///
/// KEY MECHANIC: For each section, spawns 'objectsToGenerate' obstacles with random properties,
/// ensuring no two objects overlap through collision-avoidance algorithm.
/// </summary>
public class RandomObjectInSection : MonoBehaviour
{
    [Header("Parent & Terrain")]
    public GameObject assignedParent = null;
    public Terrain terrain = null;

    [Header("Objects to Spawn")]
    public List<GameObject> objects = null;
    public int objectsToGenerate = 5;

    [Header("Object Transform Settings")]
    /// <summary>
    /// Min/max scale for random objects. Combined with Random.NextDouble(),
    /// creates variation in obstacle sizes (e.g., small rocks to large boulders).
    /// </summary>
    public float minScale = 3;
    public float maxScale = 6;
    /// <summary>
    /// Random rotation range for Y-axis. Full 0-360 degree rotation ensures
    /// varied visual appearance of obstacles (not all facing same direction).
    /// </summary>
    public float minYRotation = 0f;
    public float maxYRotation = 360f;

    [Header("Collision & Placement")]
    public int layerMaskToDetect = 0;
    /// <summary>
    /// Collision detection radius. Objects must be placed without colliding with
    /// other obstacles in this radius (prevents crowded spawning).
    /// </summary>
    public float collisionRadius = 10f;
    /// <summary>
    /// Maximum attempts to place a single object before giving up.
    /// Limits execution time and prevents infinite loops if section is too crowded.
    /// </summary>
    public int maxAttemptsPerObject = 10;

    [Header("Terrain Placement Offsets")]
    public float terrainXMinimumOffsett = 60;
    public float terrainXMaximumOffsett = -110;

    private float zMin, zMax, xMin, xMax;   // Internal boundaries for spawning

    /// <summary>
    /// Initialization: Calculates terrain boundaries, validates inputs, and spawns objects
    /// with randomized properties while avoiding collisions.
    /// 
    /// DESIGN & RANDOM NUMBERS: Demonstrates sophisticated constraint-based procedural generation.
    /// </summary>
    void Start()
    {
        // Calculate placement boundaries based on terrain size and offsets
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

        /// RANDOM NUMBER GENERATION LOOP: For each object to generate
        for (int i = 0; i < objectsToGenerate; i++)
        {
            bool placed = false;

            /// COLLISION-AVOIDANCE ALGORITHM: Attempt placement multiple times with different random positions
            /// This demonstrates combining randomness with deterministic constraints.
            for (int atmpts = 0; !placed && atmpts < maxAttemptsPerObject; atmpts++)
            {
                // Generate random X/Z position within terrain boundaries
                // Uses System.Random.NextDouble() which returns 0.0 to 1.0, then scales to boundary range
                float newX = (float)rand.NextDouble() * (xMax - xMin) + xMin;
                float newZ = (float)rand.NextDouble() * (zMax - zMin) + zMin;
                /// Random scale variation: uses linear interpolation between min and max
                float newScale = (float)rand.NextDouble() * (maxScale - minScale) + minScale;
                /// Random Y rotation: full 360 degree range for varied visual appearance
                float newYRotation = (float)rand.NextDouble() * (maxYRotation - minYRotation) + minYRotation;

                // Sample terrain height at this position and place object ON TOP of terrain
                Vector3 position = new Vector3(newX, 0, newZ);
                float terrainY = Terrain.activeTerrain.SampleHeight(position) + Terrain.activeTerrain.GetPosition().y;
                position.y = terrainY;

                /// COLLISION DETECTION: Check if this position collides with existing objects
                /// Uses OverlapSphere to detect all colliders in a radius
                Collider[] hits = Physics.OverlapSphere(position, collisionRadius, layerMaskToDetect);

                // Only place object if no collisions detected
                if (hits.Length == 0)
                {
                    /// Random prefab selection: picks a random prefab from the objects list
                    int randomIndex = rand.Next(objects.Count);
                    Quaternion newRotation = Quaternion.Euler(0, newYRotation, 0);

                    /// Instantiate with random position, rotation, and scale
                    GameObject obj = Instantiate(objects[randomIndex], position, newRotation, assignedParent.transform);
                    obj.transform.localScale = Vector3.one * newScale;

                    /// PHYSICS SETUP: Add mesh collider for collision detection
                    MeshCollider mc = obj.AddComponent<MeshCollider>();
                    mc.sharedMesh = obj.GetComponentInChildren<MeshFilter>().sharedMesh;
                    mc.convex = false;  // Non-convex allows more complex shapes

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
