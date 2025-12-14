using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Handles hover-based movement with strafing and sway for a vehicle or character.
// Applies physics-based hovering, strafing, tilt during movement, and auto-correction of upright orientation.
public class StrafeHoverMovement : MonoBehaviour
{
    [Header("Hover Settings")]
    public float hoverDistance = 35f;
    public float hoverForce = 35f;
    public float hoverDamping = 5f;
    public float swayAmount = 20f;
    public float swaySpeed = 5f;

    [Header("Strafe Movement")]
    public float strafeAcceleration = 100f;
    public float strafeDrag = 2f;

    [Header("Tilt Settings")]
    public float maxTilt = 25f;
    public float tiltSpeed = 5f;
    public float uprightSpring = 4f;
    public float uprightDamping = 2f;


    // Internal state
    private Rigidbody rb; // Rigidbody component for physics
    private float targetTilt = 0f, tiltMomentum = 0f; // Used for smooth damp calculation of tilt
    private bool pressingA = false, pressingD = false, pressingS = false, pressingW = false;
    

   
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;      // Prevent physics from automatically rotating the object
        rb.linearDamping = strafeDrag; // Apply drag to smooth movement
    }

  
    void Update()
    {
        // Update input state
        pressingA = Keyboard.current.aKey.isPressed;
        pressingD = Keyboard.current.dKey.isPressed;
        pressingW = Keyboard.current.wKey.isPressed;
        pressingS = Keyboard.current.sKey.isPressed;

        ControlTilt();       // Tilt object when strafing and auto-correct tilt when no input
        StrafeWithSwayMovement();// Handle strafing movement with sway
    }


    void FixedUpdate()
    {
        Hover();
    }


    void StrafeWithSwayMovement()     // Applies strafing movement along X/Z axes with sway oscillation.
    {
        Vector3 strafeDirection = Vector3.zero;

        if (transform.position.z > 5 && pressingS) // Move Forward
        {
            rb.AddForce((strafeDirection - transform.forward).normalized * (strafeAcceleration / 5), ForceMode.Acceleration);
        }
        else if (transform.position.z < 30 && pressingW) // Move Backward
        {
            rb.AddForce((strafeDirection + transform.forward).normalized * (strafeAcceleration / 8), ForceMode.Acceleration);
        }

        // Left/right strafing

        if (pressingD) 
        {
            strafeDirection += transform.right;
        }
        else if (pressingA)
        {
            strafeDirection -= transform.right;
        }

        // Calculate sway force (side-to-side oscillation)
        float swayForce = Mathf.Sin(Time.deltaTime * swaySpeed) * swayAmount;

        // Total force applied: strafe + sway
        Vector3 totalForce = (strafeDirection.normalized * strafeAcceleration) + (new Vector3(1, 0, 0) * swayForce);
        rb.AddForce(totalForce, ForceMode.Acceleration);
    }


    void Hover()  // Applies hover force based on distance from the ground using raycast.
    {
        Ray downRay = new Ray(transform.position, Vector3.down);

        if (Physics.Raycast(downRay, out RaycastHit hit, hoverDistance))
        {
            float proportionalHeight = 1f - (hit.distance / hoverDistance);         // Calculate proportional height (0 at max hover distance, 1 at contact)
            float upwardForce = proportionalHeight * hoverForce;                    // Spring-like upward force
            float dampingForce = -rb.linearVelocity.y * hoverDamping;               // Damping force to reduce oscillation
  
            rb.AddForce(Vector3.up * (upwardForce + dampingForce), ForceMode.Acceleration);  // Apply combined upward force
        }
    }


    void ControlTilt()    // Controlls all Tilts visual effects 
    {
        if (pressingA)
        {
            targetTilt = maxTilt;
        } else if (pressingD) {
            targetTilt = -maxTilt;
        } else {
            Quaternion current = transform.rotation;
            Quaternion target = Quaternion.Euler(0, transform.eulerAngles.y, 0); // upright rotation
            transform.rotation = Quaternion.Slerp(current, target, uprightSpring * Time.deltaTime);
            return;
        }

        float currentZRotation = transform.localEulerAngles.z;
        if (currentZRotation > 180) currentZRotation -= 360; // (-180 to 180 range)

        // Smoothly interpolate towards target tilt
        float newZRotation = Mathf.SmoothDamp(currentZRotation, targetTilt, ref tiltMomentum, 1f / tiltSpeed);

        // Apply tilt while preserving X/Y rotation
        transform.localRotation = Quaternion.Euler(transform.localEulerAngles.x, transform.localEulerAngles.y, newZRotation);
    }
}