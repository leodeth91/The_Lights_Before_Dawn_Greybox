using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>La herencia comparte el inicio y el final de los estados. Cada estado define su propia acción.</summary>
public abstract class AlienState
{
    public abstract string Name { get; }
    public abstract IEnumerator Execute(AlienController alien);
}
public sealed class AlienWanderState : AlienState
{
    public override string Name => "Deambula";
    public override IEnumerator Execute(AlienController alien)
    {
        alien.Motor.SetSpeed(alien.PatrolSpeed);
        Vector2 duration = alien.WanderDuration;
        float end = Time.time + Random.Range(duration.x, duration.y);
        RoomModule room = alien.Occupant.Room;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * 3f;
            Vector3 point = alien.transform.position + new Vector3(offset.x, 0, offset.y);
            if (offset.sqrMagnitude > .36f && room.ContainsPoint(point, .35f)
                && alien.Motor.SetDestination(point, .8f, room)) break;
        }
        while (Time.time < end) yield return null;
        alien.Motor.Stop();
    }
}
public sealed class AlienScanState : AlienState
{
    public override string Name => "Observa";
    public override IEnumerator Execute(AlienController alien)
    {
        alien.Visible.Clear(); alien.Scanning = true;
        // Se vacía la lista para esta observación. Solo reúne objetos visibles y alcanzables; luego elige uno según los pesos de actividad.
        Vector2 duration = alien.ScanDuration;
        float scanSeconds = Random.Range(duration.x, duration.y);
        float end = Time.time + scanSeconds;
        float previous = Time.time;
        float nextObservation = Time.time;
        while (Time.time < end)
        {
            // Gira también el cuerpo: la cabeza sola no llega a observar lo que quedó a su espalda.
            alien.transform.Rotate(0f, 360f * (Time.time - previous) / scanSeconds, 0f, Space.World);
            previous = Time.time;
            if (Time.time >= nextObservation)
            {
                nextObservation = Time.time + .12f;
                foreach (AlienInterest interest in AlienInterest.Active)
                    if (!alien.Visible.Contains(interest) && !alien.IsCoolingDown(interest)
                        && alien.Perception.Visible(interest, alien.Head.Head, alien.Occupant.Room)
                        && alien.Motor.CanReach(interest.ApproachPoint, alien.Occupant.Room)) alien.Visible.Add(interest);
            }
            yield return null;
        }
        alien.Scanning = false;
        alien.Target = alien.Perception.Choose(alien.Visible,
            alien.HideSpotInterestWeight, alien.PassageInterestWeight);
    }
}
public sealed class AlienApproachState : AlienState
{
    public override string Name => "Se acerca";
    public override IEnumerator Execute(AlienController alien)
    {
        if (alien.Target == null || !alien.Target.Available) yield break;
        yield return alien.Motor.Go(alien.Target.ApproachPoint);
    }
}
public sealed class AlienInspectState : AlienState
{
    public override string Name => "Inspecciona";
    public override IEnumerator Execute(AlienController alien)
    {
        IInspectable inspectable = alien.Target != null ? alien.Target.GetComponent<IInspectable>() : null;
        if (!alien.TryBeginInspection(inspectable) || alien.CatchRequested) yield break;
        yield return new WaitForSeconds(alien.InspectionDuration);
        alien.FinishInspection();
    }
}

public sealed class AlienAlertState : AlienState
{
    public override string Name => "Alerta";
    public override IEnumerator Execute(AlienController alien)
    {
        alien.Motor.Stop();
        alien.NotifyAlertStarted();
        float end = Time.time + alien.AlertDuration;
        while (Time.time < end)
        {
            alien.FocusPoint = alien.PlayerVisible && alien.Player != null
                ? alien.Player.PerceptionTransform.position : alien.Memory.LastKnownPosition;
            yield return null;
        }
    }
}

