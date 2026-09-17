using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum AlienInterestKind { HideSpot, Object, Passage }
public interface IAlienInterest
{
    AlienInterestKind Kind { get; }
    RoomModule Room { get; }
    Vector3 LookPoint { get; }
    Vector3 ApproachPoint { get; }
    bool Available { get; }
}

/// <summary>Comparte la identificación de la habitación entre los objetivos que el alien puede observar.</summary>
public abstract class AlienInterest : MonoBehaviour, IAlienInterest
{
    private static readonly List<AlienInterest> active = new List<AlienInterest>();
    // Los objetos se registran al activarse y se retiran al desactivarse. El alien consulta esta lista sin buscar todos los objetos de la escena cada cuadro.
    public static IReadOnlyList<AlienInterest> Active => active;
    public RoomModule Room => GetComponentInParent<RoomModule>();
    public abstract AlienInterestKind Kind { get; }
    public abstract Vector3 LookPoint { get; }
    public abstract Vector3 ApproachPoint { get; }
    public virtual bool Available => isActiveAndEnabled;
    protected virtual void OnEnable() { if (!active.Contains(this)) active.Add(this); }
    protected virtual void OnDisable() => active.Remove(this);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => active.Clear();
}
