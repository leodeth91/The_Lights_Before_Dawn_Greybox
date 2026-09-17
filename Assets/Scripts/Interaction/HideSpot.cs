using UnityEngine;
using UnityEngine.Events;

public enum HideTransitionType
{
    ProneSlide,
    CrouchSlide,
    StepInside,
    ClimbInside,
    BehindCurtain,
    StepOverAndHide
}

public enum HideConcealment
{
    Enclosed,
    VisualCover
}

/// <summary>Guarda los puntos de entrada, ocultamiento y salida y quién ocupa el escondite. No muevas esos puntos separados del mueble al reacomodarlo.</summary>
[DisallowMultipleComponent]
public sealed class HideSpot : MonoBehaviour, IInteractable, IPerceptionTarget, IInspectable
{
    [SerializeField] private string displayName = "debajo de la cama";
    [SerializeField] private string objectName = "Cama";
    [SerializeField] private Transform entryPoint;
    [SerializeField] private Transform hiddenPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private Collider interactionBounds;
    [SerializeField] private bool allowEntryFromAnySide;
    [SerializeField] private float alignDuration = 0.25f;
    [SerializeField] private float transitionDuration = 0.55f;
    [SerializeField] private HideTransitionType transitionType;
    [SerializeField] private HideConcealment concealment;
    [SerializeField, Min(0.44f)] private float hiddenControllerHeight = 0.48f;
    [SerializeField, Min(0.1f)] private float maxInteractionDistance = 0.65f;
    [SerializeField] private Vector3 hiddenVisualEulerAngles = new Vector3(0f, 0f, 90f);
    [SerializeField] private Vector3 hiddenVisualScale = Vector3.one;
    [SerializeField] private UnityEvent onEnterStarted = new UnityEvent();
    [SerializeField] private UnityEvent onHidden = new UnityEvent();
    [SerializeField] private UnityEvent onExitStarted = new UnityEvent();
    [SerializeField] private UnityEvent onExited = new UnityEvent();
    [SerializeField] private UnityEvent onInvestigated = new UnityEvent();

    private PlayerHidingController occupant;
    // Se guarda la referencia del ocupante, no un bool duplicado: así la revisión sabe si realmente hay alguien dentro.
    private GameObject inspector;
    public string InspectionName => objectName;
    public Vector3 InspectionPosition => entryPoint != null ? entryPoint.position : transform.position;
    public bool TryBeginInspection(GameObject actor)
    {
        if (actor == null || (inspector != null && inspector != actor)) return false;
        inspector = actor;
        GetComponent<IOpenable>()?.TryOpen(actor);
        return true;
    }
    public void EndInspection(GameObject actor)
    {
        if (inspector != actor) return;
        inspector = null;
        GetComponent<IOpenable>()?.TryClose(actor);
        // AlienController comprueba si está ocupado y decide si el jugador pierde.
        NotifyInvestigated();
    }

    public string InteractionPrompt => occupant != null && occupant.IsHidden
        ? "E: Salir" : objectName + "\nE: Esconderse";
    public PerceptionTargetKind PerceptionKind => PerceptionTargetKind.HideSpot;
    public Transform PerceptionTransform => hiddenPoint != null ? hiddenPoint : transform;
    public bool IsAvailableForDetection => true;
    public Transform EntryPoint => entryPoint;
    public Transform HiddenPoint => hiddenPoint;
    public Transform ExitPoint => exitPoint != null ? exitPoint : entryPoint;
    public float AlignDuration => alignDuration;
    public float TransitionDuration => transitionDuration;
    public string DisplayName => displayName;
    public string ObjectName => objectName;
    public bool IsOccupied => occupant != null && occupant.IsHidden;
    public float MaxInteractionDistance => maxInteractionDistance;
    public Collider InteractionBounds => interactionBounds;
    public bool AllowsEntryFromAnySide => allowEntryFromAnySide;
    public HideTransitionType TransitionType => transitionType;
    public HideConcealment Concealment => concealment;
    public float HiddenControllerHeight => hiddenControllerHeight;
    public Vector3 HiddenVisualEulerAngles => hiddenVisualEulerAngles;
    public Vector3 HiddenVisualScale => hiddenVisualScale;
    public UnityEvent OnEnterStarted => onEnterStarted;
    public UnityEvent OnHidden => onHidden;
    public UnityEvent OnExitStarted => onExitStarted;
    public UnityEvent OnExited => onExited;
    public UnityEvent OnInvestigated => onInvestigated;

