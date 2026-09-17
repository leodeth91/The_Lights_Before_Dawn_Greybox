using UnityEngine;

/// <summary>Reproduce pasos según la distancia recorrida. Caminar suena suave e interno; correr suena más fuerte y avisa a los aliens.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController), typeof(AudioSource))]
public sealed class PlayerFootstepAudio : MonoBehaviour
{
    [Header("Replace these with imported assets later")]
    [SerializeField] private AudioClip walkingClip;
    [SerializeField] private AudioClip runningClip;

    [Header("Cadence by distance travelled")]
    [SerializeField, Min(0.2f)] private float walkingStepDistance = 0.52f;
    [SerializeField, Min(0.2f)] private float runningStepDistance = 0.68f;
    [SerializeField, Min(0.1f)] private float teleportResetDistance = 1.25f;

    [Header("Greybox mix")]
    [SerializeField, Range(0f, 1f)] private float walkingVolume = 0.16f;
    [SerializeField, Range(0f, 1f)] private float crouchingVolumeMultiplier = 0.58f;
    [SerializeField, Range(0f, 1f)] private float runningVolume = 0.52f;
    [SerializeField] private AudioSource internalWalkingSource;
    [SerializeField] private AudioSource externalRunningSource;

    private ChildPlayerController movement;
    private CharacterController character;
    private AudioClip runtimeWalkingClip;
    private AudioClip runtimeRunningClip;
    private Vector3 previousPosition;
    private float travelledSinceStep;
    private bool positionInitialized;
    private bool wasRunning;
    private readonly System.Random pitchRandom = new System.Random(20260912);

    public AudioClip WalkingClip => walkingClip != null ? walkingClip : runtimeWalkingClip;
    public AudioClip RunningClip => runningClip != null ? runningClip : runtimeRunningClip;
    public bool IsReady => WalkingClip != null && RunningClip != null
        && internalWalkingSource != null && externalRunningSource != null
        && Mathf.Approximately(internalWalkingSource.spatialBlend, 0f)
        && Mathf.Approximately(externalRunningSource.spatialBlend, 1f);

    private void Awake()
    {
        movement = GetComponent<ChildPlayerController>();
        character = GetComponent<CharacterController>();
        EnsureAudioSources();
        BuildMissingGreyboxClips();
        ResetTravelTracking();
    }

    private void LateUpdate()
    {
        if (!positionInitialized)
        {
            ResetTravelTracking();
            return;
        }

        Vector3 displacement = transform.position - previousPosition;
        previousPosition = transform.position;
        displacement.y = 0f;
        float distance = displacement.magnitude;

        // Una habitación que se mueve o una entrada al escondite pueden desplazar al jugador sin que esté dando pasos.
        if (distance > teleportResetDistance)
        {
            travelledSinceStep = 0f;
            return;
        }

        if (character == null || !character.enabled || !character.isGrounded
            || movement == null || movement.IsMovementLocked || distance < 0.0001f)
        {
            if (movement == null || !movement.IsSprinting || movement.IsMovementLocked || distance < .0001f)
                wasRunning = false;
            return;
        }

        travelledSinceStep += distance;
        bool running = movement.IsSprinting;
        bool startedRunning = running && !wasRunning;
        wasRunning = running;
        if (startedRunning)
        {
            // Una carrera corta también produce una pista. Los pasos siguientes conservan la cadencia del Inspector.
            travelledSinceStep = 0f;
            PlayRunningStep();
            return;
        }
        float stepDistance = running ? runningStepDistance : walkingStepDistance;
        if (travelledSinceStep < stepDistance)
        {
            return;
        }

        travelledSinceStep %= stepDistance;
        if (running) PlayRunningStep();
        else PlayWalkingStep();
    }

