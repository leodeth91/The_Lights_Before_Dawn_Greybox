using UnityEngine;
using UnityEngine.UI;

/// <summary>Actualiza los textos y paneles al recibir avisos del juego. Muestra la habitación, la interacción y el tiempo; no decide objetivos ni capturas.</summary>
[DisallowMultipleComponent]
public sealed class PrototypeHUD : MonoBehaviour
{
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text resultText;
    [SerializeField] private Text roomText;
    [SerializeField] private Text connectionText;
    [SerializeField] private Text timerText;
    [SerializeField] private Text interactionText;

    private HouseFlowController house;
    private GameSessionManager session;
    private PlayerInteractor playerInteractor;

    public void Configure(GameObject menu, GameObject result, Text resultLabel,
        Text roomLabel, Text connectionLabel, Text timerLabel, Text interactionLabel)
    {
        startPanel = menu;
        resultPanel = result;
        resultText = resultLabel;
        roomText = roomLabel;
        connectionText = connectionLabel;
        timerText = timerLabel;
        interactionText = interactionLabel;
    }

    private void Start()
    {
        house = HouseFlowController.Instance;
        session = GameSessionManager.Instance;
        playerInteractor = FindFirstObjectByType<PlayerInteractor>();

        if (house != null)
        {
            house.CurrentRoomChanged += HandleRoomChanged;
            house.ConnectionOpened += HandleConnectionOpened;
            house.ConnectionClosed += HandleConnectionClosed;
            HandleRoomChanged(null, house.CurrentRoom);
        }

        if (session != null)
        {
            session.StateChanged += HandleStateChanged;
            HandleStateChanged(session.State);
        }

        if (startPanel != null)
        {
            // Conserva el texto del menú editado a mano y solo agrega la nueva opción.
            foreach (Text label in startPanel.GetComponentsInChildren<Text>(true))
                if (label.text.IndexOf("comenzar", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && !label.text.Contains("Salir al escritorio"))
                {
                    label.text += "\nQ: Salir al escritorio";
                    break;
                }
        }

        if (playerInteractor != null)
        {
            playerInteractor.PromptChanged += HandlePromptChanged;
            HandlePromptChanged(playerInteractor.CurrentPrompt);
        }
    }

    private void OnDestroy()
    {
        if (house != null)
        {
            house.CurrentRoomChanged -= HandleRoomChanged;
            house.ConnectionOpened -= HandleConnectionOpened;
            house.ConnectionClosed -= HandleConnectionClosed;
        }

        if (session != null)
        {
            session.StateChanged -= HandleStateChanged;
        }

        if (playerInteractor != null)
        {
            playerInteractor.PromptChanged -= HandlePromptChanged;
        }
    }

    private void HandlePromptChanged(string prompt)
    {
        if (interactionText != null)
        {
            interactionText.text = prompt;
        }
    }

    private void Update()
    {
        if (timerText != null && session != null)
        {
            int remainingSeconds = Mathf.CeilToInt(session.RemainingTime);
            int minutes = remainingSeconds / 60;
            int seconds = remainingSeconds % 60;
            timerText.text = session.IsPlaying && session.HasSurvivalTimer
                ? $"Amanecer en: {minutes:00}:{seconds:00}" : string.Empty;
        }
    }

    private void HandleStateChanged(GameSessionState state)
    {
        if (startPanel != null)
        {
            startPanel.SetActive(state == GameSessionState.Menu);
        }

        bool finished = state == GameSessionState.Victory || state == GameSessionState.Defeat;
        if (resultPanel != null)
        {
            resultPanel.SetActive(finished);
        }

        if (resultText != null && finished)
        {
            resultText.text = state == GameSessionState.Victory
                ? "Llegó el amanecer, sobreviviste\n\nR: reiniciar"
                : "nunca nadie lo volvió a ver\n\nR: reiniciar";
        }
    }

    private void HandleRoomChanged(RoomModule previous, RoomModule current)
    {
        if (roomText != null)
        {
            roomText.text = current != null ? "Habitacion: " + current.DisplayName : string.Empty;
        }
        RefreshPlayerConnection();
    }

    private void HandleConnectionOpened(RoomConnection connection)
    {
        RefreshPlayerConnection();
    }

    private void HandleConnectionClosed(RoomConnection connection)
    {
        RefreshPlayerConnection();
    }

    private void RefreshPlayerConnection()
    {
        if (connectionText == null || house == null) return;
        RoomConnection connection = house.ActiveConnection;
        if (connection == null) { connectionText.text = "Conexion cerrada"; return; }
        RoomModule destination = connection.SourceRoom == house.CurrentRoom ? connection.TargetRoom : connection.SourceRoom;
        connectionText.text = (connection.IsAnomalous ? "Anomalia: " : "Conexion normal: ") + destination.DisplayName;
    }
}
