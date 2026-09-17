using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(AlienMotor), typeof(AlienPerception), typeof(AlienHeadScanner))]
[RequireComponent(typeof(AlienMemory))]
public sealed class AlienController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(.1f)] private float patrolSpeed = .82f;
    [SerializeField, Min(.1f)] private float searchSpeed = 1.05f;
    [SerializeField, Min(.1f)] private float chaseSpeed = 2.35f;
    [Header("Player response")]
    [SerializeField] private bool enablePlayerDetection = true;
    [SerializeField, Min(.02f)] private float perceptionInterval = .10f;
    [SerializeField, Min(.1f)] private float alertDurationMin = .90f;
    [SerializeField, Min(.1f)] private float alertDurationMax = 1.15f;
    [SerializeField, Min(.1f)] private float catchDistance = .55f;
    [SerializeField, Min(2f)] private float searchDuration = 45f;
    [SerializeField, Min(.05f)] private float chaseDestinationRefresh = .15f;
    [SerializeField, Min(0f)] private float lostSightGrace = .35f;
    [Header("Inspection")]
    [SerializeField, Min(.1f)] private float inspectionDurationMin = 3f;
    [SerializeField, Min(.1f)] private float inspectionDurationMax = 6f;
    [Header("Sound investigation")]
    [SerializeField, Min(.1f)] private float turnTowardSoundDuration = .65f;
    [SerializeField] private Vector2 responseWaitRange = new Vector2(.9f, 1.25f);
    [SerializeField] private Vector2 soundSearchDuration = new Vector2(4f, 8f);
    [SerializeField, Min(.1f)] private float assistanceSpeed = 1.45f;
    [SerializeField, Min(.5f)] private float soundSearchRadius = 2.2f;
    [Header("Events and debug")]
    [SerializeField] private UnityEvent<string> onStateChanged = new UnityEvent<string>();
    [SerializeField] private UnityEvent onAlertStarted = new UnityEvent();
    [SerializeField] private bool verboseLogging;

    private readonly Dictionary<AlienInterest, float> cooldowns = new Dictionary<AlienInterest, float>();
    private readonly AlienState wander = new AlienWanderState(), scan = new AlienScanState(),
        approach = new AlienApproachState(), inspect = new AlienInspectState(), traverse = new AlienTraverseState(),
        alert = new AlienAlertState(), chase = new AlienChaseState(), search = new AlienSearchState(),
        investigateKnownHide = new AlienInvestigateKnownHideSpotState(), catchPlayer = new AlienCatchPlayerState();
    private Coroutine behaviourRoutine;
    private PlayerVisibility player;
    private bool playerSubscribed;
    private float nextPerceptionTime;
    private bool sensesEnabled;
    private bool hearingEnabled = true;
    private bool traversingDoor;
    private bool soundReactionActive;
    private bool playerCallReactionActive;
    private SoundStimulus activeSound;
    private SoundStimulus latestRunningSound;

    public bool RefreshRunningSound(SoundStimulus stimulus)
    {
        if (!enabled || !hearingEnabled || !soundReactionActive || PlayerVisible
            || stimulus.Kind != SuspiciousSoundKind.PlayerRunningStep) return false;
        // Actualiza la pista sin reiniciar la pregunta con cada paso ni crear otra investigación.
        latestRunningSound = stimulus;
        FocusPoint = stimulus.PositionFor(Occupant.Room);
        return true;
    }
    private int friendlyResponseSoundId;
    private readonly List<AlienInterest> expiredInterests = new List<AlienInterest>();
    private AlienActivityDirector activityDirector;
    private AlienActivityTuning activity = new AlienActivityTuning(
        AlienActivityPhase.Cautious, new Vector2(2f, 4f), new Vector2(3.5f, 4.5f),
        1f, 1f, 1f, 1f, 1f, 12f, .1f);
    private int routineChoicesWithoutHideSpot;
    private int routineChoicesWithoutPassage;

    public readonly List<AlienInterest> Visible = new List<AlienInterest>();
    public AlienMotor Motor { get; private set; }
    public AlienPerception Perception { get; private set; }
    public AlienHeadScanner Head { get; private set; }
    public AlienMemory Memory { get; private set; }
    public AlienAmbientAudio Voice { get; private set; }
    public RoomOccupant Occupant { get; private set; }
    public AlienInterest Target { get; set; }
    public IInspectable Inspection { get; set; }
    public RoomConnection Passage { get; set; }
    public bool Scanning { get; set; }
    public bool PlayerVisible { get; private set; }
    public bool HasRecentVisualContact => PlayerVisible
        || (Memory != null && Memory.TimeSinceLastSeen <= lostSightGrace);
    public bool CatchRequested { get; private set; }
    public Vector3? FocusPoint { get; set; }
    public AlienState CurrentState { get; private set; }
    public string StateName { get; private set; } = "Espera";
    public PlayerVisibility Player => player;
    public float PatrolSpeed => patrolSpeed * activity.PatrolSpeedMultiplier;
    public float SearchSpeed => searchSpeed * activity.SearchSpeedMultiplier;
    public float ChaseSpeed => chaseSpeed;
    public float CatchDistance => catchDistance;
    public float SearchDuration => searchDuration;
    public float ChaseDestinationRefresh => chaseDestinationRefresh;
    public Vector2 WanderDuration => activity.WanderDuration;
    public Vector2 ScanDuration => activity.ScanDuration;
    public float HideSpotInterestWeight => activity.HideSpotWeight
        * (1f + Mathf.Min(5, routineChoicesWithoutHideSpot) * activity.AlternationPressure);
    public float PassageInterestWeight => activity.PassageWeight
        * (1f + Mathf.Min(5, routineChoicesWithoutPassage) * activity.AlternationPressure);
    public float InspectionDuration => Random.Range(inspectionDurationMin,
        Mathf.Max(inspectionDurationMin, inspectionDurationMax))
        * activity.InspectionDurationMultiplier;
    public float AlertDuration => Random.Range(alertDurationMin,
        Mathf.Max(alertDurationMin, alertDurationMax));
    public UnityEvent<string> OnStateChanged => onStateChanged;
    public UnityEvent OnAlertStarted => onAlertStarted;
    public bool CanReactToSound => enabled && gameObject.activeInHierarchy && sensesEnabled
        && hearingEnabled
        && !traversingDoor && !soundReactionActive && !PlayerVisible
        && !(CurrentState is AlienAlertState) && !(CurrentState is AlienChaseState)
        && !(CurrentState is AlienSearchState) && !(CurrentState is AlienCatchPlayerState)
        && !(CurrentState is AlienInvestigateKnownHideSpotState)
        && !(CurrentState is AlienTraverseState);
    public bool CanReactToSoundStimulus(SoundStimulus stimulus)
    {
        if (stimulus == null || !enabled || !gameObject.activeInHierarchy
            || !sensesEnabled || !hearingEnabled || traversingDoor
            || soundReactionActive || PlayerVisible
            || CurrentState is AlienAlertState || CurrentState is AlienChaseState
            || CurrentState is AlienCatchPlayerState
            || CurrentState is AlienInvestigateKnownHideSpotState
            || CurrentState is AlienTraverseState) return false;

        // Escuchar al jugador correr aporta una pista más reciente que un punto de búsqueda.
        return !(CurrentState is AlienSearchState)
            || stimulus.Kind == SuspiciousSoundKind.PlayerRunningStep;
    }
    public bool CanReactToPlayerCall => enabled && gameObject.activeInHierarchy
        && sensesEnabled && hearingEnabled && !traversingDoor && !playerCallReactionActive
        && !PlayerVisible && !(CurrentState is AlienAlertState)
        && !(CurrentState is AlienChaseState) && !(CurrentState is AlienCatchPlayerState)
        && !(CurrentState is AlienInvestigateKnownHideSpotState)
        && !(CurrentState is AlienTraverseState);
    public AlienController LastPlayerCallSender { get; private set; }
    public bool AllowsRoamingVocalization => sensesEnabled && !soundReactionActive
        && !playerCallReactionActive
        && !PlayerVisible && !(CurrentState is AlienAlertState)
        && !(CurrentState is AlienChaseState) && !(CurrentState is AlienCatchPlayerState);

    private void Awake()
    {
        Motor = GetComponent<AlienMotor>();
        Perception = GetComponent<AlienPerception>();
        Head = GetComponent<AlienHeadScanner>();
        Memory = GetComponent<AlienMemory>();
        Occupant = GetComponent<RoomOccupant>();
        Voice = GetComponent<AlienAmbientAudio>();
        if (GetComponent<SoundResponsibility>() == null)
            gameObject.AddComponent<SoundResponsibility>();
        if (GetComponent<AlienHearing>() == null)
            gameObject.AddComponent<AlienHearing>();
    }

    private IEnumerator Start()
    {
        BindActivityDirector();
        ResolvePlayer();
        while (GameSessionManager.Instance != null && !GameSessionManager.Instance.IsPlaying)
            yield return null;
        yield return new WaitForSeconds(3f);
        sensesEnabled = true;
        StartBehaviour(OrdinaryLoop());
    }

    private void ResolvePlayer()
    {
        if (player == null) player = FindFirstObjectByType<PlayerVisibility>();
        if (!playerSubscribed && player != null && player.HidingController != null)
        {
            player.HidingController.StateChanged -= HandlePlayerHideStateChanged;
            player.HidingController.StateChanged += HandlePlayerHideStateChanged;
            playerSubscribed = true;
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (activityDirector == null) BindActivityDirector();
        ResolvePlayer();
        if (enablePlayerDetection && sensesEnabled && Time.time >= nextPerceptionTime) SensePlayer();
        Vector3? lookPoint = FocusPoint;
        if (!lookPoint.HasValue && Target != null) lookPoint = Target.LookPoint;
        if (Head != null) Head.Look(Scanning, lookPoint);
    }

    private void SensePlayer()
    {
        nextPerceptionTime = Time.time + perceptionInterval;
        HouseFlowController house = HouseFlowController.Instance;
        bool visibleNow = player != null && Perception.CanSeePlayer(player, Head.Head,
            Occupant != null ? Occupant.Room : null, house);
        PlayerVisible = visibleNow;
        if (!visibleNow) return;
        RoomOccupant playerOccupant = player.GetComponent<RoomOccupant>();
        Memory.Observe(player, playerOccupant != null ? playerOccupant.Room : null);
        if (playerOccupant != null) house.RememberPursuit(gameObject, playerOccupant.Room);
        if (traversingDoor) return; // Termina de cruzar el marco antes de cambiar de acción.
        if (CurrentState is AlienAlertState || CurrentState is AlienChaseState
            || CurrentState is AlienCatchPlayerState) return;
        // Si ya estaba buscando al jugador, reconocerlo no necesita otra pausa de sorpresa.
        StartBehaviour(PlayerResponseLoop(CurrentState is AlienSearchState
            || CurrentState is AlienInvestigateKnownHideSpotState));
    }

    private void HandlePlayerHideStateChanged(PlayerHideState state, HideSpot spot)
    {
        if (!enablePlayerDetection || !sensesEnabled || state != PlayerHideState.Entering || spot == null
            || player == null) return;
        HouseFlowController house = HouseFlowController.Instance;
        if (!Perception.CanSeePlayer(player, Head.Head, Occupant.Room, house)) return;
        RoomOccupant playerOccupant = player.GetComponent<RoomOccupant>();
        Memory.Observe(player, playerOccupant != null ? playerOccupant.Room : null);
        Memory.RememberHidingSpot(spot);
        StartBehaviour(KnownHidingSpotLoop());
    }

    private void StartBehaviour(IEnumerator routine)
    {
        if (behaviourRoutine != null) StopCoroutine(behaviourRoutine);
        CleanupInterruptedActivity();
        behaviourRoutine = StartCoroutine(routine);
    }

    // State separa las acciones: caminar, observar, acercarse y revisar. Al detectar al Player se interrumpe este ciclo para darle prioridad.
    private IEnumerator OrdinaryLoop()
    {
        while (GameSessionManager.Instance == null || GameSessionManager.Instance.IsPlaying)
        {
            HouseFlowController.Instance?.ForgetPursuit(gameObject);
            expiredInterests.Clear();
            foreach (var entry in cooldowns)
                if (entry.Key == null || entry.Value <= Time.time) expiredInterests.Add(entry.Key);
            foreach (AlienInterest interest in expiredInterests) cooldowns.Remove(interest);
            Target = null;
            // Al terminar una búsqueda no debe seguir mirando su última pared o punto recordado.
            FocusPoint = null;
            Scanning = false;
            yield return Run(wander);
            yield return Run(scan);
            if (Target == null) continue;
            yield return Run(approach);
            if (Motor.Arrived && Target != null && Target.Available)
                yield return Run(Target.Kind == AlienInterestKind.Passage ? traverse : inspect);
            if (CatchRequested)
            {
                yield return Run(catchPlayer);
                yield break;
            }
            if (Target != null)
            {
                RegisterRoutineChoice(Target.Kind);
                cooldowns[Target] = Time.time + activity.InterestCooldown;
            }
            Target = null;
        }
    }

    private IEnumerator PlayerResponseLoop(bool resumeChase = false)
    {
        CatchRequested = false;
        if (!resumeChase) yield return Run(alert);
        if (Memory.KnownHidingSpot != null)
            yield return Run(investigateKnownHide);
        else if (Memory.HasLastKnownPosition)
            yield return Run(chase);
        if (CatchRequested)
        {
            yield return Run(catchPlayer);
            yield break;
        }
        if (GameSessionManager.Instance != null && !GameSessionManager.Instance.IsPlaying) yield break;
        if (Memory.KnownHidingSpot != null)
            yield return Run(investigateKnownHide);
        if (CatchRequested)
        {
            yield return Run(catchPlayer);
            yield break;
        }
        if (GameSessionManager.Instance != null && !GameSessionManager.Instance.IsPlaying) yield break;
        yield return Run(search);
        if (CatchRequested)
        {
            yield return Run(catchPlayer);
            yield break;
        }
        Memory.ClearSearch();
        yield return OrdinaryLoop();
    }

    private IEnumerator KnownHidingSpotLoop()
    {
        CatchRequested = false;
        yield return Run(investigateKnownHide);
        if (CatchRequested)
        {
            yield return Run(catchPlayer);
            yield break;
        }
        if (GameSessionManager.Instance != null && !GameSessionManager.Instance.IsPlaying) yield break;
        yield return Run(search);
        if (CatchRequested)
        {
            yield return Run(catchPlayer);
            yield break;
        }
        Memory.ClearSearch();
        yield return OrdinaryLoop();
    }

    public void BeginSoundInvestigation(SoundStimulus stimulus)
    {
        if (!CanReactToSoundStimulus(stimulus)) return;
        StartBehaviour(SoundInvestigationLoop(stimulus));
    }

    public void BeginSoundAssistance(AlienCommunicationSignal signal)
    {
        if (signal == null || signal.Stimulus == null || !CanReactToSound) return;
        StartBehaviour(SoundAssistanceLoop(signal));
    }

    public void ConfirmSoundWasFriendly(int soundId)
    {
        if (soundReactionActive && activeSound != null && activeSound.Id == soundId)
            friendlyResponseSoundId = soundId;
    }

    public void BeginPlayerCallAssistance(PlayerSpottedSignal signal)
    {
        if (signal == null || !CanReactToPlayerCall) return;
        StartBehaviour(PlayerCallAssistanceLoop(signal));
    }

    private IEnumerator PlayerCallAssistanceLoop(PlayerSpottedSignal signal)
    {
        playerCallReactionActive = true;
        LastPlayerCallSender = signal.Sender;
        SetActivityState("Acude al jugador detectado");
        FocusPoint = signal.WorldPosition;
        Motor.SetSpeed(ChaseSpeed);
        if (signal.Room != null && Occupant.Room != signal.Room)
            yield return TravelToRoom(signal.Room);
        if (signal.Room != null && Occupant.Room == signal.Room)
            yield return SearchSoundArea(signal.Room, signal.WorldPosition, ChaseSpeed);
        playerCallReactionActive = false;
        if (IsSessionRunning()) yield return OrdinaryLoop();
    }

    private IEnumerator SoundInvestigationLoop(SoundStimulus stimulus)
    {
        soundReactionActive = true;
        activeSound = stimulus;
        latestRunningSound = stimulus.Kind == SuspiciousSoundKind.PlayerRunningStep ? stimulus : null;
        friendlyResponseSoundId = 0;
        SetActivityState("Escucha un ruido");
        Motor.Stop();
        RoomModule room = Occupant != null ? Occupant.Room : null;
        Vector3 target = stimulus.PositionFor(room);
        FocusPoint = target;

        float turnDuration = stimulus.Kind == SuspiciousSoundKind.PlayerRunningStep
            ? Mathf.Min(turnTowardSoundDuration, .30f) : turnTowardSoundDuration;
        float turnUntil = Time.time + turnDuration;
        while (Time.time < turnUntil && IsSessionRunning())
        {
            TurnTowardSound();
            yield return null;
        }

        if (!soundReactionActive || activeSound != stimulus) yield break;
        SetActivityState("Pregunta por el ruido");
        Voice?.PlayQuestionVocalization();
        SoundSignalSystem.EmitCommunication(AlienCommunicationKind.Question,
            stimulus, this, room, target);

        float waitMinimum = Mathf.Max(.1f, responseWaitRange.x);
        float waitMaximum = Mathf.Max(waitMinimum, responseWaitRange.y);
        float responseDeadline = Time.time + Random.Range(waitMinimum, waitMaximum);
        while (Time.time < responseDeadline && friendlyResponseSoundId != stimulus.Id
            && IsSessionRunning())
        {
            TurnTowardSound();
            yield return null;
        }

        if (friendlyResponseSoundId == stimulus.Id)
        {
            EndSoundReaction();
            yield return OrdinaryLoop();
            yield break;
        }

        SetActivityState("Llama a su compañero");
        Voice?.PlayCallVocalization();
        room = Occupant != null ? Occupant.Room : room;
        target = (latestRunningSound ?? stimulus).PositionFor(room);
        SoundSignalSystem.EmitCommunication(AlienCommunicationKind.Call,
            stimulus, this, room, target);
        yield return SearchSoundArea(room, target, SearchSpeed);
        EndSoundReaction();
        if (IsSessionRunning()) yield return OrdinaryLoop();
    }

    private IEnumerator SoundAssistanceLoop(AlienCommunicationSignal signal)
    {
        soundReactionActive = true;
        activeSound = signal.Stimulus;
        friendlyResponseSoundId = 0;
        SetActivityState("Acude a la llamada");
        RoomModule destinationRoom = signal.SearchRoom;
        FocusPoint = signal.SearchWorldPosition;
        Motor.SetSpeed(assistanceSpeed);
        if (destinationRoom != null && Occupant.Room != destinationRoom)
            yield return TravelToRoom(destinationRoom);
        if (destinationRoom == null || Occupant.Room == destinationRoom)
            yield return SearchSoundArea(destinationRoom, signal.SearchWorldPosition,
                assistanceSpeed);
        EndSoundReaction();
        if (IsSessionRunning()) yield return OrdinaryLoop();
    }

    private IEnumerator SearchSoundArea(RoomModule room, Vector3 center, float speed)
    {
        if (room == null || Occupant == null) yield break;
        if (Occupant.Room != room) yield return TravelToRoom(room);
        if (Occupant.Room != room) yield break;

        SetActivityState("Investiga el sonido");
        Motor.SetSpeed(speed);
        center = room == Occupant.Room ? center : transform.position;
        FocusPoint = center;
        yield return Motor.Go(center, 8f);

        float minimum = Mathf.Max(.5f, soundSearchDuration.x);
        float maximum = Mathf.Max(minimum, soundSearchDuration.y);
        float end = Time.time + Random.Range(minimum, maximum);
        while (Time.time < end && IsSessionRunning())
        {
            Scanning = true;
            FocusPoint = null;
            Vector2 offset = Random.insideUnitCircle * soundSearchRadius;
            Vector3 point = center + new Vector3(offset.x, 0f, offset.y);
            Motor.SetDestination(point, 1f, room);
            float moveUntil = Mathf.Min(end, Time.time + Random.Range(.75f, 1.35f));
            while (Time.time < moveUntil && IsSessionRunning()) yield return null;
            Motor.Stop();
            Scanning = false;
            FocusPoint = center;
            float observeUntil = Mathf.Min(end, Time.time + Random.Range(.35f, .75f));
            while (Time.time < observeUntil && IsSessionRunning()) yield return null;
        }
        Motor.Stop();
        Scanning = false;
    }

    private bool IsSessionRunning()
        => GameSessionManager.Instance == null || GameSessionManager.Instance.IsPlaying;

    private void SetActivityState(string activity)
    {
        CurrentState = null;
        StateName = activity;
        onStateChanged.Invoke(StateName);
        if (verboseLogging) Debug.Log(name + " -> " + StateName, this);
    }

    private void TurnTowardSound()
    {
        if (!FocusPoint.HasValue) return;
        Vector3 direction = FocusPoint.Value - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < .001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(direction), 180f * Time.deltaTime);
    }

    private void EndSoundReaction()
    {
        if (activeSound != null) SoundSignalSystem.ReleaseLead(activeSound, this);
        soundReactionActive = false;
        activeSound = null;
        latestRunningSound = null;
        friendlyResponseSoundId = 0;
    }

    private IEnumerator Run(AlienState state)
    {
        CurrentState = state;
        StateName = state.Name;
        onStateChanged.Invoke(StateName);
        if (verboseLogging) Debug.Log(name + " -> " + StateName, this);
        yield return state.Execute(this);
        if (CurrentState == state) CurrentState = null;
    }

    public void RequestCatch() => CatchRequested = true;

    public IEnumerator TravelToRoom(RoomModule destinationRoom)
    {
        if (destinationRoom == null || Occupant == null || Occupant.Room == destinationRoom)
            yield break;
        HouseFlowController house = HouseFlowController.Instance;
        if (house == null) yield break;
        // Calcula el camino con las puertas conocidas. El destino sale de la memoria del alien, sin consultar la posición actual del jugador.
        var attempted = new HashSet<DoorSocket>();
        for (int hop = 0; hop < house.ModuleById.Count && Occupant.Room != destinationRoom; hop++)
        {
            DoorSocket socket = NextRouteDoor(destinationRoom, attempted);
            if (socket == null) yield break;
            attempted.Add(socket);
            yield return CrossDoor(socket);
        }
    }

    private DoorSocket NextRouteDoor(RoomModule destination, HashSet<DoorSocket> attempted)
    {
        HouseFlowController house = HouseFlowController.Instance;
        var frontier = new Queue<RoomModule>();
        var firstDoor = new Dictionary<RoomModule, DoorSocket>();
        frontier.Enqueue(Occupant.Room);
        firstDoor[Occupant.Room] = null;
        while (frontier.Count > 0)
        {
            RoomModule room = frontier.Dequeue();
            foreach (DoorSocket socket in room.Sockets)
            {
                if (attempted.Contains(socket) || IsCoolingDown(socket.GetComponent<DoorInterest>())) continue;
                RoomConnection connection = house.ConnectionFor(socket);
                RoomModule next;
                if (connection != null)
                {
                    if (connection.IsAnomalous && !house.CanFollow(connection, gameObject)) continue;
                    next = connection.SourceRoom == room ? connection.TargetRoom : connection.SourceRoom;
                }
                else next = house.NormalDestination(socket)?.Room;
                if (next == null || firstDoor.ContainsKey(next)) continue;
                firstDoor[next] = firstDoor[room] != null ? firstDoor[room] : socket;
                if (next == destination) return firstDoor[next];
                frontier.Enqueue(next);
            }
        }
        return null;
    }

    public IEnumerator CrossDoor(DoorSocket socket)
    {
        HouseFlowController house = HouseFlowController.Instance;
        if (house == null || socket == null || Occupant.Room != socket.Room) yield break;
        RoomModule origin = Occupant.Room;
        traversingDoor = true;
        try
        {
            for (int attempt = 0; attempt < 3 && Occupant.Room == origin; attempt++)
            {
                Vector3 approach = socket.ClearApproachPoint
                    + socket.transform.right * (attempt == 0 ? 0f : attempt == 1 ? .4f : -.4f);
                yield return Motor.Go(approach, 4f);
                if (!Motor.Arrived) continue;
                house.RequestOpen(socket, gameObject);
                float deadline = Time.time + 3.5f;
                RoomConnection connection = null;
                while (Time.time < deadline)
                {
                    connection = house.ConnectionFor(socket);
                    if (connection != null && connection.SourceSocket.Door.IsOpen) break;
                    yield return null;
                }
                house.CancelDoorRequest(gameObject);
                if (connection == null || !connection.SourceSocket.Door.IsOpen
                    || (connection.IsAnomalous && !house.CanFollow(connection, gameObject))
                    || !house.ReservePassage(connection, gameObject)) continue;
                Passage = connection;
                yield return null;
                DoorSocket exit = connection.SourceSocket == socket
                    ? connection.TargetSocket : connection.SourceSocket;
                yield return Motor.Go(exit.ClearApproachPoint, 6f);
                if (Motor.Arrived) Occupant.Enter(exit.Room);
                if (!Motor.Arrived)
                {
                    DoorSocket safeSide = Occupant.Room == exit.Room ? exit : socket;
                    yield return Motor.Go(safeSide.ClearApproachPoint, 3f);
                }
                house.ReleasePassage(connection, gameObject);
                Passage = null;
                if (Occupant.Room != origin) connection.Pursuers.Remove(gameObject);
                // Pide cerrar inmediatamente después de liberar su reserva.
                // Si otro personaje sigue en el marco, la casa conserva la orden hasta que salga.
                house.RequestClose(connection.SourceSocket, gameObject);
                deadline = Time.time + 1.5f;
                while (house.ConnectionFor(socket) == connection
                    && connection.SourceSocket.Door.State != DoorState.Closed && Time.time < deadline)
                    yield return null;
                if (Occupant.Room != origin) yield break;
                yield return new WaitForSeconds(.25f + attempt * .2f);
            }
        }
        finally
        {
            house.CancelDoorRequest(gameObject);
            if (Passage != null)
            {
                house.ReleasePassage(Passage, gameObject);
                house.CloseAfterPassage(Passage, gameObject);
                Passage = null;
            }
            traversingDoor = false;
            if (Occupant.Room == origin && socket != null)
            {
                DoorInterest interest = socket.GetComponent<DoorInterest>();
                if (interest != null) cooldowns[interest] = Time.time + 8f;
            }
        }
    }

    public void SetPlayerDetectionEnabled(bool enabled)
    {
        enablePlayerDetection = enabled;
        if (!enabled) PlayerVisible = false;
    }

    public void SetHearingEnabled(bool enabled)
    {
        hearingEnabled = enabled;
        if (!enabled && soundReactionActive)
        {
            StartBehaviour(OrdinaryLoop());
        }
    }

    public bool IsCoolingDown(AlienInterest interest)
        => interest != null && cooldowns.TryGetValue(interest, out float until) && Time.time < until;

    private void BindActivityDirector()
    {
        AlienActivityDirector director = AlienActivityDirector.Instance;
        if (activityDirector == director) return;
        if (activityDirector != null) activityDirector.ActivityChanged -= HandleActivityChanged;
        activityDirector = director;
        if (activityDirector == null) return;
        activityDirector.ActivityChanged += HandleActivityChanged;
        HandleActivityChanged(activityDirector.Current);
    }

    private void HandleActivityChanged(AlienActivityTuning tuning)
    {
        // El evento cambia los valores que usarán los estados en sus próximos pasos.
        // No corta una persecución, una inspección ni un cruce que estén en marcha.
        activity = tuning;
    }

    private void RegisterRoutineChoice(AlienInterestKind kind)
    {
        // Si repitió mucho una clase de decisión, la otra gana peso en el próximo
        // ciclo. Esto genera recorridos variados sin revelar dónde está el jugador.
        routineChoicesWithoutHideSpot = kind == AlienInterestKind.HideSpot
            ? 0 : routineChoicesWithoutHideSpot + 1;
        routineChoicesWithoutPassage = kind == AlienInterestKind.Passage
            ? 0 : routineChoicesWithoutPassage + 1;
    }

    public void NotifyAlertStarted()
    {
        Voice?.PlayCallVocalization();
        RoomModule room = Occupant != null ? Occupant.Room : null;
        Vector3 lastSeen = Memory != null && Memory.HasLastKnownPosition
            ? Memory.LastKnownPosition : transform.position;
        SoundSignalSystem.EmitPlayerSpotted(this, room, lastSeen);
        onAlertStarted.Invoke();
    }


    public void NotifyChaseStarted() => Voice?.PlayChaseVocalization();

    /// <summary>Todos los estados revisan el escondite de la misma manera: comprueban si está ocupado, sin importar por qué llegaron hasta él.</summary>
    public bool TryBeginInspection(IInspectable inspectable)
    {
        if (inspectable == null || !inspectable.TryBeginInspection(gameObject)) return false;
        Inspection = inspectable;
        if (inspectable is HideSpot spot && spot.IsOccupied) RequestCatch();
        return true;
    }

    public void FinishInspection()
    {
        IInspectable inspection = Inspection;
        Inspection = null;
        if (inspection is Object unityObject && unityObject == null) return;
        inspection?.EndInspection(gameObject);
    }

    private void CleanupInterruptedActivity()
    {
        EndSoundReaction();
        playerCallReactionActive = false;
        FinishInspection();
        Scanning = false;
        FocusPoint = null;
        Motor?.Stop();
        traversingDoor = false;
        HouseFlowController.Instance?.CancelDoorRequest(gameObject);
        if (Passage != null && HouseFlowController.Instance != null)
        {
            HouseFlowController.Instance.ReleasePassage(Passage, gameObject);
            HouseFlowController.Instance.CloseAfterPassage(Passage, gameObject);
        }
        Passage = null;
    }

    private void OnDisable()
    {
        if (activityDirector != null) activityDirector.ActivityChanged -= HandleActivityChanged;
        activityDirector = null;
        if (player != null && player.HidingController != null)
            player.HidingController.StateChanged -= HandlePlayerHideStateChanged;
        playerSubscribed = false;
        CleanupInterruptedActivity();
        HouseFlowController.Instance?.ForgetPursuit(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        AlienPerception perception = Perception != null ? Perception : GetComponent<AlienPerception>();
        AlienHeadScanner scanner = Head != null ? Head : GetComponent<AlienHeadScanner>();
        if (perception == null || scanner == null || scanner.Head == null) return;
        Transform eyes = scanner.Head;
        float half = perception.FieldOfView * .5f;
        Gizmos.color = PlayerVisible ? Color.red : new Color(1f, .7f, .1f, .75f);
        Gizmos.DrawRay(eyes.position, Quaternion.AngleAxis(-half, Vector3.up) * eyes.forward * perception.Range);
        Gizmos.DrawRay(eyes.position, Quaternion.AngleAxis(half, Vector3.up) * eyes.forward * perception.Range);
        if (FocusPoint.HasValue) Gizmos.DrawLine(eyes.position, FocusPoint.Value);
    }
}
