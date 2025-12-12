using UnityEngine;
using UnityEngine.InputSystem;

public class BodyController : MonoBehaviour
{
    private float maxRotation = 15f;
    private float minRotation = 0f;
    private float leftWingRotation = 0f;
    private float rightWingRotation = 0f;
    private float topEngineRotation = 0f;
    private float bottonEngineRotation = 0f;
    public float rate = 120;
    public GameObject TopEngine;
    public GameObject BottomEngine;
    public GameObject WingRight;
    public GameObject WingLeft;
    
    void Start()
    {
        
    }


    void Update()
    {
        if (Keyboard.current.sKey.isPressed)
        {
            if (topEngineRotation < maxRotation)
            {
                float r = rate * Time.deltaTime;
                TopEngine.transform.Rotate(0,r,0);
                topEngineRotation += r;
            }
            if (bottonEngineRotation < maxRotation)
            {
                float r = rate * Time.deltaTime;
                BottomEngine.transform.Rotate(0,r,0);
                bottonEngineRotation += r;
            }
        }
        else if (topEngineRotation > minRotation)
        {
            float r = rate * Time.deltaTime;
            TopEngine.transform.Rotate(0,-r,0);
            topEngineRotation -= r;
            BottomEngine.transform.Rotate(0,-r,0);
            bottonEngineRotation -= r;
        }
        Turn();
    }
    
    private void Turn()
    {
        // Left wing control
        if (Keyboard.current.aKey.isPressed)
        {
            if (leftWingRotation < maxRotation)
            {
                float r = rate * Time.deltaTime;
                WingLeft.transform.Rotate(0,r,r);
                leftWingRotation += r;
            } 
            // transform.Rotate(0, -rate * Time.deltaTime, 0);
        } 
        else if (leftWingRotation > minRotation)
        {
            float r = rate * Time.deltaTime;
            WingLeft.transform.Rotate(0,-r,-r);
            leftWingRotation -= r;
        }
        // Right wing control
        if (Keyboard.current.dKey.isPressed)
        {
            if (rightWingRotation < maxRotation)
            {
                float r = rate * Time.deltaTime;
                WingRight.transform.Rotate(0,r,-r);
                rightWingRotation += r;
            } 
            // transform.Rotate(0, rate * Time.deltaTime, 0);

        }
        else if (rightWingRotation > minRotation)
        {
            float r = rate * Time.deltaTime;
            WingRight.transform.Rotate(0,-r,r);
            rightWingRotation -= r;
        }
    }
}
