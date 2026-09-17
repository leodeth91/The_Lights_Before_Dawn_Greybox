using UnityEngine;

/// <summary>Es un acuerdo común: cualquier objeto usable informa su texto, si puede usarse y qué ocurre al pulsar E. El Player no necesita conocer cada tipo de objeto.</summary>
public interface IInteractable
{
    string InteractionPrompt { get; }
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
}
