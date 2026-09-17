using UnityEngine;

public sealed class StairInterest : AlienInterest
{
    [SerializeField] private Vector3 destinationLocal;
    public override AlienInterestKind Kind => AlienInterestKind.Passage;
    public override Vector3 LookPoint => transform.position + Vector3.up * .4f;
    public override Vector3 ApproachPoint => transform.position;
    public Vector3 Destination => transform.parent.TransformPoint(destinationLocal);
    public void Configure(Vector3 destination) => destinationLocal = destination;
}
