using UnityEngine;

public enum PerceptionTargetKind
{
    Player,
    Object,
    HideSpot
}

/// <summary>Define qué información ofrece un objetivo detectable. El alien se encarga de comprobar si hay algo que le tapa la vista.</summary>
public interface IPerceptionTarget
{
    PerceptionTargetKind PerceptionKind { get; }
    Transform PerceptionTransform { get; }
    bool IsAvailableForDetection { get; }
}
