using UnityEngine;
using System.Collections;
using System.Collections.Generic;


// Moves a section (e.g., terrain or level segment) continuously along the negative Z-axis.
// Provides methods to stop movement or increase speed dynamically.


public class MoveSection : MonoBehaviour
{
    public static float metersPerSecond = 90; // the shared speed of  all sections

    void Start(){
        metersPerSecond = 90;
    }

    void Update()       // moves the section backward along Z-axis based on current speed.
    {
        transform.position -= new Vector3(0, 0, metersPerSecond) * Time.deltaTime;
    }


    public void Stop()  // Stops movement of all sections by setting speed to zero.
    {
        metersPerSecond = 0f;
    }


    public static void IncreaseSpeed() // Increases the movement speed of all sections by a fixed amount.
    {
        metersPerSecond += 5f;
    }
}
