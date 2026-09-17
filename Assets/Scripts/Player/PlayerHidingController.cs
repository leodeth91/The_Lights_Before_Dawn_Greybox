using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerHideState
{
    Outside,
    Entering,
    Hidden,
    Exiting
}

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerHidingController : MonoBehaviour
{
    [SerializeField] private ChildPlayerController movementController;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private OverShoulderCamera playerCamera;
    [SerializeField] private Transform visualRoot;
    private Coroutine transitionRoutine;
    private HideSpot currentSpot;
    private IHideTransitionStrategy transitionStrategy;
    private float normalHeight;
    private Vector3 normalCenter;
    private float normalStepOffset;
    private Quaternion normalVisualRotation;
    private Vector3 normalVisualScale;
    private Vector3 currentEntryPosition;
    private Quaternion currentEntryRotation;
    private readonly List<Collider> ignoredSpotColliders = new List<Collider>();

    public PlayerHideState State { get; private set; } = PlayerHideState.Outside;
    public bool IsHidden => State == PlayerHideState.Hidden;
    public bool IsTransitioning => State == PlayerHideState.Entering
        || State == PlayerHideState.Exiting;
    public HideSpot CurrentSpot => currentSpot;
    public IInteractable PriorityInteractable => IsHidden ? currentSpot : null;

    public event Action<PlayerHideState, HideSpot> StateChanged;

    public void Configure(ChildPlayerController movement, CharacterController character,
        OverShoulderCamera cameraController, Transform modelRoot)
    {
        movementController = movement;
        characterController = character;
        playerCamera = cameraController;
        visualRoot = modelRoot;
    }

    private void Awake()
    {
        if (movementController == null) movementController = GetComponent<ChildPlayerController>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (playerCamera == null) playerCamera = FindFirstObjectByType<OverShoulderCamera>();
        transitionStrategy = new UnderBedHideTransitionStrategy();
        CacheNormalShape();
    }

    public void Toggle(HideSpot spot)
    {
        if (IsTransitioning)
        {
            return;
        }

        if (IsHidden)
        {
            if (spot == currentSpot)
            {
                StartTransition(ExitSpot());
            }
            return;
        }

        if (State == PlayerHideState.Outside && spot != null && spot.TryClaim(this))
        {
            currentSpot = spot;
            currentSpot.GetEntryPose(transform.position, out currentEntryPosition,
                out currentEntryRotation);
            SetCurrentSpotCollisionIgnored(true);
            StartTransition(EnterSpot());
        }
    }

    private void StartTransition(IEnumerator routine)
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }
        transitionRoutine = StartCoroutine(routine);
    }

    // La corrutina reparte el movimiento entre varios cuadros. Así entrar no teletransporta al Player ni congela el juego.
    private IEnumerator EnterSpot()
    {
        SetState(PlayerHideState.Entering);
        currentSpot.NotifyEnterStarted();
        SetPlayerControl(false);
        playerCamera?.SetHidingView(true);

        normalVisualRotation = visualRoot != null ? visualRoot.localRotation : Quaternion.identity;
        normalVisualScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
        transitionStrategy = HideTransitionStrategyFactory.Create(currentSpot.TransitionType);
        yield return MoveRoot(transform.position, transform.rotation,
            currentEntryPosition, currentEntryRotation,
            currentSpot.AlignDuration, normalVisualRotation, normalVisualRotation,
            normalVisualScale, normalVisualScale);

        ApplyHiddenControllerShape(currentSpot.HiddenControllerHeight);
        Quaternion hiddenVisual = Quaternion.Euler(currentSpot.HiddenVisualEulerAngles);
        yield return MoveRoot(transform.position, transform.rotation,
            currentSpot.HiddenPoint.position, currentSpot.HiddenPoint.rotation,
            currentSpot.TransitionDuration, normalVisualRotation, hiddenVisual,
            normalVisualScale, Vector3.Scale(normalVisualScale, currentSpot.HiddenVisualScale));

        transitionRoutine = null;
        SetState(PlayerHideState.Hidden);
        currentSpot.NotifyHidden();
    }

    private IEnumerator ExitSpot()
    {
        SetState(PlayerHideState.Exiting);
        currentSpot.NotifyExitStarted();
        Quaternion hiddenVisual = visualRoot != null ? visualRoot.localRotation
            : Quaternion.Euler(currentSpot.HiddenVisualEulerAngles);
        Vector3 hiddenScale = visualRoot != null ? visualRoot.localScale : normalVisualScale;

        yield return MoveRoot(transform.position, transform.rotation,
            currentEntryPosition, currentEntryRotation,
            currentSpot.TransitionDuration, hiddenVisual, normalVisualRotation,
            hiddenScale, normalVisualScale);

        HideSpot exitedSpot = currentSpot;
        currentSpot = null;
        exitedSpot.Release(this);
        RestoreNormalControllerShape();
        SetCurrentSpotCollisionIgnored(false);
        playerCamera?.SetHidingView(false);
        SetPlayerControl(true);
        transitionRoutine = null;
        SetState(PlayerHideState.Outside, exitedSpot);
        exitedSpot.NotifyExited();
    }

    private IEnumerator MoveRoot(Vector3 startPosition, Quaternion startRotation,
        Vector3 destination, Quaternion destinationRotation, float duration,
        Quaternion startVisualRotation, Quaternion destinationVisualRotation,
        Vector3 startVisualScale, Vector3 destinationVisualScale)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            Vector3 desiredPosition = transitionStrategy.EvaluatePosition(startPosition,
                destination, normalized);
            MoveWithCharacterController(desiredPosition);
            transform.rotation = transitionStrategy.EvaluateRotation(startRotation,
                destinationRotation, normalized);
            if (visualRoot != null)
            {
                visualRoot.localRotation = transitionStrategy.EvaluateRotation(
                    startVisualRotation, destinationVisualRotation, normalized);
                visualRoot.localScale = Vector3.Lerp(startVisualScale,
                    destinationVisualScale, UnderBedHideTransitionStrategy.Smooth(normalized));
            }
            yield return null;
        }

        MoveWithCharacterController(destination);
        transform.rotation = destinationRotation;
        if (visualRoot != null)
        {
            visualRoot.localRotation = destinationVisualRotation;
            visualRoot.localScale = destinationVisualScale;
        }
    }

    private void SetPlayerControl(bool enabled)
    {
        movementController?.SetMovementLocked(!enabled);
    }

    private void MoveWithCharacterController(Vector3 destination)
    {
        if (characterController != null && characterController.enabled)
        {
            characterController.Move(destination - transform.position);
        }
        else
        {
            transform.position = destination;
        }
    }

    private void CacheNormalShape()
    {
        if (characterController == null)
        {
            return;
        }

        normalHeight = characterController.height;
        normalCenter = characterController.center;
        normalStepOffset = characterController.stepOffset;
        normalVisualRotation = visualRoot != null ? visualRoot.localRotation : Quaternion.identity;
        normalVisualScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
    }

    private void ApplyHiddenControllerShape(float requestedHeight)
    {
        if (characterController == null)
        {
            return;
        }

        float minimum = characterController.radius * 2f + 0.01f;
        characterController.height = Mathf.Max(minimum, requestedHeight);
        characterController.center = Vector3.up * (characterController.height * 0.5f);
        characterController.stepOffset = Mathf.Min(0.08f, characterController.height * 0.2f);
    }

    private void RestoreNormalControllerShape()
    {
        if (characterController == null)
        {
            return;
        }

        characterController.height = normalHeight;
        characterController.center = normalCenter;
        characterController.stepOffset = normalStepOffset;
    }

    // Este aviso permite a los aliens recordar una entrada que vieron y a la UI cambiar el texto, sin repetir la lógica aquí.
    private void SetState(PlayerHideState nextState, HideSpot eventSpot = null)
    {
        State = nextState;
        StateChanged?.Invoke(State, eventSpot != null ? eventSpot : currentSpot);
    }

    private void OnDisable()
    {
        if (currentSpot != null)
        {
            currentSpot.Release(this);
            currentSpot = null;
        }
        RestoreNormalControllerShape();
        SetCurrentSpotCollisionIgnored(false);
        SetPlayerControl(true);
        if (visualRoot != null)
        {
            visualRoot.localRotation = normalVisualRotation;
            visualRoot.localScale = normalVisualScale;
        }
    }

    private void SetCurrentSpotCollisionIgnored(bool ignored)
    {
        if (characterController == null)
        {
            ignoredSpotColliders.Clear();
            return;
        }

        if (ignored)
        {
            ignoredSpotColliders.Clear();
            if (currentSpot == null)
            {
                return;
            }

            Collider[] spotColliders = currentSpot.GetComponentsInChildren<Collider>(true);
            foreach (Collider spotCollider in spotColliders)
            {
                if (spotCollider == null || spotCollider == characterController)
                {
                    continue;
                }

                Physics.IgnoreCollision(characterController, spotCollider, true);
                ignoredSpotColliders.Add(spotCollider);
            }
            return;
        }

        foreach (Collider spotCollider in ignoredSpotColliders)
        {
            if (spotCollider != null)
            {
                Physics.IgnoreCollision(characterController, spotCollider, false);
            }
        }
        ignoredSpotColliders.Clear();
    }
}
