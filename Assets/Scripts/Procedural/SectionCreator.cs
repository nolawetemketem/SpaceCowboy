using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;


// Responsible for spawning terrain sections at runtime. 
// Initializes a set of starting sections and spawns new oneswhen the player reaches a trigger, creating an endless track effect.

public class SectionCreator : MonoBehaviour
{
    [Header("Section References")]
    public GameObject parent;
    public GameObject terrainModel;

    [Header("Spawn Settings")]
    public Vector3 spawnOffset;

    public int numberOfInitialSections = 4;


    void Start()  // Spawns the initial set of terrain sections at startup and queues them for later cleanup.
    {
        for (int i = 0; i < numberOfInitialSections; i++)
        {
            spawnOffset = new Vector3(494, 0, 350 + (i * 700));      

            GameObject clone = Instantiate(terrainModel, spawnOffset, Quaternion.identity, parent.transform);
            SectionDestroyer.terrainQueue.Enqueue(clone);
        }
        spawnOffset = new Vector3(494,0, ((numberOfInitialSections - 1) * 700) + 370); // Set offset for future section spawns
    }

    // Detects when a section creation trigger is hit and spawns a new terrain section ahead of the current one.
    // Also increases movement speed to escalate difficulty.
    
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Create"))      
        {
            GameObject clone = Instantiate(terrainModel, other.transform.position + spawnOffset, Quaternion.identity, parent.transform);
            SectionDestroyer.terrainQueue.Enqueue(clone);  
            MoveSection.IncreaseSpeed();                   
        }
    }
}
