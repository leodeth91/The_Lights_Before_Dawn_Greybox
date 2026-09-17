using UnityEngine;

/// <summary>Identifica a cada personaje aunque cambie de padre en la jerarquía o tenga varios colliders.</summary>
[DisallowMultipleComponent]
public sealed class RoomOccupant : MonoBehaviour
{
    public RoomModule Room { get; private set; }
    public bool UsesNormalDoors { get; private set; }
    public void Configure(RoomModule room, bool normalDoors)
    {
        Room = room;
        UsesNormalDoors = normalDoors;
    }
    public void Enter(RoomModule room) => Room = room;
}
