using System.Collections.Generic;
using UnityEngine;

/// <summary>Representa una habitación reutilizable del pool. Su código interno identifica el lugar aunque cambien el nombre visible o su posición en la casa.</summary>
[DisallowMultipleComponent]
public sealed class RoomModule : MonoBehaviour
{
    [SerializeField] private string roomId;
    [SerializeField] private string displayName;
    [SerializeField] private HouseFloor floor;
    [SerializeField] private bool anchorRoom;
    [SerializeField] private Vector3 localVolumeCenter = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private Vector3 localVolumeSize = new Vector3(4f, 2.8f, 4f);
    [SerializeField] private List<DoorSocket> sockets = new List<DoorSocket>();

    public string RoomId => roomId;
    public string DisplayName => displayName;
    public HouseFloor Floor => floor;
    public bool IsAnchorRoom => anchorRoom;
    public Vector3 LocalVolumeCenter => localVolumeCenter;
    public Vector3 LocalVolumeSize => localVolumeSize;
    public IReadOnlyList<DoorSocket> Sockets => sockets;
    public bool IsCurrent { get; private set; }

    public void Configure(string id, string visibleName, HouseFloor houseFloor, bool isAnchor,
        Vector3 volumeCenter, Vector3 volumeSize)
    {
        roomId = id;
        displayName = visibleName;
        floor = houseFloor;
        anchorRoom = isAnchor;
        localVolumeCenter = volumeCenter;
        localVolumeSize = volumeSize;
        RefreshSockets();
    }

    public void RefreshSockets()
    {
        sockets.Clear();
        sockets.AddRange(GetComponentsInChildren<DoorSocket>(true));
        foreach (DoorSocket socket in sockets)
        {
            socket.AssignRoom(this);
        }
    }

    public void SetCurrent(bool current)
    {
        IsCurrent = current;
    }

    public bool ContainsPoint(Vector3 worldPoint, float interiorMargin = 0f)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 half = localVolumeSize * 0.5f;
        half.x = Mathf.Max(0.01f, half.x - interiorMargin);
        half.z = Mathf.Max(0.01f, half.z - interiorMargin);
        Vector3 delta = localPoint - localVolumeCenter;

        return Mathf.Abs(delta.x) <= half.x
            && Mathf.Abs(delta.y) <= half.y + 0.25f
            && Mathf.Abs(delta.z) <= half.z;
    }

    public void ResetDoorsInstant()
    {
        foreach (DoorSocket socket in sockets)
        {
            socket.ResetForPool();
        }
    }

    private void Awake()
    {
        if (sockets.Count == 0)
        {
            RefreshSockets();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = IsCurrent
            ? new Color(0.15f, 1f, 0.35f, 0.8f)
            : new Color(0.15f, 0.65f, 1f, 0.45f);
        Gizmos.DrawWireCube(localVolumeCenter, localVolumeSize);
        Gizmos.matrix = previous;
    }
}
