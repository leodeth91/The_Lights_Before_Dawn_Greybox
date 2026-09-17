using UnityEngine;
using UnityEngine.Events;

/// <summary>Permite que los futuros objetos se revisen mediante la misma interfaz que los escondites.</summary>
public sealed class InspectableObject : AlienInterest, IInspectable
{
    [SerializeField] private string displayName = "Objeto";
    [SerializeField] private Transform approach;
    [SerializeField] private UnityEvent onInspectionStarted = new UnityEvent();
    [SerializeField] private UnityEvent onInspectionFinished = new UnityEvent();
    private GameObject inspector;
    public override AlienInterestKind Kind => AlienInterestKind.Object;
    public override Vector3 LookPoint => transform.position;
    public override Vector3 ApproachPoint => approach != null ? approach.position : transform.position;
    public string InspectionName => displayName;
    public Vector3 InspectionPosition => ApproachPoint;
    public bool TryBeginInspection(GameObject actor)
    {
        if (actor == null || (inspector != null && inspector != actor)) return false;
        inspector = actor; onInspectionStarted.Invoke(); return true;
    }
    public void EndInspection(GameObject actor)
    {
        if (inspector != actor) return;
        inspector = null; onInspectionFinished.Invoke();
    }
}
