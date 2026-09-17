using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Abre la puerta girando alrededor de su bisagra real. La hoja conserva su collider durante el movimiento.</summary>
public sealed class DoorInteractable : MonoBehaviour, IInteractable, IOpenable
{
    [SerializeField] private float openAngle = 100f;
    [SerializeField] private float openDuration = 0.30f;
    [SerializeField] private Collider doorLeafCollider;
    [SerializeField, Min(0.5f)] private float maxOpenInteractionDistance = 1.70f;
    [SerializeField, Min(0.5f)] private float maxCloseInteractionDistance = 1.80f;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Coroutine animationRoutine;
    private DoorEntity domainEntity;
    private DoorSocket socket;
    private readonly List<Collider> ignoredInteractorColliders = new List<Collider>();

    public bool IsOpen => State == DoorState.Open;
    public DoorState State => domainEntity != null ? domainEntity.State : DoorState.Closed;
    public DoorSocket Socket => socket;
    public Renderer LeafRenderer => doorLeafCollider != null
        ? doorLeafCollider.GetComponent<Renderer>() : GetComponentInChildren<MeshRenderer>();
    public string InteractionPrompt => State == DoorState.Open ? "E: cerrar"
        : State == DoorState.Opening ? string.Empty : "E: abrir";

    public event Action<DoorInteractable, GameObject> OpeningStarted;
    public event Action<DoorInteractable> Opened;
    public event Action<DoorInteractable> ClosingStarted;
    public event Action<DoorInteractable, GameObject> ClosingStartedBy;
    public event Action<DoorInteractable> Closed;

    private void Awake()
    {
        EnsureInitialized();
        DoorSoundEmitter sound = GetComponent<DoorSoundEmitter>();
        if (sound == null) sound = gameObject.AddComponent<DoorSoundEmitter>();
        sound.Bind(this);
    }

    public bool CanInteract(GameObject interactor)
    {
        if (domainEntity == null
            || (State != DoorState.Closing && !domainEntity.CanInteract(interactor)))
        {
            return false;
        }

        if (interactor == null)
        {
            return true;
        }

        // Con la puerta cerrada mide la distancia hasta su hoja, no hasta la bisagra. Así resulta igual de fácil abrir desde los dos lados.
        Vector3 referencePoint = transform.position;
        if (State == DoorState.Closed && doorLeafCollider != null)
        {
            referencePoint = doorLeafCollider.ClosestPoint(interactor.transform.position);
        }

        Vector3 offset = interactor.transform.position - referencePoint;
        offset.y = 0f;
        float maximumDistance = State == DoorState.Closed
            ? maxOpenInteractionDistance : maxCloseInteractionDistance;
        return offset.sqrMagnitude <= maximumDistance * maximumDistance;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        if (State == DoorState.Closed || State == DoorState.Closing)
        {
            TryOpen(interactor);
        }
        else if (State == DoorState.Open)
        {
            TryClose(interactor);
        }
    }

    public void Toggle()
    {
        Interact(null);
    }

    public bool TryOpen(GameObject actor)
    {
        if (IsOpen) return true;
        return socket != null && HouseFlowController.Instance != null
            ? HouseFlowController.Instance.RequestOpen(socket, actor) : BeginOpen(actor);
    }

    public bool TryClose(GameObject actor)
    {
        if (State == DoorState.Closed) return true;
        return socket != null && HouseFlowController.Instance != null
            ? HouseFlowController.Instance.RequestClose(socket, actor) : BeginClose(actor);
    }

    public void AssignSocket(DoorSocket owner)
    {
        socket = owner;
    }

    public void Configure(float angle, float duration)
    {
        openAngle = angle;
        openDuration = Mathf.Max(0.01f, duration);
    }

    public void Configure(float angle, float duration, Collider leafCollider)
    {
        Configure(angle, duration);
        doorLeafCollider = leafCollider;
    }

    public void ConfigureInteractionDistances(float openDistance, float closeDistance)
    {
        maxOpenInteractionDistance = Mathf.Max(0.5f, openDistance);
        maxCloseInteractionDistance = Mathf.Max(0.5f, closeDistance);
    }

    public bool BeginOpen(GameObject interactor)
    {
        EnsureInitialized();
        if (!domainEntity.TryBeginOpen())
        {
            return false;
        }

        IgnoreInteractorCollision(interactor);
        OpeningStarted?.Invoke(this, interactor);
        StartAnimation(true);
        return true;
    }

    public bool BeginClose(GameObject interactor = null)
    {
        EnsureInitialized();
        if (!domainEntity.TryBeginClose())
        {
            return false;
        }

        IgnoreInteractorCollision(interactor);
        ClosingStarted?.Invoke(this);
        ClosingStartedBy?.Invoke(this, interactor);
        StartAnimation(false);
        return true;
    }

    public void SetClosedInstant()
    {
        EnsureInitialized();
        RestoreInteractorCollision();
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        transform.localRotation = closedRotation;
        domainEntity.ResetClosed();
    }

    private void EnsureInitialized()
    {
        if (domainEntity != null)
        {
            return;
        }

        domainEntity = new DoorEntity(gameObject.name);
        ResolveDoorCollider();
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    private void StartAnimation(bool targetOpen)
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine = StartCoroutine(AnimateDoor(targetOpen));
    }

    private IEnumerator AnimateDoor(bool targetOpen)
    {
        Quaternion start = transform.localRotation;
        Quaternion target = targetOpen ? openRotation : closedRotation;
        float duration = Mathf.Max(0.01f, openDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            normalized = normalized * normalized * (3f - 2f * normalized);
            transform.localRotation = Quaternion.Slerp(start, target, normalized);
            yield return null;
        }

        transform.localRotation = target;
        domainEntity.CompleteTransition(targetOpen);
        animationRoutine = null;

        RestoreInteractorCollision();
        if (targetOpen)
        {
            Opened?.Invoke(this);
        }
        else
        {
            Closed?.Invoke(this);
        }
    }

    private void ResolveDoorCollider()
    {
        if (doorLeafCollider == null)
        {
            doorLeafCollider = GetComponentInChildren<Collider>(true);
        }
    }

    private void IgnoreInteractorCollision(GameObject interactor)
    {
        RestoreInteractorCollision();
        ResolveDoorCollider();
        if (doorLeafCollider == null)
        {
            return;
        }
        IgnoreActorColliders(interactor);
        HouseFlowController house = HouseFlowController.Instance;
        if (house != null)
            foreach (RoomOccupant occupant in house.Occupants)
                if (occupant != null) IgnoreActorColliders(occupant.gameObject);
    }

    private void IgnoreActorColliders(GameObject actor)
    {
        if (actor == null) return;
        Collider[] interactorColliders = actor.GetComponentsInChildren<Collider>(true);
        foreach (Collider interactorCollider in interactorColliders)
        {
            if (interactorCollider == null || interactorCollider == doorLeafCollider
                || ignoredInteractorColliders.Contains(interactorCollider))
            {
                continue;
            }

            Physics.IgnoreCollision(doorLeafCollider, interactorCollider, true);
            ignoredInteractorColliders.Add(interactorCollider);
        }
    }

    private void RestoreInteractorCollision()
    {
        if (doorLeafCollider != null)
        {
            foreach (Collider interactorCollider in ignoredInteractorColliders)
            {
                if (interactorCollider != null)
                {
                    Physics.IgnoreCollision(doorLeafCollider, interactorCollider, false);
                }
            }
        }

        ignoredInteractorColliders.Clear();
    }

    private void OnDisable()
    {
        RestoreInteractorCollision();
    }
}
