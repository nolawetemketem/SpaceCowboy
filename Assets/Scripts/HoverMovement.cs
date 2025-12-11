using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class HoverMovement : MonoBehaviour
{
    public float hoverDistance, hoverForce, correctionForce;
    public float bankForce, turnSpeed, accel, decel;
    Rigidbody rb;
    float speed = 0;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        Movement();
    }
    void FixedUpdate()
    {
        Hover();
        RotationCorrection();
    }

    void Movement()
    {
        Vector3 horizonForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        //acceleration / deceleration
        if (Keyboard.current.wKey.isPressed)
        {
            rb.AddForce(accel * Time.deltaTime * horizonForward);
        }
        else if (Keyboard.current.sKey.isPressed)
        {
            rb.AddForce(-decel * Time.deltaTime * horizonForward);
        }
        
        //turning
        if (Keyboard.current.aKey.isPressed)
        {
            rb.AddTorque(-turnSpeed * Time.deltaTime * transform.up);
            rb.AddTorque(bankForce * Time.deltaTime * horizonForward);
        }
        else if (Keyboard.current.dKey.isPressed)
        {
            rb.AddTorque(turnSpeed * Time.deltaTime * transform.up);
            rb.AddTorque(-bankForce * Time.deltaTime * horizonForward);
        }
    }

    void Hover()
    {
        Ray downRay = new Ray(transform.position, Vector3.down);
        if (Physics.Raycast(downRay, out RaycastHit hit, hoverDistance))
        {
            rb.AddForce(hoverForce * Vector3.up);
        }
    } 
    void RotationCorrection()
    {
        if (Vector3.Angle(transform.up, Vector3.up) > 5) {
            rb.AddTorque(correctionForce * Vector3.Cross(transform.up.normalized, Vector3.up));
        }
    }
    
    public void Boost()
    {
        
    }

    public void Jump()
    {
        
    }
}
