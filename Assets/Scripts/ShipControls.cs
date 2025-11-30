using UnityEngine;
using UnityEngine.InputSystem;

public class ShipControls : MonoBehaviour
{
    public int maxSpeed = 30;
    public int acceleration = 10;
    public int pitchRate = 120;
    public int yawRate = 120;
    public int rollRate = 120;
    bool moving = false;
    float speed = 0;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        if (!moving && Keyboard.current.spaceKey.isPressed)
        {
            moving = true;
        }

        if (moving)
        {
            //acceleration / deceleration
            float delta = acceleration * Time.deltaTime;
            if (Keyboard.current.wKey.isPressed && speed <= maxSpeed)
            {
                float diff = maxSpeed - speed;
                speed += delta < diff ? delta : diff;
            }
            else if (Keyboard.current.sKey.isPressed && speed >= maxSpeed * -1)
            {
                float diff = speed - maxSpeed * -1;
                speed -= delta < diff ? delta : diff;
            }

            transform.Translate(speed * Time.deltaTime * Vector3.forward);
        }


        //pitch
        if (Keyboard.current.upArrowKey.isPressed)
        {
            transform.Rotate(pitchRate * Time.deltaTime * -1, 0, 0);
        }
        else if (Keyboard.current.downArrowKey.isPressed)
        {
            transform.Rotate(pitchRate * Time.deltaTime, 0, 0);
        }

        //yaw
        if (Keyboard.current.leftArrowKey.isPressed)
        {
            transform.Rotate(0, yawRate * Time.deltaTime * -1, 0);
        }
        else if (Keyboard.current.rightArrowKey.isPressed)
        {
            transform.Rotate(0, yawRate * Time.deltaTime, 0, 0);
        }

        //roll
        if (Keyboard.current.dKey.isPressed)
        {
            transform.Rotate(0, 0, rollRate * Time.deltaTime * -1);
        }
        else if (Keyboard.current.aKey.isPressed)
        {
            transform.Rotate(0, 0, rollRate * Time.deltaTime);
        }
    }
}
