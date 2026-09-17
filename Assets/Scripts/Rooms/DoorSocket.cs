using UnityEngine;

/// <summary>Es el punto donde se conectan dos habitaciones. Su posición y dirección permiten alinear los marcos; no es la bisagra de la hoja.</summary>
[DisallowMultipleComponent]
public sealed class DoorSocket : MonoBehaviour
{
    [SerializeField] private string socketId;
    [SerializeField] private HouseFloor floor;
    [SerializeField] private DoorSocketKind kind;
    [SerializeField] private RoomModule room;
    [SerializeField] private DoorInteractable door;
    [SerializeField] private GameObject portalAssembly;
    [SerializeField] private DoorThresholdSensor thresholdSensor;

    public string SocketId => socketId;
    public HouseFloor Floor => floor;
    public DoorSocketKind Kind => kind;
    public RoomModule Room => room;
    public DoorInteractable Door => door;
    public Vector3 Position => transform.position;
    public Vector3 Outward => transform.forward;
    public Vector3 ClearApproachPoint
    {
        get
        {
            BoxCollider volume = thresholdSensor != null ? thresholdSensor.GetComponent<BoxCollider>() : null;
            float depth = 1.5f;
            if (volume != null)
            {
                Vector3 edge = volume.transform.TransformPoint(volume.center - Vector3.forward * volume.size.z * .5f);
                depth = Mathf.Max(depth, Vector3.Dot(Position - edge, Outward) + .45f);
            }
            return Position - Outward * depth;
        }
    }
    public bool IsThresholdOccupied => thresholdSensor != null && thresholdSensor.IsOccupied;
    public DoorSocket ConnectedSocket { get; private set; }

    public void Configure(string id, HouseFloor socketFloor, DoorSocketKind socketKind,
        RoomModule owner, DoorInteractable controlledDoor, GameObject assembly,
        DoorThresholdSensor sensor)
    {
        socketId = id;
        floor = socketFloor;
        kind = socketKind;
        room = owner;
        door = controlledDoor;
        portalAssembly = assembly;
        thresholdSensor = sensor;
        if (door != null)
        {
            door.AssignSocket(this);
        }
    }

    public bool IsAnomalyCompatibleWith(DoorSocket other)
    {
        // La planta indica de dónde viene la habitación, no limita la anomalía.
        // Los marcos se alinean en posición y altura antes de permitir el cruce.
        if (other == null || floor == HouseFloor.Unspecified || other.floor == HouseFloor.Unspecified)
        {
            return false;
        }

        if (kind == DoorSocketKind.AnomalyOnly)
        {
            return other.kind == DoorSocketKind.Interior;
        }

        return kind == DoorSocketKind.Interior && other.kind == DoorSocketKind.Interior;
    }

    public bool IsThresholdOccupiedByOtherThan(GameObject ignoredObject)
    {
        return thresholdSensor != null && thresholdSensor.IsOccupiedByOtherThan(ignoredObject);
    }

    public void AssignRoom(RoomModule owner)
    {
        room = owner;
    }

    public void ConnectTo(DoorSocket other)
    {
        ConnectedSocket = other;
    }

    public void Disconnect()
    {
        ConnectedSocket = null;
    }

    public void SetPortalAssemblyActive(bool active)
    {
        if (portalAssembly != null)
        {
            portalAssembly.SetActive(active);
        }
    }

    public void ResetForPool()
    {
        Disconnect();
        SetPortalAssemblyActive(true);
        if (door != null)
        {
            door.SetClosedInstant();
        }
    }

    private void Awake()
    {
        if (room == null)
        {
            room = GetComponentInParent<RoomModule>();
        }

        if (door != null)
        {
            door.AssignSocket(this);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = ConnectedSocket == null ? Color.cyan : Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 0.10f);
        Gizmos.DrawRay(transform.position, transform.forward * 0.65f);
    }
}
