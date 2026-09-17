using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Usa el centro de la cámara para elegir el objeto cercano con el que interactuar. Lee la acción Player/Interact y, si falta, la tecla E del nuevo Input System.</summary>
public sealed class PlayerInteractor : MonoBehaviour
{
    public InputActionAsset inputActions;
    public Transform cameraTransform;
    public float interactionDistance = 2.40f;
    [Min(0.01f)] public float aimAssistRadius = 0.10f;
    public LayerMask interactionMask = ~(1 << 2);

    private InputActionMap playerMap;
    private InputAction interactAction;
    private PlayerHidingController hidingController;
    private IInteractable focusedInteractable;
    private StoryPhoneInteractionArea nearbyPhone;
    public void SetNearbyPhone(StoryPhoneInteractionArea area) { nearbyPhone = area; }
    public void ClearNearbyPhone(StoryPhoneInteractionArea area) { if (nearbyPhone == area) nearbyPhone = null; }
    private readonly Collider[] nearbyColliders = new Collider[32];

    public string CurrentPrompt { get; private set; } = string.Empty;
    public event Action<string> PromptChanged;

    private void Awake()
    {
        CacheInputAction();
        hidingController = GetComponent<PlayerHidingController>();
    }

    private void OnEnable()
    {
        CacheInputAction();
        if (playerMap != null)
        {
            playerMap.Enable();
        }
    }

    private void OnDisable()
    {
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
        if (GameSessionManager.Instance != null && !GameSessionManager.Instance.IsPlaying)
        {
            SetFocusedInteractable(null);
            return;
        }

        Transform interactionCamera = cameraTransform != null ? cameraTransform : Camera.main?.transform;
        if (interactionCamera == null)
        {
            SetFocusedInteractable(null);
            return;
        }

        IInteractable priority = hidingController != null
            ? hidingController.PriorityInteractable : null;
        // La zona del celular evita tener que acertar con la cámara; salir de un escondite conserva prioridad.
        IInteractable phonePriority = nearbyPhone != null && nearbyPhone.CanInteract(gameObject) ? nearbyPhone : null;
        IInteractable interactable = priority ?? phonePriority ?? FindInteractable(interactionCamera);
        if (interactable != null && !interactable.CanInteract(gameObject))
        {
            interactable = null;
        }
        SetFocusedInteractable(interactable);

        if (WasInteractPressed() && interactable != null)
        {
            interactable.Interact(gameObject);
        }
    }

    private void SetFocusedInteractable(IInteractable interactable)
    {
        string nextPrompt = interactable != null ? interactable.InteractionPrompt : string.Empty;
        focusedInteractable = interactable;
        if (nextPrompt == CurrentPrompt)
        {
            return;
        }

        CurrentPrompt = nextPrompt;
        PromptChanged?.Invoke(CurrentPrompt);
    }

    // Ordena los impactos desde el más cercano. Un objeto sólido delante debe impedir usar otro que está detrás.
    private IInteractable FindInteractable(Transform interactionCamera)
    {
        Ray ray = new Ray(interactionCamera.position, interactionCamera.forward);
        RaycastHit[] hits = Physics.SphereCastAll(ray, Mathf.Max(0.01f, aimAssistRadius),
            interactionDistance, interactionMask, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        bool directAimBlocked = false;

        foreach (RaycastHit hit in hits)
        {
            // La búsqueda puede tocar el propio cuerpo del jugador. Ignora sus colliders para que no interfieran.
            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            IInteractable aimedInteractable = ResolveInteractable(hit.collider);
            if (aimedInteractable != null)
            {
                return aimedInteractable;
            }

            if (!hit.collider.isTrigger)
            {
                directAimBlocked = true;
                break;
            }
        }

        // Si la mira apunta a un mueble sólido, no ofrece una puerta que esté detrás. La ayuda por cercanía solo se usa cuando la vista está libre.
        if (directAimBlocked)
        {
            return null;
        }

        Vector3 origin = transform.position + Vector3.up * 0.75f;
        int count = Physics.OverlapSphereNonAlloc(origin, interactionDistance, nearbyColliders,
            interactionMask, QueryTriggerInteraction.Ignore);
        HashSet<IInteractable> candidates = new HashSet<IInteractable>();
        IInteractable closestInteractable = null;
        float closestScore = float.MaxValue;
        Vector3 cameraForward = Vector3.ProjectOnPlane(interactionCamera.forward, Vector3.up).normalized;

        for (int i = 0; i < count; i++)
        {
            IInteractable candidate = ResolveInteractable(nearbyColliders[i]);
            if (candidate == null || !candidates.Add(candidate))
            {
                continue;
            }


            // Para esconderse hay que apuntar al escondite. Los marcos mantienen la ayuda por cercanía para facilitar cerrar puertas abiertas.
            if (candidate is HideSpot || candidate is StoryPhone || candidate is StoryPhoneInteractionArea)
            {
                continue;
            }

            // Para abrir una puerta cerrada hay que apuntarla. La cercanía solo ayuda a cerrar las que ya están abiertas.
            if (IsClosedDoor(candidate))
            {
                continue;
            }

            Vector3 closestPoint = nearbyColliders[i].ClosestPoint(origin);
            float distance = Vector3.Distance(origin, closestPoint);
            if (distance > interactionDistance)
            {
                continue;
            }

            Vector3 direction = closestPoint - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                continue;
            }

            float facing = Vector3.Dot(cameraForward, direction.normalized);
            if (facing < -0.75f)
            {
                continue;
            }

            float score = distance - facing * 0.35f;
            if (score < closestScore)
            {
                closestScore = score;
                closestInteractable = candidate;
            }
        }

        return closestInteractable;
    }

    private static bool IsClosedDoor(IInteractable interactable)
    {
        if (interactable is DoorInteractable door)
        {
            return door.State == DoorState.Closed;
        }

        if (interactable is DoorFrameInteractable frame && frame.Door != null)
        {
            return frame.Door.State == DoorState.Closed;
        }

        return false;
    }

    private static IInteractable ResolveInteractable(Collider source)
    {
        if (source == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = source.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IInteractable interactable)
            {
                return interactable;
            }
        }

        return null;
    }

    private void CacheInputAction()
    {
        playerMap = null;
        interactAction = null;
        if (inputActions == null)
        {
            return;
        }

        playerMap = inputActions.FindActionMap("Player", false);
        if (playerMap != null)
        {
            interactAction = playerMap.FindAction("Interact", false);
        }
    }

    private bool WasInteractPressed()
    {
        bool actionPressed = interactAction != null && interactAction.WasPressedThisFrame();
        bool keyboardPressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        return actionPressed || keyboardPressed;
    }
}
