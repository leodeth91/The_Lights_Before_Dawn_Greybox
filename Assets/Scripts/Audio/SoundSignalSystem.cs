using System;
using System.Collections.Generic;
using UnityEngine;

public enum SuspiciousSoundKind
{
    PlayerRunningStep,
    DoorOpening,
    DoorClosing
}

public enum AlienCommunicationKind
{
    Question,
    Response,
    Call
}

/// <summary>Representa un sonido que la IA puede investigar durante un tiempo limitado. Su posición se guarda dentro de la habitación para acompañarla si se mueve.</summary>
public sealed class SoundStimulus
{
    public int Id { get; }
    public SuspiciousSoundKind Kind { get; }
    public GameObject Producer { get; }
    public RoomModule PrimaryRoom { get; }
    public RoomModule SecondaryRoom { get; }
    public float CreatedAt { get; }

    private readonly Vector3 primaryLocalPosition;
    private readonly Vector3 secondaryLocalPosition;

    internal SoundStimulus(int id, SuspiciousSoundKind kind, Vector3 worldPosition,
        GameObject producer, RoomModule primaryRoom, RoomModule secondaryRoom)
    {
        Id = id;
        Kind = kind;
        Producer = producer;
        PrimaryRoom = primaryRoom;
        SecondaryRoom = secondaryRoom;
        CreatedAt = Time.time;
        primaryLocalPosition = primaryRoom != null
            ? primaryRoom.transform.InverseTransformPoint(worldPosition) : worldPosition;
        secondaryLocalPosition = secondaryRoom != null
            ? secondaryRoom.transform.InverseTransformPoint(worldPosition) : worldPosition;
    }

    public bool CanBeHeardIn(RoomModule room)
        => room != null && (room == PrimaryRoom || room == SecondaryRoom);

    public Vector3 PositionFor(RoomModule room)
    {
        if (room != null && room == SecondaryRoom)
            return room.transform.TransformPoint(secondaryLocalPosition);
        if (PrimaryRoom != null)
            return PrimaryRoom.transform.TransformPoint(primaryLocalPosition);
        return primaryLocalPosition;
    }
}

public sealed class AlienCommunicationSignal
{
    public AlienCommunicationKind Kind { get; }
    public SoundStimulus Stimulus { get; }
    public AlienController Sender { get; }
    public RoomModule SearchRoom { get; }
    public Vector3 SearchLocalPosition { get; }

    internal AlienCommunicationSignal(AlienCommunicationKind kind, SoundStimulus stimulus,
        AlienController sender, RoomModule searchRoom, Vector3 worldSearchPosition)
    {
        Kind = kind;
        Stimulus = stimulus;
        Sender = sender;
        SearchRoom = searchRoom;
        SearchLocalPosition = searchRoom != null
            ? searchRoom.transform.InverseTransformPoint(worldSearchPosition)
            : worldSearchPosition;
    }

    public Vector3 SearchWorldPosition => SearchRoom != null
        ? SearchRoom.transform.TransformPoint(SearchLocalPosition) : SearchLocalPosition;
}

/// <summary>Guarda dónde vio al jugador un compañero por última vez, tomando la habitación como referencia.</summary>
public sealed class PlayerSpottedSignal
{
    public AlienController Sender { get; }
    public RoomModule Room { get; }
    public Vector3 LocalPosition { get; }

    internal PlayerSpottedSignal(AlienController sender, RoomModule room,
        Vector3 worldPosition)
    {
        Sender = sender;
        Room = room;
        LocalPosition = room != null
            ? room.transform.InverseTransformPoint(worldPosition) : worldPosition;
    }

    public Vector3 WorldPosition => Room != null
        ? Room.transform.TransformPoint(LocalPosition) : LocalPosition;
}

/// <summary>Distribuye los avisos de sonido. Separa los ruidos sospechosos de las preguntas y respuestas para que los aliens no se alerten sin parar entre sí.</summary>
public static class SoundSignalSystem
{
    private static int nextSoundId = 1;
    private static readonly Dictionary<int, AlienController> leadInvestigators
        = new Dictionary<int, AlienController>();

    // Observer: emitir un ruido avisa a los oyentes registrados. Caminar no emite este aviso; correr y las puertas sí.
    public static event Action<SoundStimulus> SuspiciousSoundEmitted;
    public static event Action<AlienCommunicationSignal> CommunicationEmitted;
    public static event Action<PlayerSpottedSignal> PlayerSpotted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        nextSoundId = 1;
        leadInvestigators.Clear();
        SuspiciousSoundEmitted = null;
        CommunicationEmitted = null;
        PlayerSpotted = null;
    }

    public static SoundStimulus EmitSuspicious(SuspiciousSoundKind kind,
        Vector3 worldPosition, GameObject producer, RoomModule primaryRoom,
        RoomModule secondaryRoom = null, float responsibilityDuration = 2.75f)
    {
        if (primaryRoom == null) return null;
        var stimulus = new SoundStimulus(nextSoundId++, kind, worldPosition,
            producer, primaryRoom, secondaryRoom);

        if (producer != null && producer.TryGetComponent(out SoundResponsibility responsibility))
            responsibility.Claim(stimulus.Id, responsibilityDuration);

        SuspiciousSoundEmitted?.Invoke(stimulus);
        return stimulus;
    }

    public static bool TryClaimLead(SoundStimulus stimulus, AlienController alien)
    {
        if (stimulus == null || alien == null) return false;
        if (leadInvestigators.TryGetValue(stimulus.Id, out AlienController current))
            return current == alien;
        leadInvestigators.Add(stimulus.Id, alien);
        return true;
    }

    public static void ReleaseLead(SoundStimulus stimulus, AlienController alien)
    {
        if (stimulus != null && leadInvestigators.TryGetValue(stimulus.Id, out AlienController current)
            && current == alien) leadInvestigators.Remove(stimulus.Id);
    }

    public static void EmitCommunication(AlienCommunicationKind kind,
        SoundStimulus stimulus, AlienController sender, RoomModule searchRoom,
        Vector3 worldSearchPosition)
    {
        if (stimulus == null || sender == null) return;
        CommunicationEmitted?.Invoke(new AlienCommunicationSignal(kind, stimulus,
            sender, searchRoom, worldSearchPosition));
    }

    public static void EmitPlayerSpotted(AlienController sender, RoomModule room,
        Vector3 worldPosition)
    {
        if (sender == null || room == null) return;
        PlayerSpotted?.Invoke(new PlayerSpottedSignal(sender, room, worldPosition));
    }
}
