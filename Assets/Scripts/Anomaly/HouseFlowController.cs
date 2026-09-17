using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Coordina las habitaciones compartidas. Sus ocupantes y puertas conectadas determinan cuándo pueden volver al pool.</summary>
[DisallowMultipleComponent]
public sealed class HouseFlowController : MonoBehaviour
{
    public static HouseFlowController Instance { get; private set; }
    [SerializeField] private List<RoomModule> roomModules = new List<RoomModule>();
    [SerializeField] private Transform modulePoolRoot;
    [SerializeField] private RoomModule initialRoom;
    [SerializeField] private Transform player;
    [Header("Anomaly randomness")]
    [Tooltip("Genera una semilla nueva al cargar cada partida. Desactivar permite usar una semilla fija para pruebas.")]
    [SerializeField] private bool randomizeSeed = true;
    [SerializeField] private int randomSeed = 20260901;
    [Tooltip("Cada conexión elegible sortea un destino seguro. Desactivar permite probar la probabilidad configurada abajo.")]
    [SerializeField] private bool randomizeEveryEligibleDoor = true;
    [SerializeField, Range(0f, 1f)] private float anomalyChance = .4f;
    [SerializeField] private List<NormalRoomConnection> normalConnections = new List<NormalRoomConnection>();
    [SerializeField] private bool verboseLogging;
    [Header("Door coordination")]
    [SerializeField, Min(1f)] private float requestLifetime = 10f;
    [SerializeField, Min(1f)] private float passageLifetime = 8f;
    [SerializeField, Min(1f)] private float pursuitLifetime = 12f;
    [SerializeField] private Vector2 automaticCloseDelay = new Vector2(9f, 16f);
    private readonly Dictionary<string, RoomModule> moduleById = new Dictionary<string, RoomModule>();
    private readonly Dictionary<string, DoorSocket> socketById = new Dictionary<string, DoorSocket>();
    private readonly Dictionary<string, string> normalDestinationBySocketId = new Dictionary<string, string>();
    private readonly List<RoomConnection> connections = new List<RoomConnection>();
    private readonly List<RoomOccupant> occupants = new List<RoomOccupant>();
    private readonly Dictionary<RoomModule, Pose> parking = new Dictionary<RoomModule, Pose>();
    private readonly HashSet<RoomConnection> closeWhenClear = new HashSet<RoomConnection>();
    private readonly Dictionary<RoomConnection, GameObject> closeRequestedBy
        = new Dictionary<RoomConnection, GameObject>();
    private sealed class DoorRequest
    {
        public DoorSocket Socket;
        public GameObject Actor;
        public float Expires, NextAttempt;
        public bool IsPlayer;
    }
    // Guarda una solicitud por personaje. Dentro de cada prioridad atiende primero al que pidió antes, sin duplicar intentos.
    // La lista conserva el orden de llegada y permite cancelar una petición. Un stack atendería primero la última y podría dejar esperando a las anteriores.
    private readonly List<DoorRequest> requests = new List<DoorRequest>();
    private readonly List<GameObject> expiredActors = new List<GameObject>();
    private readonly List<RoomConnection> connectionSnapshot = new List<RoomConnection>();
    public int PendingRequestCount => requests.Count;
    public int ActiveSeed { get; private set; }
    private AnomalyDirector anomalyDirector;
    private System.Random connectionRandom;
    public RoomModule CurrentRoom { get; private set; }
    public RoomConnection ActiveConnection => connections.Find(c => c.SourceRoom == CurrentRoom || c.TargetRoom == CurrentRoom);
    public IReadOnlyList<RoomConnection> Connections => connections;
    public IReadOnlyList<RoomOccupant> Occupants => occupants;
    public IReadOnlyDictionary<string, RoomModule> ModuleById => moduleById;
    public int RegisteredSocketCount => socketById.Count;
    public bool LastConnectionWasAnomalous { get; private set; }
    public event Action<RoomConnection> ConnectionOpened;
    public event Action<RoomConnection> ConnectionClosed;
    public event Action<RoomModule, RoomModule> CurrentRoomChanged;

