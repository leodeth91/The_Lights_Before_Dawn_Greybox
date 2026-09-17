#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Pruebas opcionales que comprueban las mecánicas dentro de la escena real de Unity.</summary>
public sealed class AlienRuntimeSmokeTest : MonoBehaviour
{
    private readonly List<string> failures = new List<string>();
    private readonly List<string> observations = new List<string>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!SessionState.GetBool("TLBD.AlienSmoke", false)) return;
        SessionState.SetBool("TLBD.AlienSmoke", false);
        Application.runInBackground = true;
        new GameObject("ALIEN_SMOKE_TEST").AddComponent<AlienRuntimeSmokeTest>();
    }
    private void Check(bool condition, string message)
    { if (!condition) failures.Add(message); }
    private void Capture(string message, string trace, LogType type)
    { if (type == LogType.Error || type == LogType.Exception) failures.Add(message); }
    private IEnumerator Start()
    {
        Application.logMessageReceived += Capture;
        yield return null;
        HouseFlowController house = HouseFlowController.Instance;
        AlienController[] aliens = FindObjectsByType<AlienController>(FindObjectsSortMode.None);
        Check(house != null && house.ModuleById.Count == 11 && house.RegisteredSocketCount == 27, "House registration failed");
        Check(aliens.Length == 2, "Expected two aliens");
        AlienActivityDirector activity = FindFirstObjectByType<AlienActivityDirector>();
        Check(activity != null, "Alien activity director is missing");
        if (activity != null)
        {
            Check(activity.GetTuningForProgress(0f).Phase == AlienActivityPhase.Cautious
                && activity.GetTuningForProgress(.5f).Phase == AlienActivityPhase.Active
                && activity.GetTuningForProgress(.9f).Phase == AlienActivityPhase.Intense,
                "Alien activity phases do not follow game progress");
        }
        Check(aliens.All(a => a.Occupant.Room.RoomId == "foyer"), "Aliens must start in foyer");
        Check(aliens.All(a => a.Motor.Agent.isOnNavMesh), "Alien spawn outside NavMesh");
        Check(aliens.All(a => a.GetComponent<AlienAmbientAudio>()?.IsReady == true),
            "Alien procedural footsteps or vocal sources are not ready");
        Check(aliens.All(a => a.GetComponent<AlienHearing>() != null
            && a.GetComponent<SoundResponsibility>() != null),
            "Alien hearing or sound responsibility is missing from the prefab");
        Check(house.ModuleById.Values.Sum(room => room.GetComponentsInChildren<DoorSoundEmitter>(true).Length)
            == house.RegisteredSocketCount, "Not every registered door has a sound emitter");
        foreach (AlienController alien in aliens) alien.SetPlayerDetectionEnabled(false);
        Check(house.Occupants.Count == 3, "Expected three occupants");
        Check(house.CurrentRoom.RoomId == "child_room", "Player room changed by alien spawn");
        PlayerFootstepAudio footsteps = FindFirstObjectByType<PlayerFootstepAudio>();
        Check(footsteps != null && footsteps.IsReady,
            "Player procedural footsteps or their internal/external sources are not ready");
        Check(house.ModuleById["stair_hall"].transform.Find("Geometria_ProBuilder/Escalera_ProBuilder") != null, "Single ProBuilder stairs missing");
        RoomModule hall = house.ModuleById["stair_hall"];
        hall.gameObject.SetActive(true);
        StairInterest[] accesses = hall.GetComponentsInChildren<StairInterest>();
        var filter = new NavMeshQueryFilter { agentTypeID = RoomNavigation.AgentType, areaMask = NavMesh.AllAreas };
        foreach (StairInterest access in accesses)
        {
            NavMeshPath path = new NavMeshPath();
            bool reachable = NavMesh.SamplePosition(access.ApproachPoint, out NavMeshHit from, .5f, filter)
                && NavMesh.SamplePosition(access.Destination, out NavMeshHit to, .5f, filter)
                && NavMesh.CalculatePath(from.position, to.position, filter, path)
                && path.status == NavMeshPathStatus.PathComplete;
            Check(reachable, "Stair route unavailable: " + access.name);
        }
        Check(accesses.Length == 2, "Stair access markers missing");
        hall.gameObject.SetActive(false);
        GameSessionManager.Instance.BeginGame();
        Time.timeScale = 5;
        yield return new WaitForSeconds(1.5f);
        Check(aliens.All(a => a.StateName == "Espera"), "Aliens activated before 3 seconds");
        int opened = 0, closed = 0, crossings = 0;
        bool observingPatrol = true;
        var visited = new Dictionary<AlienController, HashSet<string>>();
        foreach (AlienController alien in aliens) visited[alien] = new HashSet<string> { "foyer" };
        house.ConnectionOpened += c => { opened++; if (observingPatrol) Check(!c.IsAnomalous, "Alien opened an anomalous connection"); };
        house.ConnectionClosed += c => closed++;
        float deadline = Time.time + 100;
        while (Time.time < deadline)
        {
            foreach (AlienController alien in aliens)
            {
                Check(alien.Occupant.Room != null && alien.Occupant.Room.gameObject.activeSelf, "Occupied room was pooled");
                Check(alien.Motor.Agent.isOnNavMesh, "Alien left NavMesh");
                if (visited[alien].Add(alien.Occupant.Room.RoomId)) crossings++;
            }
            yield return new WaitForSeconds(1);
        }
        foreach (AlienController alien in aliens)
            observations.Add(alien.name + " rooms=" + string.Join(",", visited[alien]) + " state=" + alien.StateName
                + " position=" + alien.transform.position + " visible=" + alien.Visible.Count);
        Check(opened > 0, "No alien opened a door during integration run");
        Check(crossings > 0, "No alien crossed into another room");
        Check(closed > 0, "No alien closed a door after crossing");
        observations.Add("Connections opened=" + opened + ", closed=" + closed + ", crossings=" + crossings);
        observingPatrol = false; // Las siguientes pruebas dejan que el player active las anomalías.
        // Esta prueba no juega los objetivos: prepara expresamente la etapa que permite anomalías.
        if (StoryProgression.Instance != null)
            typeof(StoryProgression).GetMethod("Advance", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).Invoke(StoryProgression.Instance,
                    new object[] { StoryObjective.FindPhone, new string[0] });
        yield return AlienCoordinationChecks.Run(Check);
        observations.Add("Checked simultaneous connections, passage reservations, shared rooms, cross-sector player/alien encounters and physical stair ascent/descent.");
        observations.Add("Checked retained anomalous door reopening, physical hunter crossing, close behind, expired leases, deduplicated queued reopening and automatic close.");
        GameSessionManager.Instance.SetSurvivalDuration(.1f);
        yield return new WaitForSecondsRealtime(.3f);
        Check(GameSessionManager.Instance.State == GameSessionState.Victory,
            "Survival countdown did not trigger victory");
        observations.Add("Checked that the survival countdown triggers victory.");
        DontDestroyOnLoad(gameObject);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        yield return null;
        yield return null;
        AlienController[] restarted = FindObjectsByType<AlienController>(FindObjectsSortMode.None);
        Check(restarted.Length == 2 && HouseFlowController.Instance.Occupants.Count == 3,
            "Restart did not restore both aliens and player occupancy");
        Check(restarted.All(a => a.Occupant.Room.RoomId == "foyer" && a.StateName == "Espera" && a.Motor.Agent.isOnNavMesh),
            "Restart did not restore the initial alien state");
        observations.Add("Checked scene restart.");
        File.WriteAllText("Temp/AlienTestResult.txt", "FAILURES=" + failures.Count + "\n" + string.Join("\n", failures.Distinct()) + "\n" + string.Join("\n", observations));
        Application.logMessageReceived -= Capture;
        Time.timeScale = 1;
        EditorApplication.ExitPlaymode();
    }
}
#endif
