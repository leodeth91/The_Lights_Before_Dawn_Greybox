public sealed class RoomConnection
{
    public RoomModule SourceRoom { get; }
    public DoorSocket SourceSocket { get; }
    public RoomModule TargetRoom { get; }
    public DoorSocket TargetSocket { get; }
    public bool IsAnomalous { get; }
    // La coordinación de esta conexión se descarta cuando la conexión deja de existir.
    public float AutoCloseAt { get; set; }
    internal float OpenProtectedUntil;
    internal readonly System.Collections.Generic.Dictionary<UnityEngine.GameObject, float> PassageUsers = new();
    internal readonly System.Collections.Generic.Dictionary<UnityEngine.GameObject, float> Pursuers = new();

    public RoomConnection(RoomModule sourceRoom, DoorSocket sourceSocket,
        RoomModule targetRoom, DoorSocket targetSocket, bool isAnomalous)
    {
        SourceRoom = sourceRoom;
        SourceSocket = sourceSocket;
        TargetRoom = targetRoom;
        TargetSocket = targetSocket;
        IsAnomalous = isAnomalous;
    }
}

public readonly struct RoomConnectionCandidate
{
    public RoomModule Room { get; }
    public DoorSocket Socket { get; }

    public RoomConnectionCandidate(RoomModule room, DoorSocket socket)
    {
        Room = room;
        Socket = socket;
    }
}