    public void Configure(List<RoomModule> modules, RoomModule startingRoom, Transform poolRoot,
        Transform playerTransform, int seed, List<NormalRoomConnection> topology, float chance)
    {
        roomModules = modules; initialRoom = startingRoom; modulePoolRoot = poolRoot;
        player = playerTransform; randomSeed = seed; normalConnections = topology; anomalyChance = chance;
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Una semilla por partida produce una secuencia nueva sin volver a crear el azar en cada puerta.
        // Se conserva para poder consultarla; los intentos de apertura usan la misma secuencia.
        ActiveSeed = randomizeSeed ? Guid.NewGuid().GetHashCode() : randomSeed;
        anomalyDirector = new AnomalyDirector(new RandomRoomSelectionStrategy(ActiveSeed));
        connectionRandom = new System.Random(ActiveSeed ^ 0x5F3759DF);
        if (verboseLogging) Debug.Log("Anomaly seed: " + ActiveSeed);
        if (modulePoolRoot != null) roomModules = new List<RoomModule>(modulePoolRoot.GetComponentsInChildren<RoomModule>(true));
        if (player == null) player = GameObject.FindWithTag("Player")?.transform;
        foreach (RoomModule room in roomModules)
        {
            if (room == null) continue;
            if (!moduleById.TryAdd(room.RoomId, room)) { Debug.LogError("Duplicate room: " + room.RoomId); continue; }
            parking[room] = new Pose(room.transform.position, room.transform.rotation);
            room.RefreshSockets(); room.ResetDoorsInstant(); room.SetCurrent(false);
            foreach (DoorSocket socket in room.Sockets)
            {
                if (!socketById.TryAdd(socket.SocketId, socket)) Debug.LogError("Duplicate socket: " + socket.SocketId);
                if (socket.Door != null) socket.Door.Closed += HandleDoorClosed;
            }
        }
        foreach (NormalRoomConnection pair in normalConnections)
        {
            if (!socketById.ContainsKey(pair.SocketA) || !socketById.ContainsKey(pair.SocketB))
            { Debug.LogError("Unknown normal connection: " + pair.SocketA + " / " + pair.SocketB); continue; }
            normalDestinationBySocketId[pair.SocketA] = pair.SocketB;
            normalDestinationBySocketId[pair.SocketB] = pair.SocketA;
        }
        if (initialRoom == null || !roomModules.Contains(initialRoom)) moduleById.TryGetValue("child_room", out initialRoom);
        foreach (RoomModule room in roomModules) if (room != null) room.gameObject.SetActive(room == initialRoom);
        if (initialRoom != null)
        {
            Vector3 translation = -initialRoom.transform.position;
            translation.y = 0;
            initialRoom.transform.position += translation;
            if (player != null)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                bool enabledBefore = cc != null && cc.enabled;
                if (enabledBefore) cc.enabled = false;
                player.position += translation;
                if (enabledBefore) cc.enabled = true;
                RegisterOccupant(player.gameObject, initialRoom, false);
            }
            SetCurrentRoom(initialRoom);
        }
        Physics.SyncTransforms();
        foreach (RoomModule room in roomModules)
        {
            if (room != null && room.gameObject.activeInHierarchy)
            {
                ReconcileThresholdSensors(room);
            }
        }
    }
    public RoomOccupant RegisterOccupant(GameObject actor, RoomModule room, bool normalDoors)
    {
        RoomOccupant occupant = actor.GetComponent<RoomOccupant>() ?? actor.AddComponent<RoomOccupant>();
        occupant.Configure(room, normalDoors);
        if (!occupants.Contains(occupant)) occupants.Add(occupant);
        if (room != null) room.gameObject.SetActive(true);
        return occupant;
    }
    public bool IsOccupied(RoomModule room) => occupants.Exists(o => o != null && o.gameObject.activeInHierarchy && o.Room == room);
    public RoomConnection ConnectionFor(DoorSocket socket) => connections.Find(c => c.SourceSocket == socket || c.TargetSocket == socket);
    public DoorSocket NormalDestination(DoorSocket socket)
    {
        return socket != null && normalDestinationBySocketId.TryGetValue(socket.SocketId, out string id)
            && socketById.TryGetValue(id, out DoorSocket target) ? target : null;
    }
    public bool RequestOpen(DoorSocket socket, GameObject actor)
    {
        if (actor == null) actor = player != null ? player.gameObject : null;
        RoomOccupant occupant = actor != null ? actor.GetComponent<RoomOccupant>() : null;
        if (socket == null || occupant == null || !actor.activeInHierarchy) return false;
        RoomConnection existing = ConnectionFor(socket);
        if (existing != null && existing.TargetRoom == occupant.Room) socket = existing.TargetSocket;
        if (socket.Room != occupant.Room) return false;
        if (occupant.UsesNormalDoors && existing != null && existing.IsAnomalous
            && !CanFollow(existing, actor)) return false;
        if (TryOpenNow(socket, actor, !occupant.UsesNormalDoors)) { CancelDoorRequest(actor); return true; }
        DoorRequest request = requests.Find(r => r.Actor == actor);
        if (request != null && request.Socket == socket) return true;
        CancelDoorRequest(actor);
        requests.Add(new DoorRequest { Socket = socket, Actor = actor,
            IsPlayer = !occupant.UsesNormalDoors, Expires = Time.time + requestLifetime,
            NextAttempt = Time.time + .05f });
        return true; // Solicitud aceptada; la puerta puede seguir esperando para abrir.
    }
    public void CancelDoorRequest(GameObject actor) => requests.RemoveAll(r => r.Actor == actor);
    public bool CanFollow(RoomConnection connection, GameObject actor)
        => connection != null && actor != null && connection.Pursuers.TryGetValue(actor, out float until)
            && until > Time.time;
    public void RememberPursuit(GameObject actor, RoomModule destination)
    {
        RoomModule origin = actor.GetComponent<RoomOccupant>()?.Room;
        foreach (RoomConnection connection in connections)
            if ((connection.SourceRoom == origin && connection.TargetRoom == destination)
                || (connection.TargetRoom == origin && connection.SourceRoom == destination))
                connection.Pursuers[actor] = Time.time + pursuitLifetime;
    }
    public void ForgetPursuit(GameObject actor)
    {
        foreach (RoomConnection connection in connections) connection.Pursuers.Remove(actor);
    }
    private bool TryOpenNow(DoorSocket socket, GameObject actor, bool allowAlternative = false)
    {
        RoomOccupant occupant = actor != null ? actor.GetComponent<RoomOccupant>() : null;
        RoomModule actorRoom = occupant != null ? occupant.Room : CurrentRoom;
        bool normalOnly = occupant != null && occupant.UsesNormalDoors;
        if (socket == null || socket.Door == null || actorRoom == null) return false;
        RoomConnection existing = ConnectionFor(socket);
        if (existing != null)
        {
            if (actorRoom != existing.SourceRoom && actorRoom != existing.TargetRoom) return false;
            if (normalOnly && existing.IsAnomalous && !CanFollow(existing, actor)) return false;
            if (existing.SourceSocket.Door.State == DoorState.Closed
                || existing.SourceSocket.Door.State == DoorState.Closing)
            {
                closeWhenClear.Remove(existing);
                closeRequestedBy.Remove(existing);
                existing.AutoCloseAt = NextAutoClose();
                existing.OpenProtectedUntil = Time.time + 1.2f;
                return existing.SourceSocket.Door.BeginOpen(actor);
            }
            return true;
        }
        if (socket.Room != actorRoom) return false;
        RoomConnection other = connections.Find(c => c.SourceRoom == actorRoom || c.TargetRoom == actorRoom);
        if (other != null)
        {
            if (normalOnly && Time.time < other.OpenProtectedUntil) return false;
            // Una persecución detenida por una puerta cerrada no debe bloquear las otras salidas.
            if (other.SourceSocket.Door.State == DoorState.Closed && ThresholdClear(other))
                FinalizeConnection(other);
            else { CloseAfterPassage(other, actor); TryClose(other, actor); return false; }
        }
        return OpenConnection(socket, actor, normalOnly, allowAlternative);
    }
    public bool RequestClose(DoorSocket socket, GameObject actor = null)
    {
        RoomConnection connection = ConnectionFor(socket);
        if (connection == null) return false;
        // Acepta la orden aunque otro personaje siga cruzando. Update la completa cuando sea seguro.
        CloseAfterPassage(connection, actor);
        TryClose(connection, actor);
        return true;
    }
    // Reserva el paso durante el cruce. El plazo evita que una acción interrumpida deje una puerta bloqueada para siempre.
    public bool ReservePassage(RoomConnection connection, GameObject actor)
    {
        if (connection == null || actor == null || !connections.Contains(connection)
            || connection.SourceSocket.Door.State == DoorState.Closing
            || connection.SourceSocket.Door.State == DoorState.Closed) return false;
        connection.PassageUsers[actor] = Time.time + passageLifetime;
        return true;
    }
    public void ReleasePassage(RoomConnection connection, GameObject actor)
    {
        if (connection != null && actor != null) connection.PassageUsers.Remove(actor);
    }
    public void CloseAfterPassage(RoomConnection connection, GameObject actor = null)
    {
        if (connection == null || !connections.Contains(connection)) return;
        closeWhenClear.Add(connection);
        if (actor != null) closeRequestedBy[connection] = actor;
    }
    private bool TryClose(RoomConnection connection, GameObject actor)
    {
        if (connection == null || !connections.Contains(connection)) return false;
        PruneLeases(connection.PassageUsers);
        if (connection.PassageUsers.Count > 0) return false;
        // Cerrar la hoja no es lo mismo que retirar una habitación.
        // Quien juega puede pedir cerrar desde el marco; la liberación del suelo
        // sigue esperando a TODOS los ocupantes mediante ThresholdClear.
        RoomOccupant requester = actor != null ? actor.GetComponent<RoomOccupant>() : null;
        GameObject ignored = requester != null && !requester.UsesNormalDoors ? actor : null;
        if (connection.SourceSocket.IsThresholdOccupiedByOtherThan(ignored)
            || connection.TargetSocket.IsThresholdOccupiedByOtherThan(ignored)) return false;
        DoorInteractable door = connection.SourceSocket.Door;
        if (door.State == DoorState.Open) return door.BeginClose(actor);
        return door.State == DoorState.Closing || door.State == DoorState.Closed;
    }
    private bool ThresholdClear(RoomConnection connection)
    {
        // Nadie se ignora, tampoco quien pidió cerrar: ambos suelos deben mantenerse durante el cruce.
        return !connection.SourceSocket.IsThresholdOccupied
            && !connection.TargetSocket.IsThresholdOccupied;
    }
    private float NextAutoClose() => Time.time + UnityEngine.Random.Range(
        Mathf.Max(1f, automaticCloseDelay.x), Mathf.Max(automaticCloseDelay.x, automaticCloseDelay.y));
    private void PruneLeases(Dictionary<GameObject, float> leases)
    {
        expiredActors.Clear();
        foreach (var lease in leases)
            if (lease.Key == null || !lease.Key.activeInHierarchy || lease.Value <= Time.time
                || (lease.Key.TryGetComponent(out AlienController alien) && !alien.enabled))
                expiredActors.Add(lease.Key);
        foreach (GameObject actor in expiredActors) leases.Remove(actor);
    }
    private bool OpenConnection(DoorSocket source, GameObject actor, bool normalOnly, bool allowAlternative)
    {
        StoryProgression story = StoryProgression.Instance;
        bool storyExit = !normalOnly && story != null && story.ShouldLoopExit(source);
        DoorSocket normal = NormalDestination(source);
        bool protectedHall = source.Room.IsAnchorRoom || source.Kind == DoorSocketKind.HallAccess || (normal != null && normal.Room.IsAnchorRoom);
        bool canUseAnomaly = !normalOnly && !protectedHall && (story == null || story.AnomalyEnabled);
        // Si el destino habitual sigue ocupado, una solicitud esperando puede probar otro destino válido.
        bool alternative = allowAlternative && normal != null && !CanPlace(source, normal);
        bool anomaly = canUseAnomaly && (randomizeEveryEligibleDoor || alternative || normal == null || connectionRandom.NextDouble() < anomalyChance);
        RoomConnectionCandidate destination = default;
        // El primer intento de salir vuelve al cuarto del niño; después se sortean destinos de ambas plantas.
        // El destino narrativo también debe superar las comprobaciones de colocación segura.
        if (storyExit)
        {
            foreach (DoorSocket target in story.ChildRoom.Sockets)
                if (CanPlace(source, target)) { destination = new RoomConnectionCandidate(target.Room, target); break; }
            if (destination.Room == null) return false;
        }
        else if (anomaly)
        {
            var candidates = new List<RoomConnectionCandidate>();
            foreach (RoomModule room in roomModules)
            {
                // Una habitación activa puede estar disponible si está aislada y solo contiene aliens. CanPlace comprueba ocupantes, conexiones y superposiciones antes de moverla.
                // En el modo normal de juego también participa el destino habitual: cualquiera puede salir sorteado.
                if (room == source.Room || room.IsAnchorRoom
                    || (!randomizeEveryEligibleDoor && normal != null && room == normal.Room)) continue;
                foreach (DoorSocket socket in room.Sockets)
                    if (source.IsAnomalyCompatibleWith(socket) && CanPlace(source, socket)) candidates.Add(new RoomConnectionCandidate(room, socket));
            }
            destination = anomalyDirector.SelectDestination(candidates);
        }
        bool isAnomaly = destination.Room != null;
        if (destination.Room == null && normal != null && CanPlace(source, normal)) destination = new RoomConnectionCandidate(normal.Room, normal);
        if (destination.Room == null) return false;
        if (!TryPlanPlacement(source, destination.Socket, out RoomModule relocatedRoom, out Pose placement)) return false;
        if (!destination.Room.gameObject.activeSelf)
        {
            destination.Room.ResetDoorsInstant();
            destination.Room.transform.SetPositionAndRotation(placement.position, placement.rotation);
            destination.Room.gameObject.SetActive(true);
        }
        else if (relocatedRoom != null) RelocateIsolatedRoom(relocatedRoom, placement);
        destination.Socket.SetPortalAssemblyActive(false);
        source.ConnectTo(destination.Socket); destination.Socket.ConnectTo(source);
        var connection = new RoomConnection(source.Room, source, destination.Room, destination.Socket, isAnomaly);
        connection.AutoCloseAt = NextAutoClose();
        connection.OpenProtectedUntil = Time.time + 1.2f;
        connections.Add(connection);
        if (!source.Door.BeginOpen(actor)) { FinalizeConnection(connection); return false; }
        LastConnectionWasAnomalous = isAnomaly;
        if (storyExit) story.CompleteExit();
        ConnectionOpened?.Invoke(connection);
        if (verboseLogging) Debug.Log((isAnomaly ? "ANOMALY " : "NORMAL ") + source.SocketId + " -> " + destination.Socket.SocketId);
        return true;
    }
    private static Pose TargetPose(DoorSocket source, DoorSocket target)
    {
        Transform root = target.Room.transform;
        Quaternion localRotation = Quaternion.Inverse(root.rotation) * target.transform.rotation;
        Quaternion rotation = Quaternion.LookRotation(-source.Outward, Vector3.up) * Quaternion.Inverse(localRotation);
        Vector3 localPosition = root.InverseTransformPoint(target.Position);
        return new Pose(source.Position - rotation * Vector3.Scale(localPosition, root.lossyScale), rotation);
    }
    private bool CanPlace(DoorSocket source, DoorSocket target)
        => TryPlanPlacement(source, target, out _, out _);

    // Calcula la ubicación antes de mover nada: si no hay espacio o la habitación está en uso, se prueba otro destino seguro.
    private bool TryPlanPlacement(DoorSocket source, DoorSocket target, out RoomModule movedRoom, out Pose pose)
    {
        movedRoom = null; pose = default;
        if (target.Room == source.Room || ConnectionFor(target) != null) return false;
        pose = TargetPose(source, target);
        if (target.Room.gameObject.activeSelf)
        {
            if (Vector3.Distance(target.Room.transform.position, pose.position) < .03f
                && Quaternion.Angle(target.Room.transform.rotation, pose.rotation) < .5f) return true;
            // Puede mover habitaciones aisladas y cerradas junto con sus aliens. La habitación del jugador y las conectadas por puertas abiertas o en cierre permanecen en su lugar.
            if (CanRelocate(target.Room) && FitsAt(target.Room, pose)) { movedRoom = target.Room; return true; }
            pose = TargetPose(target, source);
            if (CanRelocate(source.Room) && FitsAt(source.Room, pose)) { movedRoom = source.Room; return true; }
            return false;
        }
        movedRoom = target.Room;
        return FitsAt(target.Room, pose);
    }
    private bool CanRelocate(RoomModule room)
    {
        if (room == CurrentRoom || connections.Exists(c => c.SourceRoom == room || c.TargetRoom == room)) return false;
        foreach (DoorSocket socket in room.Sockets) if (socket.IsThresholdOccupied) return false;
        foreach (RoomOccupant occupant in occupants)
            if (occupant != null && occupant.Room == room && occupant.GetComponent<AlienMotor>() == null) return false;
        return true;
    }
    public bool TryParkIsolatedRoom(RoomModule room)
    {
        // Tras el primer bucle narrativo, despeja la salida sin separar los aliens de su habitación.
        if (room == null || !CanRelocate(room) || !parking.TryGetValue(room, out Pose pose)) return false;
        // Su posición de edición puede coincidir con el hueco que necesitamos liberar.
        // Para este traslado narrativo usamos un espacio apartado, sin cambiar el parking registrado.
        pose.position += Vector3.right * 80f;
        if (!FitsAt(room, pose)) return false;
        RelocateIsolatedRoom(room, pose);
        ReturnUnused(room);
        return true;
    }
    private bool FitsAt(RoomModule movedRoom, Pose pose)
    {
        Vector3 size = movedRoom.LocalVolumeSize;
        Vector3 rotated = pose.rotation * new Vector3(size.x, 0, size.z);
        Bounds desired = new Bounds(pose.position + Vector3.up * size.y * .5f,
            new Vector3(Mathf.Abs(rotated.x) - .08f, size.y - .08f, Mathf.Abs(rotated.z) - .08f));
        foreach (RoomModule room in roomModules)
        {
            if (room == movedRoom || !room.gameObject.activeSelf) continue;
            Vector3 other = room.transform.rotation * new Vector3(room.LocalVolumeSize.x, 0, room.LocalVolumeSize.z);
            Bounds occupied = new Bounds(room.transform.position + Vector3.up * room.LocalVolumeSize.y * .5f,
                new Vector3(Mathf.Abs(other.x) - .08f, room.LocalVolumeSize.y - .08f, Mathf.Abs(other.z) - .08f));
            if (desired.Intersects(occupied)) return false;
        }
        return true;
    }
    // Guarda a los aliens en coordenadas locales y los vuelve a colocar con la habitación. También recoloca su NavMesh.
    private void RelocateIsolatedRoom(RoomModule room, Pose pose)
    {
        var placements = new Dictionary<AlienMotor, AlienMotor.RoomPlacement>();
        foreach (RoomOccupant occupant in occupants)
        {
            if (occupant == null || occupant.Room != room) continue;
            AlienMotor motor = occupant.GetComponent<AlienMotor>();
            if (motor != null) placements.Add(motor, motor.SuspendForPlacement(room.transform));
        }
        room.transform.SetPositionAndRotation(pose.position, pose.rotation);
        room.GetComponent<RoomNavigation>()?.RefreshPlacement();
        foreach (var pair in placements) pair.Key.RestorePlacement(room.transform, pair.Value);
        Physics.SyncTransforms();
        ReconcileThresholdSensors(room);
    }

    private static void ReconcileThresholdSensors(RoomModule room)
    {
        if (room == null || !room.gameObject.activeInHierarchy) return;
        foreach (DoorThresholdSensor sensor in room.GetComponentsInChildren<DoorThresholdSensor>(true))
            if (sensor != null && sensor.isActiveAndEnabled) sensor.ReconcileOccupants();
    }
    private void HandleDoorClosed(DoorInteractable door)
    {
        RoomConnection connection = connections.Find(c => c.SourceSocket.Door == door);
        if (connection == null) return;
        PruneLeases(connection.Pursuers);
        if (connection.Pursuers.Count == 0 && ThresholdClear(connection)) FinalizeConnection(connection);
    }
    private void FinalizeConnection(RoomConnection connection)
    {
        // También protege si alguien entró mientras se animaba el cierre.
        // Una puerta cerrada puede esperar antes de liberar sus habitaciones y el enlace de navegación.
        PruneLeases(connection.PassageUsers);
        if (connection.PassageUsers.Count > 0 || !ThresholdClear(connection)) return;
        TrackOccupants();
        connections.Remove(connection);
        connection.PassageUsers.Clear();
        connection.Pursuers.Clear();
        closeWhenClear.Remove(connection);
        closeRequestedBy.Remove(connection);
        connection.SourceSocket.Disconnect(); connection.TargetSocket.Disconnect();
        connection.TargetSocket.SetPortalAssemblyActive(true);
        connection.TargetSocket.Door.SetClosedInstant();
        ConnectionClosed?.Invoke(connection);
        ReturnUnused(connection.SourceRoom); ReturnUnused(connection.TargetRoom);
    }
    private void ReturnUnused(RoomModule room)
    {
        if (IsOccupied(room) || connections.Exists(c => c.SourceRoom == room || c.TargetRoom == room)) return;
        foreach (DoorSocket socket in room.Sockets) if (socket.IsThresholdOccupied) return;
        room.ResetDoorsInstant(); room.gameObject.SetActive(false);
        if (parking.TryGetValue(room, out Pose pose)) room.transform.SetPositionAndRotation(pose.position, pose.rotation);
    }
    private void Update()
    {
        TrackOccupants();
        connectionSnapshot.Clear();
        connectionSnapshot.AddRange(connections);
        foreach (RoomConnection connection in connectionSnapshot)
        {
            PruneLeases(connection.PassageUsers);
            PruneLeases(connection.Pursuers);
            if (connection.SourceSocket.Door.State == DoorState.Closed
                && connection.PassageUsers.Count == 0 && connection.Pursuers.Count == 0 && ThresholdClear(connection))
            { FinalizeConnection(connection); continue; }
            if (closeWhenClear.Contains(connection) || Time.time >= connection.AutoCloseAt)
            {
                closeRequestedBy.TryGetValue(connection, out GameObject closingActor);
                if (TryClose(connection, closingActor))
                {
                    closeWhenClear.Remove(connection);
                    closeRequestedBy.Remove(connection);
                }
            }
        }
        requests.RemoveAll(r => r.Actor == null || !r.Actor.activeInHierarchy || r.Socket == null
            || Time.time >= r.Expires || r.Actor.GetComponent<RoomOccupant>()?.Room != r.Socket.Room
            || Vector3.Distance(r.Actor.transform.position, r.Socket.Position) > 4f);
        // Atiende por orden de llegada dentro de cada prioridad. Aumenta la prioridad de quienes llevan esperando para evitar esperas indefinidas.
        for (int priority = 0; priority < 2; priority++)
            for (int i = 0; i < requests.Count; i++)
            {
                DoorRequest request = requests[i];
                bool urgent = request.IsPlayer || request.Expires - Time.time < requestLifetime * .5f;
                if ((priority == 0) != urgent || Time.time < request.NextAttempt) continue;
                request.NextAttempt = Time.time + (request.IsPlayer ? .05f : .15f);
                bool waiting = request.IsPlayer || Time.time + requestLifetime - request.Expires >= .65f;
                if (TryOpenNow(request.Socket, request.Actor, waiting)) { requests.RemoveAt(i--); continue; }
                // Libera únicamente conexiones sin uso y alejadas del jugador. Conserva las habitaciones con puertas abiertas.
                foreach (RoomConnection idle in connectionSnapshot)
                    if (connections.Contains(idle) && idle.Pursuers.Count == 0
                        && idle.SourceRoom != CurrentRoom && idle.TargetRoom != CurrentRoom)
                        CloseAfterPassage(idle);
                if (request.Expires - Time.time < requestLifetime - 1f)
                    RecoverIsolatedSpace(request.Socket.Room);
            }
    }
    private void RecoverIsolatedSpace(RoomModule source)
    {
        foreach (RoomModule room in roomModules)
        {
            if (room == source || !room.gameObject.activeSelf || !CanRelocate(room)
                || !parking.TryGetValue(room, out Pose pose)
                || Vector3.Distance(room.transform.position, pose.position) < .1f
                || !FitsAt(room, pose)) continue;
            RelocateIsolatedRoom(room, pose);
            ReturnUnused(room);
            return;
        }
    }
    private void TrackOccupants()
    {
        occupants.RemoveAll(o => o == null);
        foreach (RoomOccupant occupant in occupants)
        {
            foreach (RoomConnection connection in connections)
            {
                RoomModule other = occupant.Room == connection.SourceRoom ? connection.TargetRoom
                    : occupant.Room == connection.TargetRoom ? connection.SourceRoom : null;
                if (other != null && other.ContainsPoint(occupant.transform.position, .12f))
                {
                    occupant.Enter(other);
                    if (occupant.UsesNormalDoors)
                    {
                        connection.Pursuers.Remove(occupant.gameObject);
                        CloseAfterPassage(connection, occupant.gameObject);
                    }
                    break;
                }
            }
            if (player != null && occupant.transform == player) SetCurrentRoom(occupant.Room);
        }
    }
    private void SetCurrentRoom(RoomModule room)
    {
        if (room == null || room == CurrentRoom) return;
        RoomModule old = CurrentRoom;
        if (old != null) old.SetCurrent(false);
        CurrentRoom = room; room.SetCurrent(true);
        // Después del inicio, las habitaciones ocupadas conservan su posición y sus zonas de navegación.
        CurrentRoomChanged?.Invoke(old, room);
    }
    private void OnDestroy()
    {
        foreach (DoorSocket socket in socketById.Values) if (socket != null && socket.Door != null) socket.Door.Closed -= HandleDoorClosed;
        if (Instance == this) Instance = null;
    }
}
