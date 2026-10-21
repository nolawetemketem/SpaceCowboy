using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;

// Manages the cleanup and lifecycle of terrain sections.
// Uses a queue to track sections in order so the oldest sections can be destroyed or stopped as the player progresses.

public class SectionDestroyer : MonoBehaviour
{
    public static Queue<GameObject> terrainQueue = new Queue<GameObject>();

    // Triggered when a terrain section reaches the destroy zone.
    void OnTriggerEnter(Collider other)               
    {

        // Destroy all child objects and remove the oldest terrain section from the queue
        if (other.gameObject.CompareTag("Destroy"))
        {
            foreach (Transform child in other.transform) 
            {
                Destroy(child.gameObject);
            }

            Destroy(other.gameObject);
            terrainQueue.Dequeue();            
        }
    }

    // Stops movement for all active terrain sections. Typically called when the game ends or is paused.
    public static void Stop()    
    {
        while (terrainQueue.Count != 0)
        {
            terrainQueue.Dequeue().GetComponent<MoveSection>().Stop();
        }
    }
}
