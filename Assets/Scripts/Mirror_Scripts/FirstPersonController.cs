using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using Unity.Logging;
using Unity.VisualScripting;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : NetworkBehaviour
{
    [Header("Camera")] public Transform playerRoot;
    public Transform playerCam;

    public float cameraSensitivity;
    private float rotX;
    private float rotY;

    [Header("Movement")] public CharacterController controller;
    public float speed;
    public float jumpHeight;
    public float gravity;
    public Transform feet;
    public bool isGrounded;
    private Vector3 velocity;

    //Input System
    [Header("Input")] public InputAction move;
    public InputAction jump;
    public InputAction mouseX;
    public InputAction mouseY;

    //Block
    public GameObject blockPrefab;
    public float maxPlaceDistance = 10f;
    public float maxRemoveDistance = 10f;

    //Highlighting
    public Material highlightMaterial;
    private GameObject highlightedBlock;

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
        if (!isLocalPlayer)
        {
            playerCam.GetComponent<Camera>().enabled = false;
            return;
        }

        HighlightBlock();

        // PLACING BLOCKS

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            CmdPlaceBlock(playerCam.position, playerCam.forward);
        }
        else if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            CmdRemoveBlock(playerCam.position, playerCam.forward);
        }

        controller.Move(velocity * Time.deltaTime);

        // Camera movement
        Vector2 mouseInput = new Vector2(mouseX.ReadValue<float>() * cameraSensitivity,
            mouseY.ReadValue<float>() * cameraSensitivity);
        rotX -= mouseInput.y;
        rotX = Mathf.Clamp(rotX, -90, 90);
        rotY += mouseInput.x;

        playerRoot.rotation = Quaternion.Euler(0f, rotY, 0f);
        playerCam.localRotation = Quaternion.Euler(rotX, 0f, 0f);

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

    // Placing and removing blocks
    [Command]
    void CmdPlaceBlock(Vector3 cameraPosition, Vector3 cameraForward)
    {
        RaycastHit hit;
        if (Physics.Raycast(cameraPosition, cameraForward, out hit, maxPlaceDistance))
        {
            Vector3 gridPosition = RoundToNearestGrid(hit.point + hit.normal * 0.5f); // Offset by half a block size

            if (!IsBlockAtPosition(gridPosition))
            {
                GameObject newBlock = Instantiate(blockPrefab, gridPosition, Quaternion.identity);
                NetworkServer.Spawn(newBlock);
            }
        }
    }

    Vector3 RoundToNearestGrid(Vector3 position)
    {
        float gridSize = 0.75f; // Adjust this to match your block size
        float x = Mathf.Round(position.x / gridSize) * gridSize;
        float y = Mathf.Round(position.y / gridSize) * gridSize;
        float z = Mathf.Round(position.z / gridSize) * gridSize;
        return new Vector3(x, y, z);
    }

    bool IsBlockAtPosition(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapSphere(position, 0.35f); // Adjust the radius as needed
        foreach (Collider collider in colliders)
        {
            if (collider.CompareTag("Block"))
            {
                return true;
            }
        }

        return false;
    }

    [Command]
    void CmdRemoveBlock(Vector3 cameraPosition, Vector3 cameraForward)
    {
        RaycastHit hit;
        if (Physics.Raycast(cameraPosition, cameraForward, out hit, maxRemoveDistance))
        {
            if (hit.collider.CompareTag("Block"))
            {
                Destroy(hit.collider.gameObject);
                NetworkServer.Destroy(hit.collider.gameObject);
            }
        }
    }

    // Highlighting scripts visible only for the local player
    void HighlightBlock()
    {
        RaycastHit hit;
        if (Physics.Raycast(playerCam.position, playerCam.forward, out hit, maxPlaceDistance))
        {
            GameObject hitBlock = hit.collider.gameObject;
            if (hitBlock.CompareTag("Block"))
            {
                Renderer blockRenderer = hitBlock.GetComponent<Renderer>();
                Material[] materials = blockRenderer.materials;

                if (materials.Length < 2)
                {
                    if (highlightedBlock != null && highlightedBlock != hitBlock)
                    {
                        RemoveHighlight(highlightedBlock);
                    }

                    if (highlightedBlock != hitBlock)
                    {
                        ApplyHighlight(hitBlock);
                        highlightedBlock = hitBlock;
                    }
                }
            }
        }
        else
        {
            if (highlightedBlock != null)
            {
                RemoveHighlight(highlightedBlock);
                highlightedBlock = null;
            }
        }
    }

    void ApplyHighlight(GameObject block)
    {
        Renderer blockRenderer = block.GetComponent<Renderer>();
        Material[] materials = blockRenderer.materials;
        Material[] newMaterials = new Material[materials.Length + 1];
        for (int i = 0; i < materials.Length; i++)
        {
            newMaterials[i] = materials[i];
        }

        newMaterials[materials.Length] = highlightMaterial;
        blockRenderer.materials = newMaterials;
    }

    void RemoveHighlight(GameObject block)
    {
        Renderer blockRenderer = block.GetComponent<Renderer>();
        Material[] materials = blockRenderer.materials;

        if (materials.Length > 1)
        {
            Material[] newMaterials = new Material[materials.Length - 1];
            for (int i = 0; i < newMaterials.Length; i++)
            {
                newMaterials[i] = materials[i];
            }

            blockRenderer.materials = newMaterials;
        }
    }
}