using UnityEngine;

/// <summary>Una zona cercana al sillón ofrece revisar el celular sin necesitar apuntar a un objeto pequeño.</summary>
public sealed class StoryPhoneInteractionArea : MonoBehaviour, IInteractable
{
    private StoryPhone phone;
    private BoxCollider proximityVolume;
    public void Configure(StoryPhone telephone)
    {
        phone = telephone;
        if (proximityVolume != null) return;
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        foreach (BoxCollider solid in GetComponents<BoxCollider>())
        {
            if (solid.isTrigger) continue;
            bounds.Encapsulate(solid.center - solid.size * .5f);
            bounds.Encapsulate(solid.center + solid.size * .5f);
        }
        // El trigger acompaña los cambios manuales del sillón y queda fuera del raycast y el NavMesh.
        var zone = new GameObject("ZonaInteraccionCelular") { layer = 2 };
        zone.transform.SetParent(transform, false);
        proximityVolume = zone.AddComponent<BoxCollider>();
        proximityVolume.isTrigger = true;
        proximityVolume.center = new Vector3(bounds.center.x, 1f, bounds.center.z);
        proximityVolume.size = new Vector3(bounds.size.x + 2.2f, 2.2f, bounds.size.z + 2.2f);
        var body = zone.AddComponent<Rigidbody>();
        body.isKinematic = true; body.useGravity = false;
        zone.AddComponent<StoryPhoneProximitySensor>().Configure(this);
    }
    public string InteractionPrompt => phone != null ? phone.InteractionPrompt : string.Empty;
    public bool CanInteract(GameObject actor) => phone != null && phone.isActiveAndEnabled && actor != null
        && Contains(actor) && phone.CanReview(actor);
    private bool Contains(GameObject actor)
    {
        if (proximityVolume == null || !proximityVolume.enabled) return false;
        Vector3 point = actor.transform.position + Vector3.up * .5f;
        return (proximityVolume.ClosestPoint(point) - point).sqrMagnitude < .001f;
    }
    public void Interact(GameObject actor) { if (CanInteract(actor)) phone.Review(actor); }
}
