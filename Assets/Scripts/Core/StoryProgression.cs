using System;
using System.Collections.Generic;
using UnityEngine;

public enum StoryObjective { FindParents, TryExit, FindPhone, Survive }

/// <summary>Ordena los objetivos. Los demás sistemas consultan este avance en lugar de guardar copias.</summary>
public sealed class StoryProgression : MonoBehaviour
{
    public static StoryProgression Instance { get; private set; }
    [SerializeField] private RoomModule parentsRoom, childRoom, officeRoom;
    [SerializeField] private DoorSocket entrance;
    [SerializeField] private StoryPhone phone;
    [SerializeField] private float survivalSeconds = 180f;
    [SerializeField] private Material objectiveDoorMaterial;
    public StoryObjective Objective { get; private set; }
    public bool AnomalyEnabled => Objective >= StoryObjective.FindPhone;
    public RoomModule ChildRoom => childRoom;
    public event Action<StoryObjective> ObjectiveChanged;
    private HouseFlowController house;
    private GameSessionManager session;
    private PlayerInteractor player;
    private readonly Queue<string> messages = new Queue<string>();
    private string currentMessage = "";
    private float messageUntil, nextGuide;
    private bool clearEntrancePending;
    private bool loopCrossed;
    private Material runtimeDoorMaterial;
    private Renderer highlightedDoor;
    private Material[] originalDoorMaterials;
    public DoorSocket GuideDoor { get; private set; }
    private bool silenceMessageShown;
    private bool GuideDoorClosed
    {
        get
        {
            if (GuideDoor == null) return false;
            // Ambos lados de un paso comparten la hoja de la conexión activa.
            DoorInteractable door = house.ConnectionFor(GuideDoor)?.SourceSocket.Door ?? GuideDoor.Door;
            return door != null && door.State == DoorState.Closed;
        }
    }

    public void Configure(RoomModule parents, RoomModule child, RoomModule office, DoorSocket exit, StoryPhone telephone)
    { parentsRoom = parents; childRoom = child; officeRoom = office; entrance = exit; phone = telephone; }

