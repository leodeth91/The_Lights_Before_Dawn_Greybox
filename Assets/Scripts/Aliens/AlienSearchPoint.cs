using UnityEngine;

public enum AlienSearchPointKind { Generic, Corner, Door, HidingSpot, Exit }

[DisallowMultipleComponent]
public sealed class AlienSearchPoint : MonoBehaviour
{
    [SerializeField] private AlienSearchPointKind kind;
    [SerializeField, Min(.1f)] private float weight = 1f;
    [SerializeField] private HideSpot associatedHidingSpot;
    [SerializeField] private float lastVisitedTime = float.NegativeInfinity;

    public AlienSearchPointKind Kind => kind;
    public float Weight => weight;
    public HideSpot AssociatedHidingSpot => associatedHidingSpot;
    public RoomModule Room => GetComponentInParent<RoomModule>();
    // Los marcadores antiguos no tienen referencia a la puerta. La resolvemos por cercanía, sin nombres.
    public DoorSocket AssociatedDoor
    {
        get
        {
            if (kind != AlienSearchPointKind.Door || Room == null) return null;
            DoorSocket best = null;
            float distance = 1.5f * 1.5f;
            foreach (DoorSocket socket in Room.Sockets)
            {
                if (socket == null) continue;
                Vector3 offset = transform.position - (socket.Position - socket.Outward * .8f);
                offset.y = 0f;
                if (offset.sqrMagnitude >= distance) continue;
                best = socket;
                distance = offset.sqrMagnitude;
            }
            return best;
        }
    }
    public Vector3 SearchPosition => associatedHidingSpot != null
        ? associatedHidingSpot.InspectionPosition
        : AssociatedDoor != null ? AssociatedDoor.ClearApproachPoint : transform.position;
    public float TimeSinceVisited => float.IsNegativeInfinity(lastVisitedTime)
        ? float.PositiveInfinity : Mathf.Max(0f, Time.time - lastVisitedTime);

    public void Configure(AlienSearchPointKind pointKind, float pointWeight,
        HideSpot hidingSpot = null)
    {
        kind = pointKind;
        weight = Mathf.Max(.1f, pointWeight);
        associatedHidingSpot = hidingSpot;
    }

    public void MarkVisited() => lastVisitedTime = Time.time;

    private void OnDrawGizmos()
    {
        Gizmos.color = kind == AlienSearchPointKind.HidingSpot
            ? new Color(1f, .45f, .1f, .8f) : new Color(.2f, .8f, 1f, .65f);
        Gizmos.DrawWireSphere(transform.position, .15f);
        Gizmos.DrawRay(transform.position, transform.forward * .35f);
    }
}
