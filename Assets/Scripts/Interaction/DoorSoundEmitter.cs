using UnityEngine;

/// <summary>Reproduce el sonido provisional de la puerta y envía un aviso para que los aliens puedan escucharlo.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DoorInteractable), typeof(AudioSource))]
public sealed class DoorSoundEmitter : MonoBehaviour
{
    [Header("Replace these with imported assets later")]
    [SerializeField] private AudioClip openingClip;
    [SerializeField] private AudioClip closingClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.42f;
    [SerializeField, Min(0.1f)] private float responsibilityDuration = 2.75f;
    [SerializeField] private AudioSource source;

    private DoorInteractable door;
    private static AudioClip runtimeOpeningClip;
    private static AudioClip runtimeClosingClip;

    public void Bind(DoorInteractable target)
    {
        if (door == target && door != null) return;
        Unbind();
        door = target != null ? target : GetComponent<DoorInteractable>();
        EnsureSource();
        if (door == null) return;
        door.OpeningStarted += HandleOpening;
        door.ClosingStartedBy += HandleClosing;
    }

    private void Awake() => Bind(GetComponent<DoorInteractable>());
    private void OnEnable() => Bind(GetComponent<DoorInteractable>());
    private void OnDisable() => Unbind();

    private void Unbind()
    {
        if (door == null) return;
        door.OpeningStarted -= HandleOpening;
        door.ClosingStartedBy -= HandleClosing;
        door = null;
    }

    private void HandleOpening(DoorInteractable openedDoor, GameObject actor)
        => PlayAndPublish(openedDoor, actor, true);

    private void HandleClosing(DoorInteractable closedDoor, GameObject actor)
        => PlayAndPublish(closedDoor, actor, false);

    private void PlayAndPublish(DoorInteractable changedDoor, GameObject actor, bool opening)
    {
        EnsureSource();
        AudioClip clip = opening ? OpeningClip : ClosingClip;
        if (source != null && clip != null)
        {
            source.pitch = opening ? Random.Range(.94f, 1.03f) : Random.Range(.88f, .98f);
            source.PlayOneShot(clip, volume);
        }

        DoorSocket socket = changedDoor != null ? changedDoor.Socket : null;
        RoomModule primary = socket != null ? socket.Room : GetComponentInParent<RoomModule>();
        RoomModule secondary = null;
        HouseFlowController house = HouseFlowController.Instance;
        RoomConnection connection = house != null && socket != null
            ? house.ConnectionFor(socket) : null;
        if (connection != null)
            secondary = connection.SourceRoom == primary
                ? connection.TargetRoom : connection.SourceRoom;

        SoundSignalSystem.EmitSuspicious(opening
                ? SuspiciousSoundKind.DoorOpening : SuspiciousSoundKind.DoorClosing,
            transform.position, actor, primary, secondary, responsibilityDuration);
    }

    private AudioClip OpeningClip
    {
        get
        {
            if (openingClip != null) return openingClip;
            if (runtimeOpeningClip == null)
                runtimeOpeningClip = ProceduralGreyboxAudio.CreateImpact(
                    "Greybox_DoorOpen", .24f, 54f, .18f, 1.65f, 3101);
            return runtimeOpeningClip;
        }
    }

    private AudioClip ClosingClip
    {
        get
        {
            if (closingClip != null) return closingClip;
            if (runtimeClosingClip == null)
                runtimeClosingClip = ProceduralGreyboxAudio.CreateImpact(
                    "Greybox_DoorClose", .19f, 46f, .34f, 2.15f, 3102);
            return runtimeClosingClip;
        }
    }

    public void EnsureSource()
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = .8f;
        source.maxDistance = 13f;
    }
}