    public void Configure(string visibleName, Transform entry, Transform hidden, Transform exit,
        HideTransitionType type = HideTransitionType.ProneSlide,
        HideConcealment concealmentType = HideConcealment.Enclosed,
        float controllerHeight = 0.48f,
        Vector3? visualEulerAngles = null,
        Vector3? visualScale = null,
        float alignmentSeconds = 0.25f, float transitionSeconds = 0.55f,
        string objectLabel = null, float interactionDistance = 0.65f,
        Collider bounds = null, bool anySideEntry = false)
    {
        displayName = visibleName;
        entryPoint = entry;
        hiddenPoint = hidden;
        exitPoint = exit;
        alignDuration = Mathf.Max(0.05f, alignmentSeconds);
        transitionDuration = Mathf.Max(0.05f, transitionSeconds);
        transitionType = type;
        concealment = concealmentType;
        hiddenControllerHeight = Mathf.Max(0.44f, controllerHeight);
        hiddenVisualEulerAngles = visualEulerAngles ?? GetDefaultVisualRotation(type);
        hiddenVisualScale = visualScale ?? Vector3.one;
        objectName = string.IsNullOrWhiteSpace(objectLabel) ? "Escondite" : objectLabel;
        maxInteractionDistance = Mathf.Max(0.1f, interactionDistance);
        interactionBounds = bounds;
        allowEntryFromAnySide = anySideEntry;
    }

    public void GetEntryPose(Vector3 interactorPosition, out Vector3 position,
        out Quaternion rotation)
    {
        Transform fallback = entryPoint != null ? entryPoint : transform;
        position = fallback.position;
        rotation = fallback.rotation;
        if (!allowEntryFromAnySide || hiddenPoint == null
            || !(interactionBounds is BoxCollider box))
        {
            return;
        }

        Vector3 localPlayer = transform.InverseTransformPoint(interactorPosition);
        Vector3 halfSize = box.size * 0.5f;
        Vector3 center = box.center;
        float normalizedX = Mathf.Abs(localPlayer.x - center.x)
            / Mathf.Max(0.01f, halfSize.x);
        float normalizedZ = Mathf.Abs(localPlayer.z - center.z)
            / Mathf.Max(0.01f, halfSize.z);
        const float controllerClearance = 0.38f;

        Vector3 localEntry = center;
        localEntry.y = transform.InverseTransformPoint(fallback.position).y;
        if (normalizedX > normalizedZ)
        {
            float side = localPlayer.x < center.x ? -1f : 1f;
            localEntry.x = center.x + side * (halfSize.x + controllerClearance);
            localEntry.z = Mathf.Clamp(localPlayer.z, center.z - halfSize.z * 0.72f,
                center.z + halfSize.z * 0.72f);
        }
        else
        {
            float side = localPlayer.z < center.z ? -1f : 1f;
            localEntry.z = center.z + side * (halfSize.z + controllerClearance);
            localEntry.x = Mathf.Clamp(localPlayer.x, center.x - halfSize.x * 0.72f,
                center.x + halfSize.x * 0.72f);
        }

        position = transform.TransformPoint(localEntry);
        Vector3 inward = Vector3.ProjectOnPlane(hiddenPoint.position - position, Vector3.up);
        if (inward.sqrMagnitude > 0.001f)
        {
            rotation = Quaternion.LookRotation(inward.normalized, Vector3.up);
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        PlayerHidingController hiding = interactor != null
            ? interactor.GetComponent<PlayerHidingController>() : null;
        if (hiding == null || hiding.IsTransitioning || (occupant != null && occupant != hiding))
        {
            return false;
        }

        if (occupant == hiding && hiding.IsHidden)
        {
            return true;
        }

        Vector3 nearestPoint = interactionBounds != null
            ? interactionBounds.ClosestPoint(interactor.transform.position)
            : (entryPoint != null ? entryPoint.position : transform.position);
        Vector3 difference = interactor.transform.position - nearestPoint;
        difference.y = 0f;
        return difference.magnitude <= maxInteractionDistance;
    }

    public void Interact(GameObject interactor)
    {
        PlayerHidingController hiding = interactor != null
            ? interactor.GetComponent<PlayerHidingController>() : null;
        if (hiding != null && CanInteract(interactor))
        {
            hiding.Toggle(this);
        }
    }

    public bool TryClaim(PlayerHidingController player)
    {
        if (player == null || (occupant != null && occupant != player))
        {
            return false;
        }

        occupant = player;
        return true;
    }

    public void Release(PlayerHidingController player)
    {
        if (occupant == player)
        {
            occupant = null;
        }
    }

    public void NotifyEnterStarted() => onEnterStarted?.Invoke();
    public void NotifyHidden() => onHidden?.Invoke();
    public void NotifyExitStarted() => onExitStarted?.Invoke();
    public void NotifyExited() => onExited?.Invoke();
    public void NotifyInvestigated() => onInvestigated?.Invoke();

    private static Vector3 GetDefaultVisualRotation(HideTransitionType type)
    {
        return type == HideTransitionType.ProneSlide
            ? new Vector3(0f, 0f, 90f)
            : Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        if (entryPoint == null || hiddenPoint == null)
        {
            return;
        }

        Gizmos.color = new Color(0.20f, 0.85f, 1f, 0.9f);
        Gizmos.DrawLine(entryPoint.position, hiddenPoint.position);
        Gizmos.DrawWireSphere(entryPoint.position, 0.12f);
        Gizmos.color = new Color(0.30f, 1f, 0.40f, 0.9f);
        Gizmos.DrawWireCube(hiddenPoint.position, Vector3.one * 0.20f);
        if (ExitPoint != null)
        {
            Gizmos.color = new Color(1f, 0.70f, 0.20f, 0.9f);
            Gizmos.DrawWireSphere(ExitPoint.position, 0.10f);
        }
    }
}
