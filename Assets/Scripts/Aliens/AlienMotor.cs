using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Encarga el movimiento al NavMeshAgent. Solo acepta caminos completos y abandona un movimiento si deja de avanzar, para no quedar atascado.</summary>
[RequireComponent(typeof(NavMeshAgent))]
public sealed class AlienMotor : MonoBehaviour
{
    public NavMeshAgent Agent { get; private set; }
    public bool Arrived { get; private set; }
    private NavMeshPath path;
    private void Awake() { Agent = GetComponent<NavMeshAgent>(); path = new NavMeshPath(); }
    public void SetSpeed(float speed)
    {
        if (Agent != null) Agent.speed = Mathf.Max(.1f, speed);
    }
    public readonly struct RoomPlacement
    {
        public readonly Vector3 LocalPosition, LocalDestination;
        public readonly Quaternion LocalRotation;
        public readonly bool Enabled, Moving;
        public RoomPlacement(AlienMotor motor, Transform room)
        {
            LocalPosition = room.InverseTransformPoint(motor.transform.position);
            LocalRotation = Quaternion.Inverse(room.rotation) * motor.transform.rotation;
            Enabled = motor.Agent.enabled;
            Moving = Enabled && motor.Agent.isOnNavMesh && motor.Agent.hasPath && !motor.Agent.isStopped;
            LocalDestination = Moving ? room.InverseTransformPoint(motor.Agent.destination) : LocalPosition;
        }
    }
    public RoomPlacement SuspendForPlacement(Transform room)
    {
        var placement = new RoomPlacement(this, room);
        Agent.enabled = false;
        return placement;
    }
    public void RestorePlacement(Transform room, RoomPlacement placement)
    {
        transform.SetPositionAndRotation(room.TransformPoint(placement.LocalPosition), room.rotation * placement.LocalRotation);
        Agent.enabled = placement.Enabled;
        if (!placement.Enabled) return;
        Agent.Warp(transform.position);
        if (placement.Moving) SetDestination(room.TransformPoint(placement.LocalDestination));
    }
    public bool TryPoint(Vector3 point, float radius, out Vector3 result)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = RoomNavigation.AgentType, areaMask = NavMesh.AllAreas };
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, radius, filter))
        { result = hit.position; return true; }
        result = point; return false;
    }
    public bool SetDestination(Vector3 point, float sampleRadius = .8f, RoomModule restrictToRoom = null)
    {
        Arrived = false;
        if (!Agent.enabled || !Agent.isOnNavMesh || !TryPoint(point, sampleRadius, out Vector3 target)) return false;
        if (restrictToRoom != null && !restrictToRoom.ContainsPoint(target, .15f)) return false;
        if (!Agent.CalculatePath(target, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        if (restrictToRoom != null)
            foreach (Vector3 corner in path.corners) if (!restrictToRoom.ContainsPoint(corner, .05f)) return false;
        Agent.isStopped = false; return Agent.SetPath(path);
    }
    public bool CanReach(Vector3 point, RoomModule room)
    {
        if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh
            || !TryPoint(point, .8f, out Vector3 target) || !room.ContainsPoint(target, .15f)) return false;
        if (!Agent.CalculatePath(target, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        foreach (Vector3 corner in path.corners)
            if (!room.ContainsPoint(corner, .05f)) return false;
        return true;
    }
    public bool SetGroundDestination(Vector3 point, RoomModule room)
    {
        // Busca alrededor del mueble; su superficie superior puede tener NavMesh, pero ser una isla inaccesible.
        point.y = room.transform.position.y + .05f;
        Vector3 best = point;
        float bestDistance = float.PositiveInfinity;
        for (int ring = 0; ring <= 3; ring++)
            for (int direction = 0; direction < (ring == 0 ? 1 : 12); direction++)
            {
                float angle = direction * Mathf.PI / 6f;
                Vector3 candidate = point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring * .5f;
                if (!TryPoint(candidate, .3f, out Vector3 ground) || Mathf.Abs(ground.y - point.y) > .3f) continue;
                float distance = (ground - point).sqrMagnitude;
                if (distance >= bestDistance || !CanReach(ground, room)) continue;
                best = ground;
                bestDistance = distance;
            }
        return bestDistance < float.PositiveInfinity && SetDestination(best, .15f, room);
    }
    public IEnumerator Go(Vector3 point, float timeout = 15f)
    {
        if (!SetDestination(point)) yield break;
        float end = Time.time + timeout;
        Vector3 lastPosition = transform.position;
        float stalledSince = Time.time;
        while (Time.time < end && Agent.enabled && Agent.isOnNavMesh)
        {
            if (!Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance + .08f)
            { Arrived = true; break; }
            if ((transform.position - lastPosition).sqrMagnitude > .01f)
            { lastPosition = transform.position; stalledSince = Time.time; }
            if (Time.time - stalledSince > 2.5f) break;
            yield return null;
        }
        Stop();
    }
    public void Stop()
    {
        if (Agent != null && Agent.enabled && Agent.isOnNavMesh) { Agent.isStopped = true; Agent.ResetPath(); }
    }
}
