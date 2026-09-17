using UnityEngine;

/// <summary>El celular se revisa con la misma acción E que los demás objetos; permanece fijo.</summary>
public sealed class StoryPhone : MonoBehaviour, IInteractable
{
    private RoomModule room;
    private void Awake()
    {
        room = GetComponentInParent<RoomModule>();
        // Los colliders del sillón también permiten revisar el celular. No añadimos otro volumen físico.
        Transform surface = transform.parent;
        if (surface == null || surface.GetComponent<HideSpot>() != null) return;
        StoryPhoneInteractionArea area = surface.GetComponent<StoryPhoneInteractionArea>()
            ?? surface.gameObject.AddComponent<StoryPhoneInteractionArea>();
        area.Configure(this);
    }
    public string InteractionPrompt => "Celular\nE: Revisar celular";
    public bool CanReview(GameObject actor) => StoryProgression.Instance != null
        && StoryProgression.Instance.Objective == StoryObjective.FindPhone
        && actor != null && actor.GetComponent<PlayerInteractor>() != null
        && GameSessionManager.Instance != null && GameSessionManager.Instance.IsPlaying
        && actor.GetComponent<RoomOccupant>()?.Room == room;
    public bool CanInteract(GameObject actor) => CanReview(actor)
        && Vector3.Distance(actor.transform.position, transform.position) < 2.5f;
    public void Review(GameObject actor) { if (CanReview(actor)) StoryProgression.Instance.ReviewPhone(actor); }
    public void Interact(GameObject actor) { if (CanInteract(actor)) Review(actor); }
}
