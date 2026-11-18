using UnityEngine;
using System.Collections;
using System.Collections.Generic;


// Moves terrain sections continuously along the negative Z-axis,
// creating the visual effect of the player moving forward through the world.
//
// Movement speed increases dynamically as game progresses,
// escalating difficulty by making sections move faster (simulating increased player speed).

public class MoveSection : MonoBehaviour
{
    public static float metersPerSecond = 80;    // Static speed variable shared by all terrain sections.


    public static void StartOnce()
    {
        metersPerSecond = 80;
    }

    // Moves this section backward along Z-axis at current speed.
    //  The negative Z movement creates visual illusion of forward camera movement (parallax effect).
    void Update()
    {
        transform.position -= new Vector3(0, 0, metersPerSecond) * Time.deltaTime;
    }

   
    // Stops movement of all sections by setting speed to zero.
    // Called when game ends (collision detected).
 
    public void Stop()
    {
        metersPerSecond = 0f;
    }

    // Increases the movement speed of all sections by a fixed amount.
    // Called each time player hits a new section trigger.
    public static void IncreaseSpeed()
    {
        metersPerSecond += 5f;
    }
}