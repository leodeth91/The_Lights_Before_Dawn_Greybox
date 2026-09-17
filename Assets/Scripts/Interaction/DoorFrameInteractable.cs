using UnityEngine;

/// <summary>El marco permanece quieto mientras gira la hoja. Así el jugador puede cerrar una puerta abierta apuntando a su marco.</summary>
[DisallowMultipleComponent]
public sealed class DoorFrameInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private DoorInteractable door;

    public DoorInteractable Door => door;
    public string InteractionPrompt => door != null ? door.InteractionPrompt : string.Empty;

    public void Configure(DoorInteractable controlledDoor)
    {
        door = controlledDoor;
    }

    public bool CanInteract(GameObject interactor)
    {
        return door != null && door.CanInteract(interactor);
    }

    public void Interact(GameObject interactor)
    {
        if (door != null)
        {
            door.Interact(interactor);
        }
    }
}
