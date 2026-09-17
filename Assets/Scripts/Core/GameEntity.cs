using UnityEngine;

/// <summary>Ejemplo de herencia: las entidades comparten un identificador, las interactivas agregan el acuerdo de uso y DoorEntity define los estados de una puerta.</summary>
public abstract class GameEntity
{
    public string Id { get; }

    protected GameEntity(string id)
    {
        Id = string.IsNullOrWhiteSpace(id) ? "entity" : id;
    }
}

public abstract class InteractiveEntity : GameEntity
{
    protected InteractiveEntity(string id) : base(id)
    {
    }

    public abstract bool CanInteract(GameObject interactor);
    public abstract void Interact(GameObject interactor);
}

public enum DoorState
{
    Closed,
    Opening,
    Open,
    Closing
}

public sealed class DoorEntity : InteractiveEntity
{
    public DoorState State { get; private set; } = DoorState.Closed;

    public DoorEntity(string id) : base(id)
    {
    }

    public override bool CanInteract(GameObject interactor)
    {
        return State == DoorState.Closed || State == DoorState.Open;
    }

    public override void Interact(GameObject interactor)
    {
        TryBeginToggle(out _);
    }

    public bool TryBeginToggle(out bool opening)
    {
        if (State == DoorState.Closed)
        {
            State = DoorState.Opening;
            opening = true;
            return true;
        }

        if (State == DoorState.Open)
        {
            State = DoorState.Closing;
            opening = false;
            return true;
        }

        opening = false;
        return false;
    }

    public bool TryBeginOpen()
    {
        // Si intentan abrir mientras se cierra, permite invertir el movimiento sin esperar.
        if (State != DoorState.Closed && State != DoorState.Closing)
        {
            return false;
        }

        State = DoorState.Opening;
        return true;
    }

    public bool TryBeginClose()
    {
        if (State != DoorState.Open)
        {
            return false;
        }

        State = DoorState.Closing;
        return true;
    }

    public void CompleteTransition(bool open)
    {
        State = open ? DoorState.Open : DoorState.Closed;
    }

    public void ResetClosed()
    {
        State = DoorState.Closed;
    }
}
