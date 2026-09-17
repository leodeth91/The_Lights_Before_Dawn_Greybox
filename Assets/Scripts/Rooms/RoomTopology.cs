using System;

public enum HouseFloor
{
    Unspecified,
    Ground,
    Upper,
    Both
}

public enum DoorSocketKind
{
    Interior,
    HallAccess,
    AnomalyOnly
}

[Serializable]
public sealed class NormalRoomConnection
{
    public string SocketA;
    public string SocketB;

    public NormalRoomConnection(string socketA, string socketB)
    {
        SocketA = socketA;
        SocketB = socketB;
    }
}
