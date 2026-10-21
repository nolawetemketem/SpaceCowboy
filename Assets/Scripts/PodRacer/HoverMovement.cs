using System;
using System.Collections;
using UnityEngine;

// Handles hover-based movement with strafing and sway for a vehicle or character.
// Applies physics-based hovering, strafing, tilt during movement, and auto-correction of upright orientation.
public class HoverMovement : MonoBehaviour
{
    [Header("Hover Settings")]
    public float hoverDistance = 35f;
    public float hoverForce = 50f;
    public float correctionForce = 25f;

    // public float hoverDamping = 5f;
    public float sineOscillationScale = 2f;
    public float sineSpeed = 1f;
    double sineX = 0.0;



    [Header("Fighter Movement")]
    public float acceleration = 110f;
    public float deceleration = 70f;
    public float turnSpeed = 35f;
    public float bankForce = 10f;


    // [Header("Camera Controls")]
    // public Transform cam;
    // public float radius = 20f, theta = 180f, phi = 70f, camSensitivity = 2f, camMoveSpeed = 5f, camLookSpeed = 1f;
    // public bool invertVerticalLook;


    [Header("Jump/Boost")]
    public bool canJump = true;
    public bool  canBoost = true;
    public float jumpForce = 25f, jumpCooldownTime = 5f, boostForce = 50f, boostCooldownTime = 5f;


    [Header("Tilt Settings")]
    public float maxTilt = 25f;
    public float tiltSpeed = 5f;
    public float uprightSpring = 4f;
    public float uprightDamping = 2f;

   // Internal state   
    public InputSystem_Actions controls;
    public Rigidbody rb; // Rigidbody component for physics
    private float targetTilt = 0f, tiltMomentum = 0f, strafeDrag = 2f; // Used for smooth damp calculation of tilt




    private void Awake()
    {
        controls = new InputSystem_Actions();
        // controls.FighterJet.ResetCamera.performed += _ => ResetCamera();
        controls.FighterJet.Jump.performed += _ => Jump();
        controls.FighterJet.Boost.performed += _ => Boost();
    }
    

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // rb.linearDamping = drag; // Apply drag to smooth movement
    }

  
    void Update()
    {
        Movement(controls.FighterJet.Move.ReadValue<Vector2>());     // Handle strafing movement with sway
        // CameraControls(controls.FighterJet.Look.ReadValue<Vector2>());
        // ControlTilt(controls.FighterJet.Move.ReadValue<Vector2>());       // Tilt object when strafing and auto-correct tilt when no input
    }

    void FixedUpdate()
    {
        Hover();
        CorrectHoverBalance();
    }

    void Movement(Vector2 direction)     // Applies strafing movement along X/Z axes with sway oscillation.
    {

        if (direction.y != 0 ) // Accel/Decel
        {
            rb.AddForce(120 * direction.y * ((direction.y > 0)? acceleration : deceleration) * Time.deltaTime * transform.forward, ForceMode.Acceleration);
        }

        if (direction.x != 0) // Turning
        {
            rb.AddTorque(120 * direction.x * turnSpeed * Time.deltaTime * transform.up);
            rb.AddTorque(120 * direction.x * -bankForce * Time.deltaTime * transform.forward);
        }

    }


    void Hover()  // Applies hover force based on distance from the ground using raycast.
    {
        Ray downRay = new Ray(transform.position, Vector3.down);

        // Calculate sine force (oscillation)
        if (sineX >= 2.0 * Math.PI)
        {
            sineX -= 2.0 * Math.PI;
        } 
        else
        {
            sineX += Time.fixedDeltaTime * sineSpeed;
        }

        float sineForce = Mathf.Sin((float) sineX) * sineOscillationScale;

        if (Physics.Raycast(downRay, out RaycastHit hit, hoverDistance + sineForce))
        {
            float proportionalHeight = 1f - (hit.distance / (hoverDistance + sineForce));         // Calculate proportional height (0 at max hover distance, 1 at contact)
            float upwardForce = proportionalHeight * hoverForce;                    // Spring-like upward force
            rb.AddForce(Vector3.up * upwardForce, ForceMode.Acceleration);  // Apply combined upward force
        }
    }


    void CorrectHoverBalance()  // Applies hover force based on distance from the ground using raycast.
    {
        float angle = Vector3.Angle(transform.up, Vector3.up); // Local Up To World Up
        if (angle > 1) {
            rb.AddTorque(correctionForce * Vector3.Cross(transform.up, Vector3.up));
        }
    }


    private void Jump()
    {
        if(canJump)
        {
            rb.AddForce(100 * jumpForce * Vector3.up);
            StartCoroutine(JumpCooldown());
        }
    }

    IEnumerator JumpCooldown()
    {
        canJump = false;
        yield return new WaitForSeconds(jumpCooldownTime);       
        canJump = true;
    }


    public void Boost()
    {
        if(canBoost)
        {
            rb.AddForce(100 * boostForce * transform.forward);
            StartCoroutine(BoostCooldown());
        }
    }

    IEnumerator BoostCooldown()
    {
        canBoost = false;
        yield return new WaitForSeconds(boostCooldownTime);       
        canBoost = true;
    }


    private void OnEnable()
    {
        controls.Enable();    

        // Debug.Log("Controls enabled: " + controls.FighterJet.enabled);
        // Debug.Log("Move action enabled: " + controls.FighterJet.Move.enabled); 
    }

    public void OnDisable()
    {
        controls.Disable();    
    }


    // private void CameraControls( Vector2 camDelta)
    // {
      
    //     theta += camDelta.x * camSensitivity;
    //     phi += (invertVerticalLook ? -1 : 1) * camDelta.y * camSensitivity;

    //     if (theta > 360)
    //     {
    //         theta -= 360;
    //     } else if (theta <= -360)
    //     {
    //         theta += 360;
    //     }

    //     phi = Mathf.Clamp(phi, 1f, 100f);

    //     Vector3 horizRight = new Vector3(transform.right.x, 0f, transform.right.z).normalized;
    //     Vector3 horizForward = new Vector3(transform.forward.x, 0f,transform.forward.z).normalized;

    //     // horizontal tracking
    //     Vector3 displacement = 
    //     (radius * Mathf.Sin(phi * Mathf.Deg2Rad) * Mathf.Sin(theta * Mathf.Deg2Rad) * horizRight) + 
    //     (radius * Mathf.Cos(phi * Mathf.Deg2Rad) * Vector3.up) + 
    //     (radius * Mathf.Sin(phi * Mathf.Deg2Rad) * Mathf.Cos(theta * Mathf.Deg2Rad) * horizForward);

    //     Vector3 intendedPos = transform.position + displacement;

    //     cam.position = Vector3.Lerp(cam.position, intendedPos, camMoveSpeed * Time.deltaTime);
    //     cam.LookAt(transform.position);
    // }

    // private void ResetCamera()
    // {
    //     theta = 180;
    //     phi = 70;
    // }



    void ControlTilt(Vector2 direction)    // Controlls all Tilts visual effects 
    {
        if (direction.x < 0)
        {
            targetTilt = maxTilt;
        } else if (direction.x > 0) {
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