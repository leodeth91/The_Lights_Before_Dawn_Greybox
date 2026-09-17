using UnityEngine;

/// <summary>Recibe avisos de sonido y coordina a los aliens sin mezclar esa tarea con su movimiento.</summary>
[DisallowMultipleComponent]
public sealed class AlienHearing : MonoBehaviour
{
    [SerializeField, Min(1f)] private float callHearingRange = 18f;

    private AlienController controller;
    private AlienAmbientAudio voice;
    private SoundResponsibility responsibility;

    private void Awake() => ResolveComponents();

    private void ResolveComponents()
    {
        if (controller == null) controller = GetComponent<AlienController>();
        if (voice == null) voice = GetComponent<AlienAmbientAudio>();
        if (responsibility == null) responsibility = GetComponent<SoundResponsibility>();
    }

    private void OnEnable()
    {
        ResolveComponents();
        SoundSignalSystem.SuspiciousSoundEmitted -= HandleSuspiciousSound;
        SoundSignalSystem.SuspiciousSoundEmitted += HandleSuspiciousSound;
        SoundSignalSystem.CommunicationEmitted -= HandleCommunication;
        SoundSignalSystem.CommunicationEmitted += HandleCommunication;
        SoundSignalSystem.PlayerSpotted -= HandlePlayerSpotted;
        SoundSignalSystem.PlayerSpotted += HandlePlayerSpotted;
    }

    private void OnDisable()
    {
        SoundSignalSystem.SuspiciousSoundEmitted -= HandleSuspiciousSound;
        SoundSignalSystem.CommunicationEmitted -= HandleCommunication;
        SoundSignalSystem.PlayerSpotted -= HandlePlayerSpotted;
    }

    private void HandleSuspiciousSound(SoundStimulus stimulus)
    {
        ResolveComponents();
        if (controller == null || stimulus == null || stimulus.Producer == gameObject
            || !stimulus.CanBeHeardIn(controller.Occupant != null ? controller.Occupant.Room : null)) return;
        if (controller.RefreshRunningSound(stimulus)) return;
        if (!controller.CanReactToSoundStimulus(stimulus)
            || !SoundSignalSystem.TryClaimLead(stimulus, controller)) return;

        controller.BeginSoundInvestigation(stimulus);
    }

    private void HandleCommunication(AlienCommunicationSignal signal)
    {
        ResolveComponents();
        if (controller == null || signal == null || signal.Stimulus == null
            || signal.Sender == controller) return;

        if (signal.Kind == AlienCommunicationKind.Question)
        {
            if (responsibility == null
                || !responsibility.IsResponsibleFor(signal.Stimulus.Id)) return;
            voice?.PlayResponseVocalization();
            SoundSignalSystem.EmitCommunication(AlienCommunicationKind.Response,
                signal.Stimulus, controller, controller.Occupant.Room,
                controller.transform.position);
            responsibility.Acknowledge(signal.Stimulus.Id);
            return;
        }

        if (signal.Kind == AlienCommunicationKind.Response)
        {
            controller.ConfirmSoundWasFriendly(signal.Stimulus.Id);
            return;
        }

        if (signal.Kind != AlienCommunicationKind.Call || !controller.CanReactToSound)
            return;

        Vector3 senderPosition = signal.Sender != null
            ? signal.Sender.transform.position : signal.SearchWorldPosition;
        RoomModule ownRoom = controller.Occupant != null ? controller.Occupant.Room : null;
        bool canHear = ownRoom != null && (ownRoom == signal.SearchRoom
            || Vector3.Distance(controller.transform.position, senderPosition) <= callHearingRange);
        if (canHear) controller.BeginSoundAssistance(signal);
    }

    private void HandlePlayerSpotted(PlayerSpottedSignal signal)
    {
        ResolveComponents();
        if (controller == null || signal == null || signal.Sender == controller
            || !controller.CanReactToPlayerCall) return;

        RoomModule ownRoom = controller.Occupant != null ? controller.Occupant.Room : null;
        Vector3 senderPosition = signal.Sender != null
            ? signal.Sender.transform.position : signal.WorldPosition;
        bool canHear = ownRoom != null && (ownRoom == signal.Room
            || Vector3.Distance(controller.transform.position, senderPosition)
                <= callHearingRange);
        if (canHear) controller.BeginPlayerCallAssistance(signal);
    }
}
