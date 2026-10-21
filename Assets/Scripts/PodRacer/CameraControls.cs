using UnityEngine;

public class CameraControls : MonoBehaviour
{

    public Transform cam;
    public InputSystem_Actions controls;
    public float radius = 25f, theta = 180f, phi = 70f, camSensitivity = 2f, camMoveSpeed = 5f, camLookSpeed = 1f;
    public bool invertVerticalLook;


    private void Awake()
    {
        controls = new InputSystem_Actions();
        controls.FighterJet.ResetCamera.performed += _ => ResetPosition();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }
    void Update()
    {

        TrackPlayer(controls.FighterJet.Look.ReadValue<Vector2>());
        // ControlTilt(controls.FighterJet.Move.ReadValue<Vector2>());       // Tilt object when strafing and auto-correct tilt when no input
    }


    private void TrackPlayer( Vector2 camDelta)
    {
      
        theta += camDelta.x * camSensitivity;
        phi += (invertVerticalLook ? -1 : 1) * camDelta.y * camSensitivity;

        if (theta > 360)
        {
            theta -= 360;
        } else if (theta <= -360)
        {
            theta += 360;
        }

        phi = Mathf.Clamp(phi, 1f, 100f);

        Vector3 horizRight = new Vector3(transform.right.x, 0f, transform.right.z).normalized;
        Vector3 horizForward = new Vector3(transform.forward.x, 0f,transform.forward.z).normalized;

        // horizontal tracking
        Vector3 displacement = 
        (radius * Mathf.Sin(phi * Mathf.Deg2Rad) * Mathf.Sin(theta * Mathf.Deg2Rad) * horizRight) + 
        (radius * Mathf.Cos(phi * Mathf.Deg2Rad) * Vector3.up) + 
        (radius * Mathf.Sin(phi * Mathf.Deg2Rad) * Mathf.Cos(theta * Mathf.Deg2Rad) * horizForward);

        Vector3 intendedPos = transform.position + displacement;

        cam.position = Vector3.Lerp(cam.position, intendedPos, camMoveSpeed * Time.deltaTime);
        cam.LookAt(transform.position);
    }


    private void ResetPosition()
    {
        theta = 180;
        phi = 70;
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
}
