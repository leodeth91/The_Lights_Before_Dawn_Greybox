using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum GameSessionState
{
    Menu,
    Playing,
    Victory,
    Defeat
}

[DisallowMultipleComponent]
public sealed class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    [SerializeField] private Transform player;
    [SerializeField] private GameObject runtimeInterface;
    [SerializeField, Min(0f)] private float survivalDuration;
    [SerializeField] private float defeatBelowY = -8f;

    private float elapsed;
    // El contador de supervivencia empieza con el celular; el tiempo total regula la actividad desde que comienza a jugarse.
    private float elapsedPlayTime;
    private bool survivalStarted;

    public GameSessionState State { get; private set; } = GameSessionState.Menu;
    public bool IsPlaying => State == GameSessionState.Playing;
    public bool HasSurvivalTimer => survivalDuration > 0f && survivalStarted;
    public float SurvivalDuration => survivalDuration;
    public float ElapsedPlayTime => elapsedPlayTime;
    public float RemainingTime => Mathf.Max(0f, survivalDuration - elapsed);
    public float Progress01 => HasSurvivalTimer
        ? Mathf.Clamp01(elapsed / survivalDuration) : 0f;

    // Quienes necesitan reaccionar al inicio, victoria o derrota escuchan este evento; no guardan una copia del estado.
    public event Action<GameSessionState> StateChanged;

    public void Configure(Transform playerTransform, float duration, GameObject interfaceRoot)
    {
        player = playerTransform;
        survivalDuration = Mathf.Max(0f, duration);
        runtimeInterface = interfaceRoot;
    }

    public void SetSurvivalDuration(float duration)
    {
        survivalDuration = Mathf.Max(0f, duration);
        survivalStarted = true;
    }

    public void StartSurvival(float duration)
    {
        if (!IsPlaying || survivalStarted) return;
        elapsed = 0f;
        survivalDuration = duration;
        survivalStarted = true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (runtimeInterface != null)
        {
            runtimeInterface.SetActive(true);
        }
        Time.timeScale = 0f;
    }

    private void Start()
    {
        StateChanged?.Invoke(State);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        Time.timeScale = 1f;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (State == GameSessionState.Menu)
        {
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            {
                QuitToDesktop();
                return;
            }
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame))
            {
                BeginGame();
            }
            return;
        }

        if (State == GameSessionState.Victory || State == GameSessionState.Defeat)
        {
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            return;
        }

        // Este reloj cuenta también los objetivos iniciales; el otro empieza con el celular.
        elapsedPlayTime += Time.deltaTime;
        if (HasSurvivalTimer) elapsed += Time.deltaTime;
        if (player != null && player.position.y < defeatBelowY)
        {
            SetState(GameSessionState.Defeat);
        }
        else if (HasSurvivalTimer && elapsed >= survivalDuration)
        {
            SetState(GameSessionState.Victory);
        }
    }

    public void QuitToDesktop()
    {
        if (State != GameSessionState.Menu) return;
        // En una build cierra el juego; dentro de Unity solo detiene Play, sin cerrar el editor.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void BeginGame()
    {
        if (State != GameSessionState.Menu)
        {
            return;
        }

        elapsed = 0f;
        elapsedPlayTime = 0f;
        survivalStarted = GetComponent<StoryProgression>() == null;
        Time.timeScale = 1f;
        SetState(GameSessionState.Playing);
    }

    public void TriggerDefeat()
    {
        if (State == GameSessionState.Playing)
        {
            SetState(GameSessionState.Defeat);
        }
    }

    public void TriggerVictory()
    {
        if (State == GameSessionState.Playing)
        {
            SetState(GameSessionState.Victory);
        }
    }

    private void SetState(GameSessionState next)
    {
        if (State == next)
        {
            return;
        }

        State = next;
        if (State == GameSessionState.Victory || State == GameSessionState.Defeat)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        StateChanged?.Invoke(State);
    }
}
