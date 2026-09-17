using UnityEngine;

/// <summary>Guarda lo que sabe cada alien. Las posiciones se guardan dentro de la habitación para que el recuerdo siga siendo válido si la anomalía la mueve.</summary>
[DisallowMultipleComponent]
public sealed class AlienMemory : MonoBehaviour
{
    [SerializeField] private RoomModule lastKnownRoom;
    [SerializeField] private Vector3 lastKnownLocalPosition;
    [SerializeField] private Vector3 lastKnownLocalDirection;
    [SerializeField] private HideSpot knownHidingSpot;
    [SerializeField] private float lastSeenTime = float.NegativeInfinity;

    private float previousObservationTime = float.NegativeInfinity;

    public bool HasLastKnownPosition => lastKnownRoom != null;
    public RoomModule LastKnownRoom => lastKnownRoom;
    public Vector3 LastKnownPosition => lastKnownRoom != null
        ? lastKnownRoom.transform.TransformPoint(lastKnownLocalPosition) : transform.position;
    public Vector3 LastKnownDirection => lastKnownRoom != null
        ? lastKnownRoom.transform.TransformDirection(lastKnownLocalDirection) : Vector3.zero;
    public HideSpot KnownHidingSpot => knownHidingSpot;
    public float TimeSinceLastSeen => float.IsNegativeInfinity(lastSeenTime)
        ? float.PositiveInfinity : Mathf.Max(0f, Time.time - lastSeenTime);

    public void Observe(PlayerVisibility player, RoomModule room)
    {
        if (player == null || room == null) return;
        // Si lo ve nuevamente fuera, el escondite anterior ya no es su ubicación actual.
        if (player.HidingController == null || player.HidingController.State == PlayerHideState.Outside)
            knownHidingSpot = null;
        // El destino para caminar debe estar en el suelo. Los puntos de cabeza y pecho solo sirven para comprobar la visión.
        Vector3 worldPosition = player.transform.position;
        float elapsed = Time.time - previousObservationTime;
        // Comparamos dentro de la misma habitación: moverla por la anomalía no es movimiento del jugador.
        Vector3 worldDirection = room == lastKnownRoom && elapsed > 0.02f && elapsed < .6f
            ? (worldPosition - LastKnownPosition) / elapsed : Vector3.zero;
        worldDirection.y = 0f;
        if (room != lastKnownRoom || elapsed >= .6f) lastKnownLocalDirection = Vector3.zero;
        if (worldDirection.sqrMagnitude > 0.01f)
            lastKnownLocalDirection = room.transform.InverseTransformDirection(worldDirection.normalized);
        lastKnownRoom = room;
        lastKnownLocalPosition = room.transform.InverseTransformPoint(worldPosition);
        previousObservationTime = Time.time;
        lastSeenTime = Time.time;
    }

    public void RememberHidingSpot(HideSpot spot)
    {
        if (spot != null) knownHidingSpot = spot;
    }

    public void ForgetHidingSpot(HideSpot spot = null)
    {
        if (spot == null || knownHidingSpot == spot) knownHidingSpot = null;
    }

    public void ClearSearch()
    {
        lastKnownRoom = null;
        lastKnownLocalDirection = Vector3.zero;
        knownHidingSpot = null;
        previousObservationTime = float.NegativeInfinity;
    }

    private void OnDrawGizmosSelected()
    {
        if (lastKnownRoom == null) return;
        Gizmos.color = new Color(1f, .2f, .1f, .9f);
        Gizmos.DrawWireSphere(LastKnownPosition, .18f);
        if (LastKnownDirection.sqrMagnitude > .01f)
            Gizmos.DrawRay(LastKnownPosition, LastKnownDirection.normalized * 1.2f);
    }
}
