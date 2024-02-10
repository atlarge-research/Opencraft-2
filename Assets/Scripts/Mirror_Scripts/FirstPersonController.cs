using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using Unity.VisualScripting;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : NetworkBehaviour
{
    [Header("Camera")]
    public Transform playerRoot;
    public Transform playerCam;

    public float cameraSensitivity;
    private float rotX;
    private float rotY;

    [Header("Movement")] 
    public CharacterController controller;
    public float speed;
    public float jumpHeight;
    public float gravity;
    public Transform feet;
    public bool isGrounded;
    private Vector3 velocity;
    
    //Input System
    [Header("Input")] 
    public InputAction move;
    public InputAction jump;
    public InputAction mouseX;
    public InputAction mouseY;

    void OnEnable()
    {
        move.Enable();
        jump.Enable();
        mouseX.Enable();
        mouseY.Enable();
    }

    void OnDisable()
    {
        move.Disable();
        jump.Disable();
        mouseX.Dispose();
        mouseY.Disable();
    }
    
    

    // Start is called before the first frame update
    void Start()
    {
        if (!isOwned)
        {
            return;
        }
        
        Cursor.lockState = CursorLockMode.Locked;
        
        controller = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!isOwned)
        {
            playerCam.GetComponent<Camera>().enabled = false;
            return;
        }
        
        controller.Move(velocity * Time.deltaTime);
        
        // Camera movement
        Vector2 mouseInput = new Vector2(mouseX.ReadValue<float>() * cameraSensitivity, mouseY.ReadValue<float>() * cameraSensitivity);
        rotX -= mouseInput.y;
        rotX = Mathf.Clamp(rotX, -90, 90);
        rotY += mouseInput.x;
        
        playerRoot.rotation = Quaternion.Euler(0f, rotY, 0f);
        playerCam.localRotation = Quaternion.Euler(rotX,0f,0f);
        
        // Player movement
        Vector2 moveInput = move.ReadValue<Vector2>();

        Vector3 moveVelocity = playerRoot.forward * moveInput.y + playerRoot.right * moveInput.x;

        controller.Move(moveVelocity * (speed * Time.deltaTime));

        isGrounded = Physics.Raycast(feet.position, feet.TransformDirection(Vector3.down), 0.15f);
        
        if (isGrounded == true)
        {
            velocity = new Vector3(0f, -3f, 0f);
        }
        else
        {
            velocity -= Vector3.up * (gravity * Time.deltaTime);
        }
        
        jump.performed += ctx => Jump();
    }

    void Jump()
    {
        if (isGrounded == true)
        {
            velocity.y = Mathf.Sqrt(2f * jumpHeight * gravity);    
        }
    }
}