using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person player controller with movement, mouse look, and interaction.
/// Uses the new Input System.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundMask;
    
    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 85f;
    [SerializeField] private Transform cameraHolder;
    
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactionMask;
    [SerializeField] private Transform interactionPoint;
    #endregion
    
    #region Properties
    public bool CanMove { get; set; } = true;
    public bool CanLook { get; set; } = true;
    public bool IsGrounded { get; private set; }
    #endregion
    
    #region Private Fields
    private CharacterController controller;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction interactAction;
    
    private Vector3 velocity;
    private float xRotation;
    private Vector2 moveInput;
    private Vector2 lookInput;
    
    // Cached interaction
    private IInteractable currentInteractable;
    private GameObject lastHitObject;
    #endregion
    
    #region Initialization
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        
        // Get input actions from PlayerInput component
        if (playerInput != null)
        {
            moveAction = playerInput.actions["Move"];
            lookAction = playerInput.actions["Look"];
            interactAction = playerInput.actions["Interact"];
        }
        
        // Auto-find camera holder if not assigned
        if (cameraHolder == null)
        {
            cameraHolder = GetComponentInChildren<Camera>()?.transform.parent;
            if (cameraHolder == null)
            {
                cameraHolder = GetComponentInChildren<Camera>()?.transform;
            }
        }
        
        // Auto-find interaction point
        if (interactionPoint == null && cameraHolder != null)
        {
            interactionPoint = cameraHolder.GetComponentInChildren<Camera>()?.transform;
        }
    }
    
    private void Start()
    {
        // Lock cursor for FPS gameplay
        LockCursor(true);
        
        // Subscribe to game state changes
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
    }
    
    private void OnEnable()
    {
        // Subscribe to interact action
        if (interactAction != null)
        {
            interactAction.performed += OnInteract;
        }
    }
    
    private void OnDisable()
    {
        // Unsubscribe from interact action
        if (interactAction != null)
        {
            interactAction.performed -= OnInteract;
        }
    }
    
    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
    }
    #endregion
    
    #region Update
    private void Update()
    {
        // Read input
        ReadInput();
        
        // Ground check
        CheckGround();
        
        // Process movement and look
        if (CanMove)
        {
            ProcessMovement();
        }
        
        if (CanLook)
        {
            ProcessLook();
        }
        
        // Interaction raycast
        ProcessInteractionRaycast();
    }
    
    private void ReadInput()
    {
        // Read move input from Input System
        if (moveAction != null)
        {
            moveInput = moveAction.ReadValue<Vector2>();
        }
        else
        {
            moveInput = Vector2.zero;
        }
        
        // Read look input from Input System
        if (lookAction != null)
        {
            lookInput = lookAction.ReadValue<Vector2>();
        }
        else
        {
            lookInput = Vector2.zero;
        }
    }
    #endregion
    
    #region Movement
    private void CheckGround()
    {
        IsGrounded = controller.isGrounded;
        
        if (IsGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small downward force to keep grounded
        }
    }
    
    private void ProcessMovement()
    {
        // Calculate movement direction relative to player facing
        Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
        
        // Apply movement
        controller.Move(moveDirection * walkSpeed * Time.deltaTime);
        
        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
    #endregion
    
    #region Mouse Look
    private void ProcessLook()
    {
        // Get mouse delta
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;
        
        // Rotate player horizontally
        transform.Rotate(Vector3.up * mouseX);
        
        // Rotate camera vertically (clamped)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);
        
        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
    }
    
    public void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
    #endregion
    
    #region Interaction
    private void ProcessInteractionRaycast()
    {
        Transform rayOrigin = interactionPoint != null ? interactionPoint : cameraHolder;
        if (rayOrigin == null) return;
        
        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        
        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactionMask))
        {
            GameObject hitObject = hit.collider.gameObject;
            
            if (hitObject != lastHitObject)
            {
                // New object - check for interactable
                lastHitObject = hitObject;
                currentInteractable = hitObject.GetComponent<IInteractable>();
                
                if (currentInteractable != null)
                {
                    currentInteractable.OnLookAt();
                }
            }
        }
        else
        {
            // Nothing hit
            if (currentInteractable != null)
            {
                currentInteractable.OnLookAway();
            }
            currentInteractable = null;
            lastHitObject = null;
        }
        
        // Debug ray
        Debug.DrawRay(ray.origin, ray.direction * interactionRange, 
            currentInteractable != null ? Color.green : Color.red);
    }
    
    private void OnInteract(InputAction.CallbackContext context)
    {
        if (!CanMove) return;
        
        if (currentInteractable != null)
        {
            currentInteractable.Interact(gameObject);
        }
    }
    
    /// <summary>
    /// Manual interaction (for fallback input).
    /// </summary>
    public void TryInteract()
    {
        if (currentInteractable != null)
        {
            currentInteractable.Interact(gameObject);
        }
    }
    #endregion
    
    #region State Handling
    private void HandleGameStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.Playing:
                CanMove = true;
                CanLook = true;
                LockCursor(true);
                break;
                
            case GameState.Paused:
            case GameState.Question:
            case GameState.Dialogue:
            case GameState.GameOver:
            case GameState.Victory:
                CanMove = false;
                CanLook = false;
                LockCursor(false);
                break;
        }
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// Teleports the player to a position.
    /// </summary>
    public void TeleportTo(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;
    }
    
    /// <summary>
    /// Sets the player's rotation.
    /// </summary>
    public void SetRotation(float yRotation)
    {
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        xRotation = 0f;
        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.identity;
        }
    }
    
    /// <summary>
    /// Bakış kontrolünü açar/kapatır.
    /// </summary>
    public void SetLookEnabled(bool enabled)
    {
        CanLook = enabled;
    }
    
    /// <summary>
    /// Hareket kontrolünü açar/kapatır.
    /// </summary>
    public void SetMovementEnabled(bool enabled)
    {
        CanMove = enabled;
    }
    
    /// <summary>
    /// CameraHolder Transform'unu döndürür.
    /// </summary>
    public Transform GetCameraHolder()
    {
        return cameraHolder;
    }
    #endregion
}

/// <summary>
/// Interface for interactable objects.
/// </summary>
public interface IInteractable
{
    /// <summary>Called when player looks at this object.</summary>
    void OnLookAt();
    
    /// <summary>Called when player looks away from this object.</summary>
    void OnLookAway();
    
    /// <summary>Called when player interacts with this object.</summary>
    void Interact(GameObject interactor);
}

