using System.Collections.Generic;
using UnityEngine;

public sealed class AlienPerception : MonoBehaviour
{
    [SerializeField, Min(1)] private float range = 14;
    [SerializeField, Range(10, 150)] private float fieldOfView = 95;
    [SerializeField, Range(10, 180)] private float bodyAwarenessFieldOfView = 120f;
    [SerializeField] private LayerMask visionMask = ~(1 << 2);
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly Vector3[] playerPoints = new Vector3[3];
    public float Range => range;
    public float FieldOfView => fieldOfView;
    public bool Visible(IAlienInterest target, Transform head, RoomModule room)
    {
        Component targetComponent = target as Component;
        if (targetComponent == null || !target.Available || target.Room != room) return false;
        return HasLineOfSight(targetComponent, target.LookPoint, head, range,
            target as DoorInterest);
    }

    public bool CanSeePlayer(PlayerVisibility player, Transform head,
        RoomModule observerRoom, HouseFlowController house)
    {
        if (player == null || head == null || observerRoom == null
            || !player.IsAvailableForDetection) return false;
        RoomOccupant playerOccupant = player.GetComponent<RoomOccupant>();
        RoomModule playerRoom = playerOccupant != null ? playerOccupant.Room : null;
        if (!RoomsShareVisibleSpace(observerRoom, playerRoom, house)) return false;
        float effectiveRange = range * Mathf.Clamp(player.VisibilityFactor, .25f, 1f);
        int pointCount = player.GetPerceptionPoints(playerPoints);
        for (int i = 0; i < pointCount; i++)
            if (HasPlayerLineOfSight(player, playerPoints[i], head, effectiveRange)) return true;
        return false;
    }

    private bool HasPlayerLineOfSight(PlayerVisibility player, Vector3 targetPoint,
        Transform head, float maximumRange)
    {
        Vector3 delta = targetPoint - head.position;
        if (delta.sqrMagnitude > maximumRange * maximumRange || delta.sqrMagnitude < .0001f)
            return false;
        Vector3 planarDirection = Vector3.ProjectOnPlane(delta, Vector3.up);
        if (planarDirection.sqrMagnitude > .0001f
            && Vector3.Dot(transform.forward, planarDirection.normalized) <= 0f)
            return false; // Lo que queda detrás del alien no se ve; permite escabullirse.
        // La apertura horizontal no debe reducirse porque el Player esté sobre una mesada.
        if (Mathf.Abs(Mathf.Atan2(delta.y, planarDirection.magnitude) * Mathf.Rad2Deg) > 80f) return false;
        bool headView = Vector3.Angle(Vector3.ProjectOnPlane(head.forward, Vector3.up), planarDirection) <= fieldOfView * .5f;
        bool bodyView = Vector3.Angle(transform.forward, planarDirection) <= bodyAwarenessFieldOfView * .5f;
        if (!headView && !bodyView) return false;
        return RayReaches(player, targetPoint, head, null);
    }

    private bool HasLineOfSight(Component targetComponent, Vector3 targetPoint,
        Transform head, float maximumRange, DoorInterest door)
    {
        Vector3 delta = targetPoint - head.position;
        if (delta.sqrMagnitude > maximumRange * maximumRange
            || delta.sqrMagnitude < .0001f
            || Vector3.Angle(head.forward, delta) > fieldOfView * .5f) return false;
        return RayReaches(targetComponent, targetPoint, head, door);
    }

    // El rayo comprueba obstáculos reales. Estar dentro del campo visual no basta si una pared o un mueble tapa al objetivo.
    private bool RayReaches(Component targetComponent, Vector3 targetPoint,
        Transform head, DoorInterest door)
    {
        Vector3 delta = targetPoint - head.position;
        int count = Physics.RaycastNonAlloc(head.position, delta.normalized, hits,
            delta.magnitude, visionMask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false; // Si no entran todos los resultados, evita dar una detección dudosa.
        float nearest = float.MaxValue;
        Collider blocker = null;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.GetComponentInParent<AlienController>() == GetComponent<AlienController>()) continue;
            if (hits[i].distance < nearest) { nearest = hits[i].distance; blocker = hits[i].collider; }
        }
        if (blocker == null || blocker.transform.IsChildOf(targetComponent.transform)) return true;
        return door != null && (blocker.GetComponentInParent<DoorInteractable>() == door.Socket.Door
            || blocker.GetComponentInParent<DoorFrameInteractable>()?.Door == door.Socket.Door);
    }

    private static bool RoomsShareVisibleSpace(RoomModule observer, RoomModule target,
        HouseFlowController house)
    {
        if (observer == null || target == null) return false;
        if (observer == target) return true;
        if (house == null) return false;
        foreach (RoomConnection connection in house.Connections)
        {
            bool joinsRooms = (connection.SourceRoom == observer && connection.TargetRoom == target)
                || (connection.SourceRoom == target && connection.TargetRoom == observer);
            if (joinsRooms && connection.SourceSocket.Door.State == DoorState.Open) return true;
        }
        return false;
    }
    public AlienInterest Choose(List<AlienInterest> visible,
        float hideSpotWeight = 1f, float passageWeight = 1f)
    {
        // Primero elige una categoría con igual probabilidad; después elige uno de sus objetos.
        var kinds = new List<AlienInterestKind>();
        foreach (AlienInterest interest in visible)
            if (interest != null && interest.Available && !kinds.Contains(interest.Kind)) kinds.Add(interest.Kind);
        if (kinds.Count == 0) return null;
        float totalWeight = 0f;
        foreach (AlienInterestKind candidate in kinds)
            totalWeight += Weight(candidate, hideSpotWeight, passageWeight);
        float selection = Random.value * totalWeight;
        AlienInterestKind kind = kinds[kinds.Count - 1];
        foreach (AlienInterestKind candidate in kinds)
        {
            selection -= Weight(candidate, hideSpotWeight, passageWeight);
            if (selection <= 0f) { kind = candidate; break; }
        }
        var candidates = visible.FindAll(i => i != null && i.Available && i.Kind == kind);
        return candidates[Random.Range(0, candidates.Count)];
    }

    private static float Weight(AlienInterestKind kind, float hideSpotWeight,
        float passageWeight)
    {
        if (kind == AlienInterestKind.HideSpot) return Mathf.Max(.01f, hideSpotWeight);
        if (kind == AlienInterestKind.Passage) return Mathf.Max(.01f, passageWeight);
        return 1f;
    }
}
