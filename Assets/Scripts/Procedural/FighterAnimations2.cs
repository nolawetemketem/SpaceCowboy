using UnityEngine;
using UnityEngine.InputSystem;


//  Controls responsive rotation animations of a fighter ship's wings and engines.
//
//  Wings rotate with A (left) and D (right) keys, tilting the fighter 
//  Engines rotate with S key, indicating acceleration/thrust
//  Smooth damping returns animations to neutral when keys are released

public class FighterAnimations2 : MonoBehaviour
{
    public GameObject TopEngine;
    public GameObject BottomEngine;
    public GameObject WingRight;
    public GameObject WingLeft;
 
    public float rate = 120;   // Controls how fast wings/engines rotate. 

    private float maxRotation = 15f, minRotation = 0f;  
    
    // Current rotation state for wings and engines - used to direct animation 
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
                topEngineRotation += r;                 // Increment per frame
            }
            if (bottonEngineRotation < maxRotation)
            {
                float r = rate * Time.deltaTime;
                BottomEngine.transform.Rotate(0, r, 0);
                bottonEngineRotation += r;
            }
        } 
        else if (topEngineRotation > minRotation)
        {  
            // Gradually get back to 0 rotation (for natural effect)
            float r = rate * Time.deltaTime;
            TopEngine.transform.Rotate(0, -r, 0);
            topEngineRotation -= r;
            BottomEngine.transform.Rotate(0, -r, 0);
            bottonEngineRotation -= r;
        }
    }

    // Rotates wings when 'A' or 'D' keys are pressed and returns them to neutral when released.    
    private void Wings()   
    {
        if (Keyboard.current.aKey.isPressed)
        {
            if (leftWingRotation < maxRotation)
            {
                float r = rate * Time.deltaTime;
                WingLeft.transform.Rotate(0, r, r);  // Rotate on both Y and Z for realistic banking
                leftWingRotation += r;
            } 
        } 
        else if (leftWingRotation > minRotation) // Smoothly return left wing to neutral position
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
                WingRight.transform.Rotate(0, r, -r);  // Opposite Z rotation creates balanced bank
                rightWingRotation += r;
            } 
        }   
        else if (rightWingRotation > minRotation) // Smoothly return right wing to neutral position
        {   
            float r = rate * Time.deltaTime;
            WingRight.transform.Rotate(0, -r, r);
            rightWingRotation -= r;
        }
    }
}