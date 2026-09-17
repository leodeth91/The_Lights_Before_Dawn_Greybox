using System;
using UnityEngine;
using UnityEngine.Events;

public enum AlienActivityPhase
{
    Cautious,
    Active,
    Intense
}

/// <summary>
/// Agrupa los valores que modifican el ritmo de los aliens. Las reglas de visión,
/// captura y puertas permanecen iguales durante toda la partida.
/// </summary>
public readonly struct AlienActivityTuning
{
    public AlienActivityPhase Phase { get; }
    public Vector2 WanderDuration { get; }
    public Vector2 ScanDuration { get; }
    public float PatrolSpeedMultiplier { get; }
    public float SearchSpeedMultiplier { get; }
    public float InspectionDurationMultiplier { get; }
    public float HideSpotWeight { get; }
    public float PassageWeight { get; }
    public float InterestCooldown { get; }
    public float AlternationPressure { get; }

    public AlienActivityTuning(AlienActivityPhase phase, Vector2 wanderDuration,
        Vector2 scanDuration, float patrolSpeedMultiplier, float searchSpeedMultiplier,
        float inspectionDurationMultiplier, float hideSpotWeight, float passageWeight,
        float interestCooldown, float alternationPressure)
    {
        Phase = phase;
        WanderDuration = wanderDuration;
        ScanDuration = scanDuration;
        PatrolSpeedMultiplier = patrolSpeedMultiplier;
        SearchSpeedMultiplier = searchSpeedMultiplier;
        InspectionDurationMultiplier = inspectionDurationMultiplier;
        HideSpotWeight = hideSpotWeight;
        PassageWeight = passageWeight;
        InterestCooldown = interestCooldown;
        AlternationPressure = alternationPressure;
    }
}

/// <summary>
/// Observa el reloj de la partida y avisa cuando cambia el nivel de actividad.
/// Cada alien recibe el mismo aviso, pero termina normalmente la acción que ya comenzó.
/// </summary>
[DisallowMultipleComponent]
public sealed class AlienActivityDirector : MonoBehaviour
{
    public static AlienActivityDirector Instance { get; private set; }

    [Header("Etapas de la partida")]
    [SerializeField, Range(.05f, .9f)] private float activePhaseStartsAt = 2f / 7f;
    [SerializeField, Range(.1f, .95f)] private float intensePhaseStartsAt = 5f / 7f;
    [SerializeField] private UnityEvent<string> onPhaseChanged = new UnityEvent<string>();

    private GameSessionManager session;

    public AlienActivityTuning Current { get; private set; }
    public event Action<AlienActivityTuning> ActivityChanged;
    public UnityEvent<string> OnPhaseChanged => onPhaseChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        Current = GetTuningForProgress(0f);
    }

    private void Start()
    {
        session = GameSessionManager.Instance;
        Apply(GetSessionTuning(), true);
    }

    private void Update()
    {
        if (session == null) session = GameSessionManager.Instance;
        if (session == null || !session.IsPlaying) return;
        Apply(GetSessionTuning(), false);
    }

    private AlienActivityTuning GetSessionTuning()
    {
        // Los límites siguen siendo dos y cinco minutos desde el inicio, aunque el contador visible sea más corto.
        bool phoneFound = StoryProgression.Instance != null
            && StoryProgression.Instance.Objective == StoryObjective.Survive;
        return GetTuningForProgress(phoneFound ? 1f : (session != null ? session.ElapsedPlayTime / 420f : 0f));
    }

    public AlienActivityTuning GetTuningForProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (progress >= intensePhaseStartsAt)
        {
            // Cerca del amanecer toman decisiones más seguido y vuelven antes a
            // escondites y puertas, con aumentos de velocidad pequeños y previsibles.
            return new AlienActivityTuning(AlienActivityPhase.Intense,
                new Vector2(.8f, 1.6f), new Vector2(2.6f, 3.1f),
                1.25f, 1.3f, .7f, 3f, 2.5f, 3f, .65f);
        }
        if (progress >= activePhaseStartsAt)
        {
            return new AlienActivityTuning(AlienActivityPhase.Active,
                new Vector2(1.7f, 3.15f), new Vector2(3f, 4f),
                1.06f, 1.08f, .92f, 1.45f, 1.4f, 9f, .25f);
        }
        return new AlienActivityTuning(AlienActivityPhase.Cautious,
            new Vector2(2f, 4f), new Vector2(3.5f, 4.5f),
            1f, 1f, 1f, 1f, 1f, 12f, .1f);
    }

    private void Apply(AlienActivityTuning next, bool force)
    {
        if (!force && Current.Phase == next.Phase) return;
        Current = next;
        ActivityChanged?.Invoke(Current);
        onPhaseChanged.Invoke(PhaseLabel(Current.Phase));
    }

    private static string PhaseLabel(AlienActivityPhase phase)
    {
        if (phase == AlienActivityPhase.Intense) return "Intensa";
        if (phase == AlienActivityPhase.Active) return "Activa";
        return "Cautelosa";
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
