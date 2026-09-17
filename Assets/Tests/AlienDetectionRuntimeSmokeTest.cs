#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class AlienDetectionRuntimeSmokeTest : MonoBehaviour
{
    private readonly List<string> failures = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!SessionState.GetBool("TLBD.DetectionSmoke", false)) return;
        SessionState.SetBool("TLBD.DetectionSmoke", false);
        Application.runInBackground = true;
        new GameObject("ALIEN_DETECTION_SMOKE_TEST").AddComponent<AlienDetectionRuntimeSmokeTest>();
    }

    private void Check(bool condition, string message)
    {
        if (!condition) failures.Add(message);
    }

    private IEnumerator Start()
    {
        yield return null;
        CheckDirectionalSearch();
        HouseFlowController house = HouseFlowController.Instance;
        AlienController[] aliens = FindObjectsByType<AlienController>(FindObjectsSortMode.None);
        PlayerVisibility player = FindFirstObjectByType<PlayerVisibility>();
        Check(house != null && aliens.Length == 2 && player != null, "Detection fixture missing");
        if (house == null || aliens.Length == 0 || player == null) { Finish(); yield break; }
        foreach (AlienController alien in aliens) alien.SetPlayerDetectionEnabled(false);
        GameSessionManager.Instance.BeginGame();
        Time.timeScale = 4f;
        yield return new WaitForSeconds(3.2f);
        Time.timeScale = 1f;

        AlienController subject = aliens[0];
        AlienController companion = aliens[1];
        subject.StopAllCoroutines();
        companion.StopAllCoroutines();
        companion.Motor.Stop();
        RoomModule foyer = house.ModuleById["foyer"];
        house.RegisterOccupant(subject.gameObject, foyer, true);
        house.RegisterOccupant(player.gameObject, foyer, false);
        CharacterController character = player.GetComponent<CharacterController>();
        if (character != null) character.enabled = false;
        bool fixtureFound = FindVisibleFixture(subject, player, foyer, out Vector3 alienPoint,
            out Vector3 playerPoint);
        Check(fixtureFound, "Could not place a clear player-vision fixture in foyer");
        subject.Motor.Agent.Warp(alienPoint);
        Vector3 awayFromPlayer = alienPoint - playerPoint;
        awayFromPlayer.y = 0f;
        Vector3 companionTarget = alienPoint + (awayFromPlayer.sqrMagnitude > .01f
            ? awayFromPlayer.normalized * 1.2f : foyer.transform.forward * 1.2f);
        if (companion.Motor.TryPoint(companionTarget, 1.5f, out Vector3 companionPoint))
            companion.Motor.Agent.Warp(companionPoint);
        house.RegisterOccupant(companion.gameObject, foyer, true);
        player.transform.position = playerPoint;
        if (character != null) character.enabled = true;
        AimHead(subject, player.PerceptionTransform.position);
        Physics.SyncTransforms();

        PlayerFootstepAudio footsteps = player.GetComponent<PlayerFootstepAudio>();
        Check(footsteps != null, "Running-footstep hearing fixture missing");
        footsteps?.PlayRunningStep();
        yield return new WaitForSeconds(.38f);
        Check(aliens.Any(alien => alien.StateName == "Pregunta por el ruido"),
            "Running footsteps did not promptly trigger the question vocalization state");
        AlienController listener = aliens.FirstOrDefault(alien => alien.StateName == "Pregunta por el ruido");
        if (listener != null)
        {
            Vector3 newerPosition = player.transform.position + foyer.transform.right * .4f;
            SoundSignalSystem.EmitSuspicious(SuspiciousSoundKind.PlayerRunningStep,
                newerPosition, player.gameObject, foyer);
            Check(listener.FocusPoint.HasValue && Vector3.Distance(listener.FocusPoint.Value, newerPosition) < .01f,
                "Running footsteps were discarded during an active sound investigation");
        }
        foreach (AlienController alien in aliens)
        {
            alien.SetHearingEnabled(false);
            alien.StopAllCoroutines();
            alien.Motor.Stop();
            alien.SetHearingEnabled(true);
        }

        // Prueba una posición elevada sin cambiar la escena guardada ni el mobiliario del usuario.
        Vector3 originalPosition = player.transform.position;
        if (character != null) character.enabled = false;
        player.transform.position += Vector3.up;
        if (character != null) character.enabled = true;
        AimHead(subject, player.PerceptionTransform.position);
        Physics.SyncTransforms();
        Check(subject.Perception.CanSeePlayer(player, subject.Head.Head, foyer, house),
            "An unobstructed elevated player is invisible");
        Vector3 reachPoint = player.transform.position + new Vector3(.4f, .35f, 0f);
        Check(player.DistanceToBody(reachPoint) <= subject.CatchDistance,
            "Capture reach measures feet instead of the elevated player's body");
        if (character != null) character.enabled = false;
        player.transform.position = originalPosition;
        if (character != null) character.enabled = true;
        CheckCrouchCover(subject, player, character);
        AimHead(subject, player.PerceptionTransform.position);
        Physics.SyncTransforms();

        float alertStarted = -1f, chaseStarted = -1f;
        subject.OnStateChanged.AddListener(state =>
        {
            if (state == "Alerta" && alertStarted < 0f) alertStarted = Time.time;
            if (state == "Persigue" && chaseStarted < 0f) chaseStarted = Time.time;
        });
        subject.SetPlayerDetectionEnabled(true);
        float deadline = Time.time + 2f;
        while (alertStarted < 0f && Time.time < deadline) yield return null;
        Check(alertStarted >= 0f, "Visible player did not immediately trigger Alert");
        Check(companion.LastPlayerCallSender == subject,
            "Nearby companion did not receive the player's visual alert call");
        companion.SetHearingEnabled(false);
        companion.StopAllCoroutines();
        companion.Motor.Stop();
        Check(subject.Memory.LastKnownRoom == foyer, "Memory did not store the player's room");
        deadline = Time.time + 2f;
        while (chaseStarted < 0f && Time.time < deadline) yield return null;
        Check(chaseStarted >= 0f, "Alert did not transition to Chase");
        if (alertStarted >= 0f && chaseStarted >= 0f)
        {
            float duration = chaseStarted - alertStarted;
            Check(duration >= .86f && duration <= 1.20f,
                "Alert duration outside configured 0.90-1.15 seconds: " + duration);
        }
        Vector3 gaze = (player.PerceptionTransform.position - subject.Head.Head.position).normalized;
        Check(Vector3.Dot(subject.Head.Head.forward, gaze) > .75f,
            "Alien head did not follow the player during Alert");

        float distanceBeforeChase = Vector3.Distance(subject.transform.position,
            player.transform.position);
        yield return new WaitForSeconds(.35f);
        float distanceAfterChase = Vector3.Distance(subject.transform.position,
            player.transform.position);
        Check(distanceAfterChase < distanceBeforeChase - .08f,
            "Chase state did not move the alien toward the visible player");

        Vector3 remembered = subject.Memory.LastKnownPosition;
        GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.name = "DetectionTest_Blocker";
        blocker.transform.position = Vector3.Lerp(subject.Head.Head.position,
            player.PerceptionTransform.position, .5f);
        Vector3 blockerForward = player.transform.position - subject.transform.position;
        blockerForward.y = 0f;
        if (blockerForward.sqrMagnitude > .001f)
            blocker.transform.rotation = Quaternion.LookRotation(blockerForward.normalized,
                Vector3.up);
        blocker.transform.localScale = new Vector3(3.2f, 3f, .35f);
        Physics.SyncTransforms();
        deadline = Time.time + .6f;
        while (subject.PlayerVisible && Time.time < deadline) yield return null;
        Check(!subject.PlayerVisible, "Solid obstacle did not break player line of sight");
        player.transform.position += foyer.transform.right * 1.1f;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.25f);
        Check(Vector3.Distance(subject.Memory.LastKnownPosition, remembered) < .15f,
            "Memory followed the hidden player's real position");
        subject.SetPlayerDetectionEnabled(false);
        yield return new WaitForSeconds(.2f);
        Vector3 distanceToMemory = subject.Memory.LastKnownPosition - subject.transform.position;
        distanceToMemory.y = 0f;
        if (distanceToMemory.magnitude > .6f && subject.Motor.Agent.hasPath)
            Check(subject.StateName == "Persigue", "Chase abandoned a reachable remembered position after brief occlusion");
        deadline = Time.time + 14f;
        while (subject.StateName != "Busca" && Time.time < deadline) yield return null;
        Check(subject.StateName == "Busca", "Losing sight did not begin Search");
        Destroy(blocker);
        // Reencontrarlo durante la búsqueda debe reanudar la persecución sin repetir la sorpresa.
        int repeatedAlerts = 0;
        subject.OnStateChanged.AddListener(state => { if (state == "Alerta") repeatedAlerts++; });
        subject.Motor.Stop();
        subject.Motor.Agent.Warp(alienPoint);
        if (character != null) character.enabled = false;
        player.transform.position = playerPoint;
        if (character != null) character.enabled = true;
        AimHead(subject, player.PerceptionTransform.position);
        Physics.SyncTransforms();
        subject.SetPlayerDetectionEnabled(true);
        deadline = Time.time + 1f;
        while (subject.StateName == "Busca" && Time.time < deadline) yield return null;
        Check(repeatedAlerts == 0, "Reacquiring the player repeated the initial alert");
        Check(subject.StateName == "Persigue", "Reacquiring during search did not resume chase immediately");

        // Un alien ve al jugador esconderse y el otro queda en otra habitación. Solo el primero debe recordar lo ocurrido.
        subject.SetPlayerDetectionEnabled(false);
        subject.StopAllCoroutines();
        RoomModule living = house.ModuleById["living_dining_room"];
        living.gameObject.SetActive(true);
        HideSpot witnessedSpot = living.GetComponentInChildren<HideSpot>(true);
        PlayerHidingController hiding = player.HidingController;
        Check(witnessedSpot != null && hiding != null, "Hiding witness fixture missing");
        if (witnessedSpot != null && hiding != null)
        {
            house.RegisterOccupant(subject.gameObject, living, true);
            house.RegisterOccupant(player.gameObject, living, false);
            AlienController unwitnessed = aliens[1];
            unwitnessed.Memory.ClearSearch();
            unwitnessed.SetPlayerDetectionEnabled(true);
            house.RegisterOccupant(unwitnessed.gameObject, foyer, true);
            if (unwitnessed.Motor.TryPoint(foyer.transform.position, 2f, out Vector3 otherPoint))
                unwitnessed.Motor.Agent.Warp(otherPoint);
            if (character != null) character.enabled = false;
            player.transform.SetPositionAndRotation(witnessedSpot.EntryPoint.position,
                witnessedSpot.EntryPoint.rotation);
            if (character != null) character.enabled = true;
            bool witnessPlaced = FindWitnessPoint(subject, player, living,
                out Vector3 witnessPoint);
            Check(witnessPlaced, "Could not place an alien with sight of the hiding entry");
            if (witnessPlaced) subject.Motor.Agent.Warp(witnessPoint);
            AimHead(subject, player.PerceptionTransform.position);
            subject.SetPlayerDetectionEnabled(true);
            Physics.SyncTransforms();
            hiding.Toggle(witnessedSpot);
            yield return null;
            Check(subject.Memory.KnownHidingSpot == witnessedSpot,
                "Witnessing Entering did not remember the exact hiding spot");
            Check(subject.StateName == "Revisa escondite conocido",
                "Witnessing Entering did not immediately prioritize the remembered hiding spot");
            Check(unwitnessed.Memory.KnownHidingSpot == null,
                "An alien in another room learned the hiding spot omnisciently");

            // Deja terminar la entrada al escondite sin que el alien finalice la partida. Después prueba directamente si el escondite está ocupado.
            subject.SetPlayerDetectionEnabled(false);
            subject.StopAllCoroutines();
            subject.FinishInspection();
            deadline = Time.time + 2.5f;
            while (!hiding.IsHidden && Time.time < deadline) yield return null;
            Check(hiding.IsHidden && witnessedSpot.IsOccupied,
                "Player did not become an occupant of the selected hiding spot");
            Check(!subject.Perception.CanSeePlayer(player, subject.Head.Head, living, house),
                "A hidden player remained visible to distant alien raycasts");
            Check(subject.TryBeginInspection(witnessedSpot),
                "Alien could not begin inspection of an occupied hiding spot");
            Check(subject.CatchRequested,
                "Inspecting an occupied hiding spot did not request player capture");
            subject.FinishInspection();
        }
        if (hiding.IsHidden && witnessedSpot != null)
        {
            hiding.Toggle(witnessedSpot);
            yield return new WaitForSeconds(3f);
        }
        yield return CheckKitchenCounter(subject, companion, player, character, house);
        Finish();
    }

    private IEnumerator CheckKitchenCounter(AlienController subject, AlienController companion,
        PlayerVisibility player, CharacterController character, HouseFlowController house)
    {
        subject.SetPlayerDetectionEnabled(false);
        companion.SetPlayerDetectionEnabled(false);
        subject.StopAllCoroutines(); companion.StopAllCoroutines();
        subject.Motor.Stop(); companion.Motor.Stop();
        subject.Memory.ClearSearch(); companion.Memory.ClearSearch();
        RoomModule kitchen = house.ModuleById["kitchen"];
        house.RegisterOccupant(subject.gameObject, kitchen, true);
        house.RegisterOccupant(player.gameObject, kitchen, false);
        Transform counterRoot = kitchen.GetComponentsInChildren<Transform>()
            .FirstOrDefault(item => item.name == "Mesada_Oeste");
        Collider counter = counterRoot != null ? counterRoot.GetComponentsInChildren<Collider>()
            .Where(item => !item.isTrigger).OrderByDescending(item => item.bounds.max.y).FirstOrDefault() : null;
        Check(counter != null, "Kitchen countertop fixture missing");
        if (counter == null) yield break;
        var movement = player.GetComponent<ChildPlayerController>();
        movement.enabled = false; // Mantiene al Player quieto sobre la mesada durante la prueba.
        character.enabled = false;
        Vector3 position = counter.bounds.center;
        position.y = counter.bounds.max.y + .04f;
        player.transform.position = position;
        character.enabled = true;
        bool placed = false;
        foreach (Vector3 offset in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
        {
            Vector3 point = position + offset * 2f;
            point.y = kitchen.transform.position.y + .05f;
            if (!subject.Motor.TryPoint(point, .8f, out Vector3 ground)) continue;
            subject.Motor.Agent.Warp(ground);
            AimHead(subject, player.PerceptionTransform.position);
            Physics.SyncTransforms();
            if (!subject.Perception.CanSeePlayer(player, subject.Head.Head, kitchen, house)) continue;
            placed = true;
            break;
        }
        Check(placed, "Alien cannot see the player on the kitchen countertop");
        if (placed)
        {
            subject.SetPlayerDetectionEnabled(true);
            float end = Time.time + 12f;
            while (GameSessionManager.Instance.IsPlaying && Time.time < end) yield return null;
            Check(GameSessionManager.Instance.State == GameSessionState.Defeat,
                "Alien cannot approach and capture the player on the kitchen countertop: state=" + subject.StateName
                + " alien=" + subject.transform.position + " player=" + player.transform.position
                + " distance=" + player.DistanceToBody(subject.Head.Head.position) + " visible=" + subject.PlayerVisible);
        }
        movement.enabled = true;
    }

    private void CheckDirectionalSearch()
    {
        // Comprueba la preferencia sin depender del mobiliario que el usuario pueda reacomodar.
        var points = new List<AlienSearchPoint>();
        for (int i = 0; i < 3; i++)
        {
            var marker = new GameObject("PuntoPruebaBusqueda").AddComponent<AlienSearchPoint>();
            marker.transform.position = new Vector3(0f, 0f, i == 2 ? -2f : 2f);
            marker.Configure(i == 1 ? AlienSearchPointKind.Door : AlienSearchPointKind.HidingSpot, 1f);
            points.Add(marker);
        }
        AlienSearchPoint behind = points[2];
        var strategy = new DirectionalSearchStrategy();
        for (int i = 0; i < 2; i++)
        {
            AlienSearchPoint selected = strategy.Select(points, Vector3.zero, Vector3.forward, Vector3.zero);
            Check(selected != null && selected != behind,
                "Directional search did not prioritize forward hiding spots and exits");
            points.Remove(selected);
            if (selected != null) Destroy(selected.gameObject);
        }
        foreach (AlienSearchPoint point in points) if (point != null) Destroy(point.gameObject);
    }

    private static bool FindVisibleFixture(AlienController alien, PlayerVisibility player,
        RoomModule room, out Vector3 alienPoint, out Vector3 playerPoint)
    {
        Vector3[] starts =
        {
            new Vector3(-1.6f, .04f, 0f), new Vector3(1.6f, .04f, 0f),
            new Vector3(0f, .04f, -1.5f), new Vector3(0f, .04f, 1.5f)
        };
        for (int i = 0; i < starts.Length; i++)
        {
            Vector3 a = room.transform.TransformPoint(starts[i]);
            Vector3 p = room.transform.TransformPoint(starts[(i + 1) % starts.Length]);
            if (!alien.Motor.TryPoint(a, 1f, out a)) continue;
            alien.Motor.Agent.Warp(a);
            player.transform.position = p;
            AimHead(alien, player.PerceptionTransform.position);
            Physics.SyncTransforms();
            if (alien.Perception.CanSeePlayer(player, alien.Head.Head, room,
                HouseFlowController.Instance))
            { alienPoint = a; playerPoint = p; return true; }
        }
        alienPoint = room.transform.position; playerPoint = room.transform.position;
        return false;
    }

    private static void AimHead(AlienController alien, Vector3 target)
    {
        Vector3 direction = target - alien.Head.Head.position;
        if (direction.sqrMagnitude > .001f)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (flat.sqrMagnitude > .001f)
                alien.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            alien.Head.Head.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    private void CheckCrouchCover(AlienController alien, PlayerVisibility player,
        CharacterController character)
    {
        ChildPlayerController movement = player.GetComponent<ChildPlayerController>();
        Transform body = player.transform.Find("PlayerVisual/Visual_Cuerpo");
        Check(movement != null && body != null, "Crouch visual fixture missing");
        if (movement == null || body == null) return;

        movement.SetCrouched(false, true);
        float standingBodyHeight = body.localScale.y;
        GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cover.name = "DetectionTest_CrouchCover";
        Vector3 sightDirection = player.transform.position - alien.Head.Head.position;
        sightDirection.y = 0f;
        if (sightDirection.sqrMagnitude > .001f)
            cover.transform.rotation = Quaternion.LookRotation(sightDirection.normalized,
                Vector3.up);
        Vector3 position = Vector3.Lerp(alien.Head.Head.position,
            player.PerceptionTransform.position, .88f);
        position.y = player.transform.position.y + .45f;
        cover.transform.position = position;
        cover.transform.localScale = new Vector3(2.2f, .90f, .18f);
        Physics.SyncTransforms();

        Check(alien.Perception.CanSeePlayer(player, alien.Head.Head,
                alien.Occupant.Room, HouseFlowController.Instance),
            "Low cover incorrectly hid the standing player");
        movement.SetCrouched(true, true);
        Physics.SyncTransforms();
        Check(character != null
                && Mathf.Abs(character.height - movement.CrouchedHeight) < .01f,
            "Crouching did not resize the CharacterController");
        Check(body.localScale.y <= standingBodyHeight * .51f,
            "Crouching did not reduce the player's torso to half height");
        Check(!alien.Perception.CanSeePlayer(player, alien.Head.Head,
                alien.Occupant.Room, HouseFlowController.Instance),
            "Low solid cover did not hide the crouched player");
        cover.SetActive(false);
        Physics.SyncTransforms();
        Check(alien.Perception.CanSeePlayer(player, alien.Head.Head,
                alien.Occupant.Room, HouseFlowController.Instance),
            "Crouching alone hid an otherwise exposed player");
        movement.SetCrouched(false, true);
        Destroy(cover);
    }

    private static bool FindWitnessPoint(AlienController alien, PlayerVisibility player,
        RoomModule room, out Vector3 result)
    {
        Vector3 center = player.transform.position;
        Vector3[] offsets = { Vector3.forward * 2.2f, Vector3.back * 2.2f,
            Vector3.left * 2.2f, Vector3.right * 2.2f };
        foreach (Vector3 offset in offsets)
        {
            if (!alien.Motor.TryPoint(center + offset, 1.4f, out Vector3 point)) continue;
            alien.Motor.Agent.Warp(point);
            AimHead(alien, player.PerceptionTransform.position);
            Physics.SyncTransforms();
            if (alien.Perception.CanSeePlayer(player, alien.Head.Head, room,
                HouseFlowController.Instance)) { result = point; return true; }
        }
        result = center;
        return false;
    }

    private void Finish()
    {
        Time.timeScale = 1f;
        File.WriteAllText("Temp/DetectionTestResult.txt",
            "FAILURES=" + failures.Count + "\n" + string.Join("\n", failures));
        EditorApplication.ExitPlaymode();
    }
}
#endif
