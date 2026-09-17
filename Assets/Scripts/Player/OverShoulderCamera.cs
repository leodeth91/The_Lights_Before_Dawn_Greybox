using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Cámara sobre el hombro que sigue al jugador y permite mirar con el mouse. Se acerca al personaje cuando una pared tapa la vista.</summary>
public sealed class OverShoulderCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public InputActionAsset inputActions;
    public Vector3 targetOffset = new Vector3(0f, 0.86f, 0f);

    [Header("Shoulder framing")]
    public float distance = 2.80f;
    public float shoulderOffset = 0.46f;
    public float cameraHeight = 0.25f;
    public float positionSmoothing = 14f;
    public float rotationSmoothing = 18f;

    [Header("Look")]
    public float mouseSensitivity = 0.08f;
    public float minimumPitch = -12f;
    public float maximumPitch = 32f;
    public bool lockCursorOnPlay = true;

    [Header("Collision")]
    public float collisionRadius = 0.16f;
    public float collisionPadding = 0.08f;
    public LayerMask collisionMask = ~(1 << 2);

    [Header("Hiding view")]
    public Vector3 hidingTargetOffset = new Vector3(0f, 0.32f, 0f);
    public float hidingDistance = 1.55f;
    public float hidingShoulderOffset = 0.22f;

    private InputAction lookAction;
    private float yaw;
    private float pitch = 10f;
    private bool cursorLocked;
    private float hidingBlend;

    public void SetHidingView(bool hiding)
    {
        hidingBlend = hiding ? 1f : 0f;
    }

    private void Awake()
    {
        CacheLookAction();
        if (target != null)
        {
            yaw = target.eulerAngles.y;
        }
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target != null && Mathf.Approximately(yaw, 0f))
        {
            yaw = target.eulerAngles.y;
        }

        SetCursorState(lockCursorOnPlay);
    }

    private void OnEnable()
    {
        CacheLookAction();
        InputActionMap map = lookAction != null ? lookAction.actionMap : null;
        if (map != null)
        {
            map.Enable();
        }
    }

    private void OnDisable()
    {
        InputActionMap map = lookAction != null ? lookAction.actionMap : null;
        if (map != null)
        {
            map.Disable();
        }

        SetCursorState(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorState(false);
        }

        if (!cursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            SetCursorState(true);
        }

        if (!cursorLocked)
        {
            return;
        }

        Vector2 look = ReadLookInput();
        yaw += look.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, minimumPitch, maximumPitch);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 effectiveTargetOffset = Vector3.Lerp(targetOffset, hidingTargetOffset,
            hidingBlend);
        float effectiveDistance = Mathf.Lerp(distance, hidingDistance, hidingBlend);
        float effectiveShoulder = Mathf.Lerp(shoulderOffset, hidingShoulderOffset,
            hidingBlend);
        Vector3 pivot = target.position + effectiveTargetOffset;
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desired = pivot + orbit * new Vector3(effectiveShoulder, cameraHeight,
            -effectiveDistance);
        Vector3 direction = desired - pivot;
        float desiredDistance = direction.magnitude;

        if (desiredDistance > 0.001f && Physics.SphereCast(pivot, collisionRadius,
                direction.normalized, out RaycastHit hit, desiredDistance, collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            desired = pivot + direction.normalized * Mathf.Max(0.05f, hit.distance - collisionPadding);
        }

        float positionBlend = 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, positionBlend);

        Vector3 lookPoint = pivot + orbit * Vector3.forward * 10f;
        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        float rotationBlend = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
    }

    private void CacheLookAction()
    {
        lookAction = null;
        if (inputActions == null)
        {
            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap("Player", false);
        if (playerMap != null)
        {
            lookAction = playerMap.FindAction("Look", false);
        }
    }

    private Vector2 ReadLookInput()
    {
        if (lookAction != null)
        {
            return lookAction.ReadValue<Vector2>();
        }

        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
    }

    private void SetCursorState(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
