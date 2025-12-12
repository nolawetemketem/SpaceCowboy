using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;

public class SectionDestroyer : MonoBehaviour
{

    public static Queue<GameObject> terrainQueue = new Queue<GameObject>();

    
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Destroy"))
        {
            foreach(Transform child in other.transform) 
            {
                Destroy(child.gameObject);
            }
            Destroy(other.gameObject);
            terrainQueue.Dequeue();
        }
    }

    public static void Stop() {
        while(terrainQueue.Count != 0) {
            terrainQueue.Dequeue().GetComponent<MoveSection>().Stop();
        }
    }
}
