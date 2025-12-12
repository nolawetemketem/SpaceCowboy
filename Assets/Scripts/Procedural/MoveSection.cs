using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class MoveSection : MonoBehaviour
{
    public static float metersPerSecond = 100;

    void Update()    // Update is called once per frame
    {
        transform.position -= new Vector3(0, 0, MoveSection.metersPerSecond) * Time.deltaTime;
    }

    public void Stop()
    {
        metersPerSecond = 0;
    }

    public static void IncreaseSpeed()
    {
        metersPerSecond += 5;
    }
}
