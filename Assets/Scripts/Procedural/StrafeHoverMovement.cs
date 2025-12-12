using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class StrafeHoverMovement : MonoBehaviour
{

    [Header("Hover")]
    public float hoverDistance = 35f;
    public float hoverForce = 35f;
    public float hoverDamping = 5f;
    public float swayAmount = 15f;
    public float swaySpeed = 15f;

    [Header("Strafe Movement")]
    public float strafeAcceleration = 100f;
    public float strafeDrag = 2f;

    [Header("Bank Rotation")]
    public float maxTilt = 25f;
    public float tiltSpeed = 5f;         
    public float uprightSpring = 4f;     
    public float uprightDamping = 2f;    
  


    float targetTilt = 0f;   // desired tilt angle
    float tiltMomentum = 0f; 
    bool pressingLeft = false;
    bool pressingRight = false;

    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; 
        rb.linearDamping = strafeDrag;
    }

    void Update()
    {
        pressingLeft = Keyboard.current.aKey.isPressed;
        pressingRight = Keyboard.current.dKey.isPressed;
        TiltWithStrafe();
        TiltAutoCorrect();
        StrafeWithSwayMovement();

    }

    void FixedUpdate()
    {
        Hover();
    }


    void StrafeWithSwayMovement()
    {
        Vector3 strafeDirection = Vector3.zero;

        if (pressingRight)     // Move Right 
        {
            strafeDirection = strafeDirection += transform.right;
        }
        else if (pressingLeft)     // Move Left 
        {
           strafeDirection = strafeDirection -= transform.right; 
        }

        float swayForce = Mathf.Sin(Time.deltaTime * swaySpeed) * swayAmount; // Calculate direction and magnitude of sway  
        Vector3 totalForce = (strafeDirection.normalized * strafeAcceleration) + (new Vector3(1,0,0) * swayForce);
        rb.AddForce(totalForce, ForceMode.Acceleration);
    }



    void Hover()
    {
        Ray downRay = new Ray(transform.position, Vector3.down);

        if (Physics.Raycast(downRay, out RaycastHit hit, hoverDistance))
        {
            float proportionalHeight = 1f - (hit.distance / hoverDistance);
            float upwardForce = proportionalHeight * hoverForce;
            float dampingForce = -rb.linearVelocity.y * hoverDamping;
            
            rb.AddForce(Vector3.up * (upwardForce + dampingForce), ForceMode.Acceleration);
        }
    } 


    void TiltWithStrafe()  
    {  
        if (pressingLeft) {
            targetTilt = maxTilt;
        } else if (pressingRight) {
            targetTilt = -maxTilt;
        } else {
            targetTilt = 0f; 
        }

        float currentZRotation = transform.localEulerAngles.z;
       
        if (currentZRotation > 180) currentZRotation -= 360; // keep angles -180..180

        float newZRotation = Mathf.SmoothDamp(currentZRotation, targetTilt, ref tiltMomentum, 1f / tiltSpeed);

        transform.localRotation = Quaternion.Euler(transform.localEulerAngles.x, transform.localEulerAngles.y, newZRotation);
    }


    void TiltAutoCorrect()
    {
        if (pressingLeft || pressingRight) {
            return; 
        }
        Quaternion current = transform.rotation;
        Quaternion target = Quaternion.Euler(0, transform.eulerAngles.y, 0);
        transform.rotation = Quaternion.Slerp(current, target, uprightSpring * Time.deltaTime);
    }
}
