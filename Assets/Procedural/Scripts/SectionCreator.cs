using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


// Responsible for spawning terrain sections at runtime.
// Creates an infinite endless-runner track by continuously instantiating new sections as the player advances.

//  Each time the player hits a "Create" trigger, a new section spawns ahead
// and the game difficulty increases (speed escalation via MoveSection.IncreaseSpeed()).

public class SectionCreator : MonoBehaviour
{
    [Header("Section References")]
    public GameObject parent;
    public GameObject terrainModel;

    [Header("Spawn Settings")]
    public Vector3 spawnOffset;

    public int numberOfInitialSections = 4;


   // Spawn initial sections spaced evenly along the Z-axis
    // Each section is positioned apart by section length

    void Start()
    {
        for (int i = 0; i < numberOfInitialSections; i++)
        {
            spawnOffset = new Vector3(494, 0, 350 + (i * 700));      

            GameObject clone = Instantiate(terrainModel, spawnOffset, Quaternion.identity, parent.transform);
            SectionDestroyer.terrainQueue.Enqueue(clone);
        }
        // Prepare spawn offset for future sections (middle of last section)
        spawnOffset = new Vector3(494, 0, ((numberOfInitialSections - 1) * 700) + 370);
    }


    // Detects when a section creation trigger is hit and spawns a new terrain section ahead of the current one.
    // Also increases movement speed to escalate difficulty
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Create"))      
        {
            GameObject clone = Instantiate(terrainModel, other.transform.position + spawnOffset, Quaternion.identity, parent.transform);
            SectionDestroyer.terrainQueue.Enqueue(clone);  
            MoveSection.IncreaseSpeed(); // increase speed by 5 m/s 
        }
    }
}