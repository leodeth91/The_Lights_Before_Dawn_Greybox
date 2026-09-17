using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>Mueve al niño con el CharacterController de Unity. Al agacharse cambia la altura del cuerpo y del collider manteniendo los pies sobre el suelo.</summary>
[RequireComponent(typeof(CharacterController))]
public sealed class ChildPlayerController : MonoBehaviour
{
    [Header("Input")]
    public InputActionAsset inputActions;
    public Transform cameraTransform;

    [Header("Movement")]
    public float walkSpeed = 1.65f;
    public float sprintSpeed = 3.20f;
    public float crouchSpeed = 0.90f;
    public float turnSpeed = 720f;
    public float gravity = -20f;
    public float jumpHeight = 0.50f;
    public float runningJumpHeightMultiplier = 1.35f;

    [Header("Child scale")]
    public float standingHeight = 1.15f;
    [Range(0.35f, 0.8f)] public float crouchedTotalHeightRatio = 0.50f;
    public float colliderRadius = 0.22f;
    public float heightChangeSpeed = 8f;

    [Header("Crouch appearance")]
    [SerializeField] private Transform visualBody;
    [SerializeField] private Transform visualHead;
    [SerializeField] private UnityEvent<bool> onCrouchChanged = new UnityEvent<bool>();

    private CharacterController characterController;
    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private InputAction jumpAction;
    private float verticalVelocity;
    private bool crouched;
    private bool movementLocked;
    private Vector3 standingBodyPosition;
    private Vector3 standingBodyScale;
    private Vector3 standingHeadPosition;
    private float crouchedHeight;
    private bool visualShapeCached;

