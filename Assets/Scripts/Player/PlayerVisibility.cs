using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerVisibility : MonoBehaviour, IPerceptionTarget
{
    [SerializeField] private PlayerHidingController hidingController;
    [SerializeField] private ChildPlayerController movementController;
    [SerializeField] private Transform perceptionPoint;
    private CharacterController characterController;

    public PerceptionTargetKind PerceptionKind => PerceptionTargetKind.Player;
    public Transform PerceptionTransform => perceptionPoint != null ? perceptionPoint : transform;
    public bool IsAvailableForDetection => !IsHidden;
    public bool IsHidden => hidingController != null && hidingController.IsHidden;
    public bool IsEnteringHideSpot => hidingController != null
        && hidingController.State == PlayerHideState.Entering;
    public bool IsCrouched => movementController != null && movementController.IsCrouched;
    // Agacharse baja los puntos que miran los aliens, de modo que un mueble pueda taparlos. Estar agachado a la vista sigue siendo detectable.
    public float VisibilityFactor => IsHidden ? 0f : 1f;
    public PlayerHidingController HidingController => hidingController;

    // Mide hasta el cuerpo, no hasta los pies: estar sobre un mueble no vuelve inmune al Player.
    public float DistanceToBody(Vector3 point)
        => characterController != null && characterController.enabled
            ? Vector3.Distance(point, characterController.bounds.ClosestPoint(point))
            : Vector3.Distance(point, transform.position);

    public int GetPerceptionPoints(Vector3[] buffer)
    {
        if (buffer == null || buffer.Length == 0) return 0;
        int count = 0;
        buffer[count++] = PerceptionTransform.position;
        if (characterController != null && characterController.enabled)
        {
            Bounds bounds = characterController.bounds;
            if (count < buffer.Length) buffer[count++] = bounds.center;
            if (count < buffer.Length)
                buffer[count++] = bounds.center - Vector3.up * bounds.extents.y * .42f;
        }
        return count;
    }

    public void Configure(PlayerHidingController hiding, ChildPlayerController movement,
        Transform visiblePoint)
    {
        hidingController = hiding;
        movementController = movement;
        perceptionPoint = visiblePoint;
    }

    private void Awake()
    {
        if (hidingController == null) hidingController = GetComponent<PlayerHidingController>();
        if (movementController == null) movementController = GetComponent<ChildPlayerController>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
    }
}