public sealed class AlienChaseState : AlienState
{
    public override string Name => "Persigue";
    public override IEnumerator Execute(AlienController alien)
    {
        alien.NotifyChaseStarted();
        alien.Motor.SetSpeed(alien.ChaseSpeed);
        float nextDestination = 0f;
        Vector3 lastProgress = alien.transform.position;
        float stalledSince = Time.time;
        while (alien.Memory.HasLastKnownPosition && alien.Player != null
            && (GameSessionManager.Instance == null || GameSessionManager.Instance.IsPlaying))
        {
            // Aunque ya no lo vea, termina de seguir su último rastro. Nunca consulta su posición oculta.
            if (!alien.PlayerVisible && alien.Memory.TimeSinceLastSeen > 12f) break;
            if (alien.Memory.LastKnownRoom != null && alien.Memory.LastKnownRoom != alien.Occupant.Room)
            {
                yield return alien.TravelToRoom(alien.Memory.LastKnownRoom);
                if (alien.Memory.LastKnownRoom != alien.Occupant.Room) break;
                stalledSince = Time.time;
                lastProgress = alien.transform.position;
            }
            Vector3 movementTarget = alien.PlayerVisible
                ? alien.Player.transform.position : alien.Memory.LastKnownPosition;
            Vector3 remaining = movementTarget - alien.transform.position;
            remaining.y = 0f;
            if (!alien.HasRecentVisualContact && remaining.magnitude <= .5f) break;
            alien.FocusPoint = alien.PlayerVisible
                ? alien.Player.PerceptionTransform.position : alien.Memory.LastKnownPosition;
            if (alien.PlayerVisible
                && alien.Player.DistanceToBody(alien.Head.Head.position) <= alien.CatchDistance)
            {
                alien.RequestCatch();
                yield break;
            }
            if (Time.time >= nextDestination)
            {
                nextDestination = Time.time + alien.ChaseDestinationRefresh;
                // Persigue por el suelo, aunque vea al Player sobre un mueble no transitable.
                Vector3 groundTarget = movementTarget;
                if (alien.PlayerVisible && alien.Player.GetComponent<RoomOccupant>()?.Room == alien.Occupant.Room)
                    groundTarget.y = alien.transform.position.y;
                bool elevated = alien.PlayerVisible && movementTarget.y - groundTarget.y > .35f;
                if (!(elevated ? alien.Motor.SetGroundDestination(groundTarget, alien.Occupant.Room)
                    : alien.Motor.SetDestination(groundTarget, 1.1f))) break;
            }
            if ((alien.transform.position - lastProgress).sqrMagnitude > .01f)
            { lastProgress = alien.transform.position; stalledSince = Time.time; }
            if (Time.time - stalledSince > 2.5f) break;
            yield return null;
        }
        alien.Motor.Stop();
        alien.FocusPoint = alien.Memory.HasLastKnownPosition
            ? alien.Memory.LastKnownPosition : (Vector3?)null;
    }
}