    public bool IsCrouched => crouched;
    public bool IsSprinting { get; private set; }
    public bool IsGrounded => characterController != null && characterController.isGrounded;
    public bool IsMovementLocked => movementLocked;
    public float CrouchedHeight => crouchedHeight;
    public UnityEvent<bool> OnCrouchChanged => onCrouchChanged;

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
        if (locked)
        {
            verticalVelocity = 0f;
            IsSprinting = false;
            SetCrouched(false, true);
        }
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        CacheCrouchAppearance();
        ConfigureController();
        CacheInputActions();
    }

    private void OnEnable()
    {
        CacheInputActions();
        if (playerMap != null)
        {
            playerMap.Enable();
        }
    }

    private void OnDisable()
    {
        IsSprinting = false;
        SetCrouched(false, true);
        if (playerMap != null)
        {
            playerMap.Disable();
        }
    }

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        if (movementLocked)
        {
            return;
        }

        UpdateCrouchState();

        Vector2 input = ReadMoveInput();
        Vector3 movement = GetCameraRelativeMovement(input);
        bool sprinting = !crouched && ReadSprintInput() && input.sqrMagnitude > 0.01f;
        IsSprinting = sprinting;
        float speed = crouched ? crouchSpeed : (sprinting ? sprintSpeed : walkSpeed);

        RotateTowardsMovement(movement);

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (ReadJumpInput() && characterController.isGrounded && !crouched)
        {
            float jumpHeightForThisJump = sprinting
                ? jumpHeight * runningJumpHeightMultiplier
                : jumpHeight;
            verticalVelocity = Mathf.Sqrt(jumpHeightForThisJump * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = movement * speed + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void ConfigureController()
    {
        characterController.height = standingHeight;
        characterController.radius = colliderRadius;
        characterController.center = Vector3.up * (standingHeight * 0.5f);
        characterController.stepOffset = 0.23f;
        characterController.slopeLimit = 45f;
        characterController.skinWidth = 0.025f;
        characterController.minMoveDistance = 0f;
    }

    // Busca las acciones por su nombre interno en el nuevo Input System; esos nombres deben conservarse en inglés.
    private void CacheInputActions()
    {
        playerMap = null;
        moveAction = null;
        sprintAction = null;
        crouchAction = null;
        jumpAction = null;
        if (inputActions == null)
        {
            return;
        }

        playerMap = inputActions.FindActionMap("Player", false);
        if (playerMap == null)
        {
            return;
        }

        moveAction = playerMap.FindAction("Move", false);
        sprintAction = playerMap.FindAction("Sprint", false);
        crouchAction = playerMap.FindAction("Crouch", false);
        jumpAction = playerMap.FindAction("Jump", false);
    }

    private Vector2 ReadMoveInput()
    {
        if (moveAction != null)
        {
            return Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        return Vector2.ClampMagnitude(input, 1f);
    }

    private bool ReadSprintInput()
    {
        if (sprintAction != null)
        {
            return sprintAction.IsPressed();
        }

        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
    }

    private bool ReadCrouchInput()
    {
        if (crouchAction != null)
        {
            return crouchAction.IsPressed();
        }

        return Keyboard.current != null && Keyboard.current.cKey.isPressed;
    }

    private bool ReadJumpInput()
    {
        if (jumpAction != null)
        {
            return jumpAction.WasPressedThisFrame();
        }

        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    }

    private Vector3 GetCameraRelativeMovement(Vector2 input)
    {
        Transform movementCamera = cameraTransform != null ? cameraTransform : Camera.main?.transform;
        Vector3 forward = movementCamera != null ? movementCamera.forward : Vector3.forward;
        Vector3 right = movementCamera != null ? movementCamera.right : Vector3.right;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        right = Vector3.ProjectOnPlane(right, Vector3.up).normalized;
        return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
    }

    private void RotateTowardsMovement(Vector3 movement)
    {
        if (movement.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation,
            turnSpeed * Time.deltaTime);
    }

    private void UpdateCrouchState()
    {
        bool wantsCrouch = ReadCrouchInput();
        if (!wantsCrouch && crouched && !CanStand())
        {
            wantsCrouch = true;
        }

        SetCrouched(wantsCrouch);
        UpdateCrouchShape(false);
    }

    /// <summary>Permite usar la misma acción de agacharse desde los controles, los tutoriales y las pruebas.</summary>
    public void SetCrouched(bool value, bool immediate = false)
    {
        if (!value && crouched && !movementLocked && !CanStand()) value = true;
        if (crouched != value)
        {
            crouched = value;
            onCrouchChanged?.Invoke(crouched);
        }
        if (immediate) UpdateCrouchShape(true);
    }

    private void UpdateCrouchShape(bool immediate)
    {
        if (characterController == null) return;
        float targetHeight = crouched ? crouchedHeight : standingHeight;
        float newHeight = Mathf.MoveTowards(characterController.height, targetHeight,
            immediate ? Mathf.Infinity : heightChangeSpeed * Time.deltaTime);
        characterController.height = newHeight;
        characterController.center = Vector3.up * (newHeight * 0.5f);
        characterController.stepOffset = crouched
            ? Mathf.Min(0.12f, newHeight * 0.20f) : 0.23f;

        if (!visualShapeCached) return;
        float amount = immediate ? Mathf.Infinity : heightChangeSpeed * Time.deltaTime;
        Vector3 targetBodyScale = standingBodyScale;
        Vector3 targetBodyPosition = standingBodyPosition;
        Vector3 targetHeadPosition = standingHeadPosition;
        if (crouched)
        {
            targetBodyScale.y = standingBodyScale.y * crouchedTotalHeightRatio;
            // La cápsula del cuerpo, antes de escalarla, tiene una mitad de altura de una unidad.
            targetBodyPosition.y = targetBodyScale.y;
            float headRadius = visualHead.localScale.y * 0.5f;
            targetHeadPosition.y = Mathf.Max(targetBodyScale.y,
                crouchedHeight - headRadius);
        }
        visualBody.localScale = Vector3.MoveTowards(visualBody.localScale,
            targetBodyScale, amount);
        visualBody.localPosition = Vector3.MoveTowards(visualBody.localPosition,
            targetBodyPosition, amount);
        visualHead.localPosition = Vector3.MoveTowards(visualHead.localPosition,
            targetHeadPosition, amount);
    }

    private void CacheCrouchAppearance()
    {
        Transform visualRoot = transform.Find("PlayerVisual");
        if (visualBody == null && visualRoot != null)
            visualBody = visualRoot.Find("Visual_Cuerpo");
        if (visualHead == null && visualRoot != null)
            visualHead = visualRoot.Find("Visual_Cabeza");
        visualShapeCached = visualBody != null && visualHead != null;
        if (!visualShapeCached)
        {
            crouchedHeight = Mathf.Max(colliderRadius * 2f + 0.01f,
                standingHeight * crouchedTotalHeightRatio);
            return;
        }
        standingBodyPosition = visualBody.localPosition;
        standingBodyScale = visualBody.localScale;
        standingHeadPosition = visualHead.localPosition;
        float standingVisualTop = Mathf.Max(
            standingBodyPosition.y + standingBodyScale.y,
            standingHeadPosition.y + visualHead.localScale.y * 0.5f);
        crouchedHeight = Mathf.Max(colliderRadius * 2f + 0.01f,
            standingVisualTop * crouchedTotalHeightRatio);
    }

    private bool CanStand()
    {
        int blockingMask = ~(1 << gameObject.layer);
        Vector3 bottom = transform.position + Vector3.up * colliderRadius;
        Vector3 top = transform.position + Vector3.up * (standingHeight - colliderRadius);
        return !Physics.CheckCapsule(bottom, top, colliderRadius, blockingMask,
            QueryTriggerInteraction.Ignore);
    }
}
