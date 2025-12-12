using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;

public class SectionCreator : MonoBehaviour
{

    public GameObject parent;
    public GameObject terrainModel;
    public Vector3 spawnOffset;
    public int numberOfInitialSections = 4;

    void Start()
    {
        for (int i = 0; i < numberOfInitialSections; i++){
            spawnOffset = new Vector3(494, 0, 350 + (i*700));
            GameObject clone = Instantiate(terrainModel, spawnOffset, Quaternion.identity, parent.transform);
            SectionDestroyer.terrainQueue.Enqueue(clone);    
        }
        spawnOffset = new Vector3(494, 0, ((numberOfInitialSections - 1) * 700) + 370);
    }

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
