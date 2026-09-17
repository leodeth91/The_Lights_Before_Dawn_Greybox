using UnityEngine;

[RequireComponent(typeof(DoorSocket))]
public sealed class DoorInterest : AlienInterest
{
    public DoorSocket Socket => GetComponent<DoorSocket>();
    public override AlienInterestKind Kind => AlienInterestKind.Passage;
    public override Vector3 LookPoint => Socket.Position + Vector3.up * 1.1f;
    public override Vector3 ApproachPoint => Socket.Position - Socket.Outward * .85f;
    public override bool Available => base.Available && HouseFlowController.Instance != null
        && HouseFlowController.Instance.NormalDestination(Socket) != null
        && (HouseFlowController.Instance.ConnectionFor(Socket) == null
            || !HouseFlowController.Instance.ConnectionFor(Socket).IsAnomalous);
}
