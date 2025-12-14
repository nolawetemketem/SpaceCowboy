using UnityEngine;
using UnityEngine.InputSystem;


/// Controls the rotation animations of a fighter ship's wings and engines based on keyboard input.
/// Engines rotate forward/backward with the S key.
/// Wings tilt left/right with the A and D keys, and smoothly return to neutral when keys are released.

public class FighterAnimations : MonoBehaviour
{
    [Header("Engine and Wing GameObjects")]
    public GameObject TopEngine;
    public GameObject BottomEngine;
    public GameObject WingRight;
    public GameObject WingLeft;
    public float rate = 120;

    private float maxRotation = 15f, minRotation = 0f;  
    // Current rotation state for wings and engines
    private float leftWingRotation = 0f, rightWingRotation = 0f;
    private float topEngineRotation = 0f, bottonEngineRotation = 0f;

    void Update()
    {
        Engine(); 
        Wings();  
    }


    private void Engine() // Rotates engines forward when 'S' is pressed, and back to neutral when released.
    {
        if (Keyboard.current.sKey.isPressed)
        {
            if (topEngineRotation < maxRotation)
            { 
                float r = rate * Time.deltaTime; 
                TopEngine.transform.Rotate(0, r, 0);   
                topEngineRotation += r;
            }
            if (bottonEngineRotation < maxRotation)
            {
                float r = rate * Time.deltaTime;
                BottomEngine.transform.Rotate(0, r, 0);
                bottonEngineRotation += r;
            }
        } 
        else if (topEngineRotation > minRotation)  // Return engines to neutral rotation when key is released
        {  
            float r = rate * Time.deltaTime;
            TopEngine.transform.Rotate(0, -r, 0);
            topEngineRotation -= r;
            BottomEngine.transform.Rotate(0, -r, 0);
            bottonEngineRotation -= r;
        }
    }


    private void Wings()   // Rotates wings when 'A' or 'D' keys are pressed and returns them to neutral when released.
    {
        if (Keyboard.current.aKey.isPressed)
        {
            if (leftWingRotation < maxRotation)
            {
                float r = rate * Time.deltaTime;
                WingLeft.transform.Rotate(0, r, r); 
                leftWingRotation += r;
            } 
        } 
        else if (leftWingRotation > minRotation) // Smoothly return left wing to neutral
        {   
            float r = rate * Time.deltaTime;
            WingLeft.transform.Rotate(0, -r, -r);
            leftWingRotation -= r;
        }

        if (Keyboard.current.dKey.isPressed)
        {
            if (rightWingRotation < maxRotation)
            {
                float r = rate * Time.deltaTime;
                WingRight.transform.Rotate(0, r, -r); 
                rightWingRotation += r;
            } 
        }   
        else if (rightWingRotation > minRotation) // Smoothly return right wing to neutral
        {   
            float r = rate * Time.deltaTime;
            WingRight.transform.Rotate(0, -r, r);
            rightWingRotation -= r;
        }
    }
}