    private void Awake() { Instance = this; }
    private void Start()
    {
        house = HouseFlowController.Instance;
        session = GameSessionManager.Instance;
        player = FindFirstObjectByType<PlayerInteractor>();
        house.CurrentRoomChanged += OnRoomChanged;
        session.StateChanged += OnSessionChanged;
        if (objectiveDoorMaterial == null)
        {
            // Un material rojo con el shader de Unity basta para destacar la puerta sin dibujar contornos.
            Shader shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
                ? Shader.Find("Standard") : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            runtimeDoorMaterial = new Material(shader) { name = "MaterialPuertaObjetivo_Rojo", color = new Color(.95f, .035f, .035f) };
            if (runtimeDoorMaterial.HasProperty("_BaseColor")) runtimeDoorMaterial.SetColor("_BaseColor", runtimeDoorMaterial.color);
        }
        if (session.IsPlaying) OnSessionChanged(session.State);
    }
    private void OnDestroy()
    {
        if (house != null) house.CurrentRoomChanged -= OnRoomChanged;
        if (session != null) session.StateChanged -= OnSessionChanged;
        RestoreDoorMaterial();
        if (runtimeDoorMaterial != null) Destroy(runtimeDoorMaterial);
        if (Instance == this) Instance = null;
    }
    private void OnSessionChanged(GameSessionState state)
    {
        if (state != GameSessionState.Playing) return;
        messages.Enqueue("Vienen otra vez, debo buscar a mamá y papá");
        messages.Enqueue("Esta vez no me atraparán");
    }
    // El objetivo cambia en un solo lugar. El resto de sistemas consulta este dato para activar el celular, las guías y la anomalía.
    private void Advance(StoryObjective next, params string[] thoughts)
    {
        Objective = next;
        RestoreDoorMaterial();
        // Un pensamiento nuevo reemplaza los anteriores para no comunicar un objetivo ya completado.
        messages.Clear(); currentMessage = "";
        foreach (string thought in thoughts) messages.Enqueue(thought);
        nextGuide = 0f;
        ObjectiveChanged?.Invoke(next);
    }
    private void OnRoomChanged(RoomModule previous, RoomModule current)
    {
        if (session.IsPlaying && Objective == StoryObjective.FindParents && current == parentsRoom)
            Advance(StoryObjective.TryExit, "No están! Tengo que salir de la casa");
        if (!silenceMessageShown && session.IsPlaying && previous == parentsRoom && current != parentsRoom
            && Objective == StoryObjective.TryExit)
        {
            silenceMessageShown = true;
            messages.Enqueue("Debo moverme en silencio para que no me encuentren");
        }
        if (!loopCrossed && Objective == StoryObjective.FindPhone && current == childRoom && previous == entrance.Room)
        {
            loopCrossed = true;
            clearEntrancePending = true;
            // El recibidor debe liberar el espacio del único acceso del cuarto del niño.
            // El cierre espera a que todos terminen de atravesar los sensores.
            var toClose = new List<RoomConnection>(house.Connections);
            foreach (RoomConnection connection in toClose)
                if (connection.SourceRoom == entrance.Room || connection.TargetRoom == entrance.Room)
                    house.CloseAfterPassage(connection, player.gameObject);
        }
        nextGuide = 0f;
    }
    public bool ShouldLoopExit(DoorSocket socket) => Objective == StoryObjective.TryExit && socket == entrance;
    public void CompleteExit()
    {
        if (Objective != StoryObjective.TryExit) return;
        Advance(StoryObjective.FindPhone, "Que está pasando? Tengo que pedir ayuda, buscaré el teléfono en la oficina");
    }
    public void ReviewPhone(GameObject actor)
    {
        if (Objective != StoryObjective.FindPhone || actor != player.gameObject || house.CurrentRoom != officeRoom) return;
        Advance(StoryObjective.Survive, "No hay señal, debo esperar a que amanezca", "NO ME ATRAPARÁN");
        session.StartSurvival(survivalSeconds);
    }
    private void Update()
    {
        if (session == null || !session.IsPlaying) { RestoreDoorMaterial(); return; }
        if (clearEntrancePending && house.TryParkIsolatedRoom(entrance.Room)) clearEntrancePending = false;
        if (Time.time >= messageUntil || currentMessage.Length == 0)
        {
            currentMessage = messages.Count > 0 ? messages.Dequeue() : "";
            messageUntil = Time.time + Mathf.Max(4f, currentMessage.Length * .07f);
        }
        if (Time.time >= nextGuide)
        {
            nextGuide = Time.time + .5f;
            RefreshGuide();
        }
        UpdateDoorMaterial();
    }
    private void RefreshGuide()
    {
        GuideDoor = null;
        if (player == null || house.CurrentRoom == null || Objective == StoryObjective.Survive) return;
        RoomModule target = Objective == StoryObjective.FindParents ? parentsRoom
            : Objective == StoryObjective.TryExit ? entrance.Room : officeRoom;
        if (house.CurrentRoom == target)
        {
            if (Objective == StoryObjective.TryExit) GuideDoor = entrance;
        }
        else GuideDoor = NextDoor(target);
    }
    private void UpdateDoorMaterial()
    {
        Renderer desired = GuideDoorClosed ? GuideDoor.Door.LeafRenderer : null;
        if (desired == highlightedDoor) return;
        RestoreDoorMaterial();
        if (desired == null) return;
        highlightedDoor = desired;
        originalDoorMaterials = desired.sharedMaterials;
        Material[] replacement = new Material[originalDoorMaterials.Length];
        for (int i = 0; i < replacement.Length; i++) replacement[i] = objectiveDoorMaterial != null ? objectiveDoorMaterial : runtimeDoorMaterial;
        desired.sharedMaterials = replacement;
    }
    private void RestoreDoorMaterial()
    {
        // Conserva los materiales originales para recuperarlos al abrir, cambiar de objetivo o terminar la partida.
        if (highlightedDoor != null) highlightedDoor.sharedMaterials = originalDoorMaterials;
        highlightedDoor = null;
        originalDoorMaterials = null;
    }
    private void OnDisable() { RestoreDoorMaterial(); }
    private DoorSocket NextDoor(RoomModule target)
    {
        var queue = new Queue<RoomModule>();
        var first = new Dictionary<RoomModule, DoorSocket>();
        queue.Enqueue(house.CurrentRoom); first[house.CurrentRoom] = null;
        while (queue.Count > 0)
        {
            RoomModule room = queue.Dequeue();
            foreach (DoorSocket socket in room.Sockets)
            {
                RoomConnection connection = house.ConnectionFor(socket);
                RoomModule next = connection != null
                    ? connection.SourceRoom == room ? connection.TargetRoom : connection.SourceRoom
                    : house.NormalDestination(socket)?.Room;
                if (next == null || first.ContainsKey(next)) continue;
                first[next] = first[room] != null ? first[room] : socket;
                if (next == target) return first[next];
                queue.Enqueue(next);
            }
        }
        return null;
    }
    private void OnGUI()
    {
        if (session == null || !session.IsPlaying) return;
        float scale = Screen.height / 720f;
        var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(27f * scale), alignment = TextAnchor.MiddleCenter, wordWrap = true };
        if (currentMessage.Length > 0)
        {
            var rect = new Rect(Screen.width * .15f, Screen.height * .32f, Screen.width * .7f, 105f * scale);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, currentMessage, style);
        }
        style.fontSize = Mathf.RoundToInt(19f * scale);
        string objective = Objective == StoryObjective.FindParents ? "Buscar a mamá y papá"
            : Objective == StoryObjective.TryExit ? "Intentar salir de la casa"
            : Objective == StoryObjective.FindPhone ? "Buscar el celular en la oficina" : "Sobrevivir hasta el amanecer";
        GUI.Label(new Rect(Screen.width * .2f, 12f * scale, Screen.width * .6f, 40f * scale), objective, style);
        if (Objective == StoryObjective.FindPhone && house.CurrentRoom == officeRoom && Camera.main != null)
        {
            Vector3 point = Camera.main.WorldToScreenPoint(phone.transform.position + Vector3.up * .18f);
            if (point.z > 0) GUI.Label(new Rect(point.x - 80f * scale, Screen.height - point.y - 25f * scale, 160f * scale, 30f * scale), "▼ Celular", style);
        }
    }
}