public sealed class AlienSearchState : AlienState
{
    private readonly ISearchStrategy strategy = new DirectionalSearchStrategy();
    public override string Name => "Busca";
    public override IEnumerator Execute(AlienController alien)
    {
        if (!alien.Memory.HasLastKnownPosition) yield break;
        alien.Motor.SetSpeed(alien.SearchSpeed * 1.5f);
        alien.FocusPoint = alien.Memory.LastKnownPosition;
        if (alien.Memory.LastKnownRoom != alien.Occupant.Room)
            yield return alien.TravelToRoom(alien.Memory.LastKnownRoom);
        if (alien.Memory.LastKnownRoom != alien.Occupant.Room) yield break;
        yield return alien.Motor.Go(alien.Memory.LastKnownPosition, 12f);
        float end = Time.time + alien.SearchDuration;
        RoomModule room = alien.Occupant.Room;
        Vector3 localOrigin = room.transform.InverseTransformPoint(alien.Memory.LastKnownPosition);
        Vector3 localDirection = room.transform.InverseTransformDirection(alien.Memory.LastKnownDirection);
        var candidates = new List<AlienSearchPoint>(room.GetComponentsInChildren<AlienSearchPoint>(true));
        bool triedExit = false;
        while (Time.time < end && alien.Memory.HasLastKnownPosition)
        {
            if (room == null || room != alien.Occupant.Room) yield break;
            Vector3 origin = room.transform.TransformPoint(localOrigin);
            Vector3 direction = room.transform.TransformDirection(localDirection);
            AlienSearchPoint point = strategy.Select(candidates, origin, direction, alien.transform.position);
            if (point == null) yield break;
            // Cada candidato se intenta una sola vez por búsqueda, incluso si no tiene camino.
            candidates.Remove(point);
            DoorSocket door = point.AssociatedDoor;
            if (point.Kind == AlienSearchPointKind.Door)
            {
                Vector3 offset = door != null ? door.Position - origin : Vector3.zero;
                offset.y = 0f;
                if (triedExit || door == null || direction.sqrMagnitude < .01f
                    || Vector3.Dot(direction.normalized, offset.normalized) < .25f
                    || alien.IsCoolingDown(door.GetComponent<DoorInterest>())) continue;
                triedExit = true;
                RoomModule previousRoom = room;
                yield return alien.CrossDoor(door);
                if (alien.Occupant.Room == previousRoom) continue;
                point.MarkVisited();
                room = alien.Occupant.Room;
                // La habitación siguiente es una hipótesis, no una nueva observación del jugador.
                localOrigin = room.transform.InverseTransformPoint(alien.transform.position);
                localDirection = room.transform.InverseTransformDirection(alien.transform.forward);
                candidates = new List<AlienSearchPoint>(room.GetComponentsInChildren<AlienSearchPoint>(true));
                continue;
            }
            HideSpot spot = point.AssociatedHidingSpot;
            Vector3 destination = point.SearchPosition;
            alien.FocusPoint = destination;
            yield return alien.Motor.Go(destination, Mathf.Min(8f, end - Time.time));
            if (!alien.Motor.Arrived) { yield return null; continue; }
            point.MarkVisited();
            if (spot != null)
            {
                if (alien.TryBeginInspection(spot))
                {
                    if (alien.CatchRequested) yield break;
                    yield return new WaitForSeconds(alien.InspectionDuration);
                    alien.FinishInspection();
                }
            }
            else
            {
                alien.Scanning = true;
                alien.FocusPoint = null;
                float observeUntil = Time.time + Random.Range(.8f, 1.8f);
                while (Time.time < observeUntil) yield return null;
                alien.Scanning = false;
            }
        }
    }
}

public sealed class AlienInvestigateKnownHideSpotState : AlienState
{
    public override string Name => "Revisa escondite conocido";
    public override IEnumerator Execute(AlienController alien)
    {
        HideSpot spot = alien.Memory.KnownHidingSpot;
        if (spot == null) yield break;
        RoomModule spotRoom = spot.GetComponentInParent<RoomModule>();
        if (spotRoom != alien.Occupant.Room) yield return alien.TravelToRoom(spotRoom);
        if (spotRoom != alien.Occupant.Room) yield break;
        alien.Motor.SetSpeed(alien.SearchSpeed);
        alien.FocusPoint = spot.InspectionPosition;
        yield return alien.Motor.Go(spot.InspectionPosition, 15f);
        if (!alien.Motor.Arrived || spot == null) yield break;
        if (!alien.TryBeginInspection(spot)) yield break;
        if (alien.CatchRequested) yield break;
        yield return new WaitForSeconds(alien.InspectionDuration);
        alien.FinishInspection();
        alien.Memory.ForgetHidingSpot(spot);
    }
}

public sealed class AlienCatchPlayerState : AlienState
{
    public override string Name => "Atrapa";
    public override IEnumerator Execute(AlienController alien)
    {
        alien.Motor.Stop();
        alien.FinishInspection();
        if (alien.Player != null) alien.FocusPoint = alien.Player.PerceptionTransform.position;
        GameSessionManager.Instance?.TriggerDefeat();
        yield return null;
    }
}
public sealed class AlienTraverseState : AlienState
{
    public override string Name => "Cruza";
    public override IEnumerator Execute(AlienController alien)
    {
        if (alien.Target is StairInterest stairs)
        {
            yield return alien.Motor.Go(stairs.Destination, 20);
            yield break;
        }
        DoorInterest interest = alien.Target as DoorInterest;
        if (interest == null) yield break;
        yield return alien.CrossDoor(interest.Socket);
    }
}
