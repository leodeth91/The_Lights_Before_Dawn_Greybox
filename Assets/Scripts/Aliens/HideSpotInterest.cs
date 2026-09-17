using UnityEngine;

[RequireComponent(typeof(HideSpot))]
public sealed class HideSpotInterest : AlienInterest
{
    private HideSpot Spot => GetComponent<HideSpot>();
    public override AlienInterestKind Kind => AlienInterestKind.HideSpot;
    public override Vector3 LookPoint => Spot.InteractionBounds != null
        ? Spot.InteractionBounds.bounds.center : transform.position + Vector3.up * .5f;
    public override Vector3 ApproachPoint => Spot.InspectionPosition;
}