    public void PlayWalkingStep()
    {
        AudioClip clip = WalkingClip;
        if (internalWalkingSource == null || clip == null) return;
        internalWalkingSource.pitch = NextPitch(0.97f, 1.035f);
        float crouchScale = movement != null && movement.IsCrouched
            ? crouchingVolumeMultiplier : 1f;
        internalWalkingSource.PlayOneShot(clip, walkingVolume * crouchScale);
    }

    public void PlayRunningStep()
    {
        AudioClip clip = RunningClip;
        if (externalRunningSource == null || clip == null) return;
        externalRunningSource.pitch = NextPitch(0.92f, 1.08f);
        externalRunningSource.PlayOneShot(clip, runningVolume);
        RoomModule room = GetComponent<RoomOccupant>()?.Room;
        RoomModule adjacentRoom = FindClosestConnectedRoom(room);
        SoundSignalSystem.EmitSuspicious(SuspiciousSoundKind.PlayerRunningStep,
            transform.position, gameObject, room, adjacentRoom);
    }

    private RoomModule FindClosestConnectedRoom(RoomModule room)
    {
        HouseFlowController house = HouseFlowController.Instance;
        if (house == null || room == null) return null;
        RoomModule closest = null;
        float closestDoorDistance = float.PositiveInfinity;
        foreach (RoomConnection connection in house.Connections)
        {
            DoorSocket roomSocket;
            RoomModule otherRoom;
            if (connection.SourceRoom == room)
            {
                roomSocket = connection.SourceSocket;
                otherRoom = connection.TargetRoom;
            }
            else if (connection.TargetRoom == room)
            {
                roomSocket = connection.TargetSocket;
                otherRoom = connection.SourceRoom;
            }
            else continue;

            float distance = (roomSocket.Position - transform.position).sqrMagnitude;
            if (distance >= closestDoorDistance) continue;
            closestDoorDistance = distance;
            closest = otherRoom;
        }
        return closest;
    }

    public void EnsureAudioSources()
    {
        AudioSource[] existing = GetComponents<AudioSource>();
        if (internalWalkingSource == null)
        {
            internalWalkingSource = existing.Length > 0
                ? existing[0] : gameObject.AddComponent<AudioSource>();
        }
        if (externalRunningSource == null || externalRunningSource == internalWalkingSource)
        {
            externalRunningSource = existing.Length > 1
                ? existing[1] : gameObject.AddComponent<AudioSource>();
        }

        ConfigureSource(internalWalkingSource, 0f, 1f, 4f);
        ConfigureSource(externalRunningSource, 1f, 0.8f, 16f);
    }

    private static void ConfigureSource(AudioSource source, float spatialBlend,
        float minimumDistance, float maximumDistance)
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = spatialBlend;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minimumDistance;
        source.maxDistance = maximumDistance;
    }

    private void BuildMissingGreyboxClips()
    {
        if (walkingClip == null)
        {
            runtimeWalkingClip = ProceduralGreyboxAudio.CreateImpact(
                "Greybox_InternalWalk", 0.085f, 72f, 0.10f, 3.6f, 1201);
        }
        if (runningClip == null)
        {
            runtimeRunningClip = ProceduralGreyboxAudio.CreateImpact(
                "Greybox_ExternalRun", 0.13f, 86f, 0.42f, 2.7f, 1202);
        }
    }

    private float NextPitch(float minimum, float maximum)
    {
        return Mathf.Lerp(minimum, maximum, (float)pitchRandom.NextDouble());
    }

    private void ResetTravelTracking()
    {
        previousPosition = transform.position;
        travelledSinceStep = 0f;
        positionInitialized = true;
        wasRunning = false;
    }

    private void OnEnable()
    {
        ResetTravelTracking();
    }

    private void OnDisable()
    {
        travelledSinceStep = 0f;
    }

    private void OnDestroy()
    {
        if (runtimeWalkingClip != null) Destroy(runtimeWalkingClip);
        if (runtimeRunningClip != null) Destroy(runtimeRunningClip);
    }
}
