#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public sealed class StoryRuntimeSmokeTest : MonoBehaviour
{
    private readonly List<string> failures = new List<string>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!SessionState.GetBool("TLBD.StorySmoke", false)) return;
        SessionState.SetBool("TLBD.StorySmoke", false);
        Application.runInBackground = true;
        new GameObject("PruebaHistoria").AddComponent<StoryRuntimeSmokeTest>();
    }
    private void Check(bool condition, string message) { if (!condition) failures.Add(message); }
    private IEnumerator Start()
    {
        Application.logMessageReceived += Capture;
        yield return null;
        var story = StoryProgression.Instance;
        var house = HouseFlowController.Instance;
        var session = GameSessionManager.Instance;
        var player = FindFirstObjectByType<PlayerInteractor>();
        Check(story != null && house != null && session != null && player != null, "Faltan componentes de historia");
        if (failures.Count > 0) { Finish(); yield break; }
        CheckAnomalyCompatibility();
        CheckRandomRoomSelection(house);
        session.BeginGame();
        foreach (var alien in FindObjectsByType<AlienController>(FindObjectsSortMode.None))
        { alien.StopAllCoroutines(); alien.enabled = false; }
        yield return new WaitForSeconds(.2f);
        Check(!session.HasSurvivalTimer && session.Progress01 == 0f, "El contador comenzó antes del celular");
        Check(session.ElapsedPlayTime > 0f, "El reloj de actividad no cuenta los objetivos iniciales");
        var doorState = new DoorEntity("test");
        Check(doorState.TryBeginOpen(), "No inicia apertura");
        doorState.CompleteTransition(true);
        doorState.TryBeginClose();
        Check(doorState.TryBeginOpen(), "No puede invertir una puerta que se está cerrando");
        Check(!story.AnomalyEnabled, "La anomalía comenzó antes del intento de salida");
        Check(story.GuideDoor != null && story.GuideDoor.Room == house.CurrentRoom, "No se indicó la puerta del siguiente objetivo");
        Renderer markedLeaf = story.GuideDoor != null ? story.GuideDoor.Door.LeafRenderer : null;
        Check(markedLeaf != null && markedLeaf.sharedMaterial.color.r > .8f && markedLeaf.sharedMaterial.color.g < .2f,
            "La puerta del objetivo no tiene material rojo");
        var parents = house.ModuleById["parents_bedroom"];
        MovePlayer(player, house, parents, parents.transform.position);
        yield return null;
        Check(story.Objective == StoryObjective.TryExit, "Entrar al cuarto de los padres no completó el objetivo");
        var foyer = house.ModuleById["foyer"];
        MovePlayer(player, house, foyer, foyer.transform.position);
        parents.gameObject.SetActive(false);
        var child = house.ModuleById["child_room"];
        child.gameObject.SetActive(false);
        DoorSocket exit = foyer.Sockets.Single(socket => socket.SocketId == "foyer_south");
        Material originalExitMaterial = exit.Door.LeafRenderer.sharedMaterial;
        MovePlayer(player, house, foyer, exit.ClearApproachPoint);
        yield return null;
        yield return new WaitForSeconds(.6f);
        Check(exit.Door.LeafRenderer.sharedMaterial.color.r > .8f && exit.Door.LeafRenderer.sharedMaterial.color.g < .2f,
            "La salida del objetivo no se destacó en rojo");
        house.RequestOpen(exit, player.gameObject);
        float deadline = Time.time + 12f;
        while (house.ConnectionFor(exit) == null && Time.time < deadline) yield return null;
        RoomConnection connection = house.ConnectionFor(exit);
        Check(connection != null && connection.TargetRoom == child, "La salida no conectó primero con el cuarto del niño");
        yield return null;
        Check(exit.Door.LeafRenderer.sharedMaterial == originalExitMaterial, "La puerta no recuperó su material original al abrir");
        Check(story.Objective == StoryObjective.FindPhone && story.AnomalyEnabled, "La salida no activó el objetivo del celular y la anomalía");
        Check(!session.HasSurvivalTimer, "El intento de salida comenzó el contador");
        // Comprueba que se pueda salir del cuarto reubicado y continuar hacia la oficina.
        // Solo esta prueba desactiva el azar para verificar un recorrido reproducible.
        var configuration = new SerializedObject(house);
        configuration.FindProperty("randomizeEveryEligibleDoor").boolValue = false;
        configuration.FindProperty("anomalyChance").floatValue = 0f;
        configuration.ApplyModifiedPropertiesWithoutUndo();
        if (connection != null)
        {
            yield return CheckThresholdClose(house, player, connection);
            MovePlayer(player, house, child, child.transform.position);
            house.CloseAfterPassage(connection, player.gameObject);
            yield return new WaitForSeconds(2f);
            DoorSocket next = child.Sockets.Single(socket => socket.SocketId == "child_east");
            MovePlayer(player, house, child, next.ClearApproachPoint);
            house.RequestOpen(next, player.gameObject);
            deadline = Time.time + 12f;
            while (house.ConnectionFor(next) == null && Time.time < deadline) yield return null;
            var onward = house.ConnectionFor(next);
            Check(onward != null, "El cuarto del niño reubicado quedó sin salida");
            if (onward == null)
                File.WriteAllText("Temp/StoryRouteDiagnostic.txt", "current=" + house.CurrentRoom.RoomId
                    + " requests=" + house.PendingRequestCount + " childDoor=" + next.Door.State
                    + " foyer=" + foyer.transform.position + " child=" + child.transform.position
                    + " connections=" + string.Join(",", house.Connections.Select(item => item.SourceRoom.RoomId + "->" + item.TargetRoom.RoomId))
                    + " active=" + string.Join(",", house.ModuleById.Values.Where(item => item.gameObject.activeSelf).Select(item => item.RoomId + ":" + item.transform.position)));
            if (onward != null)
            {
                RoomModule corridor = onward.TargetRoom;
                MovePlayer(player, house, corridor, corridor.transform.position);
                house.CloseAfterPassage(onward, player.gameObject);
                yield return new WaitForSeconds(2f);
                DoorSocket officeDoor = corridor.Sockets.Single(socket => socket.SocketId == "corridor_north_right");
                MovePlayer(player, house, corridor, officeDoor.ClearApproachPoint);
                house.RequestOpen(officeDoor, player.gameObject);
                deadline = Time.time + 12f;
                while (house.ConnectionFor(officeDoor) == null && Time.time < deadline) yield return null;
                Check(house.ConnectionFor(officeDoor)?.TargetRoom.RoomId == "office", "No pudo continuar hacia la oficina después del retorno al cuarto");
            }
        }
        var office = house.ModuleById["office"];
        StoryPhone phone = office.GetComponentInChildren<StoryPhone>(true);
        Check(phone != null && phone.GetComponent<Collider>() != null, "Falta el celular interactuable");
        if (phone != null)
        {
            Check(phone.GetComponentInParent<HideSpot>() == null, "El celular sigue dentro de un escondite");
            Transform sofa = phone.transform.parent;
            Check(sofa.name == "SofaOficina" && sofa.GetComponent<MeshFilter>() != null, "El celular no está sobre el sofá nuevo");
            Vector3 standing = sofa.TransformPoint(new Vector3(0f, 0f, 1f));
            MovePlayer(player, house, office, standing);
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(.08f);
            var filter = new NavMeshQueryFilter { agentTypeID = RoomNavigation.AgentType, areaMask = NavMesh.AllAreas };
            Check(NavMesh.SamplePosition(standing, out NavMeshHit source, .5f, filter), "El sofá bloquea el acceso al celular");
            foreach (HideSpot hide in office.GetComponentsInChildren<HideSpot>(true))
            {
                var navPath = new NavMeshPath();
                bool accessible = NavMesh.SamplePosition(hide.InspectionPosition, out NavMeshHit target, 1f, filter)
                    && NavMesh.CalculatePath(source.position, target.position, filter, navPath)
                    && navPath.status == NavMeshPathStatus.PathComplete;
                Check(accessible, "El sofá bloquea el acceso al escondite: " + hide.name);
            }
            var testCamera = new GameObject("CamaraPruebaCelular");
            testCamera.transform.position = standing + Vector3.up * 1.5f;
            testCamera.transform.LookAt(phone.transform.position);
            Transform oldCamera = player.cameraTransform;
            player.cameraTransform = testCamera.transform;
            yield return null;
            Check(player.CurrentPrompt == phone.InteractionPrompt, "Apuntar al celular seleccionó otra interacción: " + player.CurrentPrompt);
            StoryPhoneInteractionArea phoneArea = sofa.GetComponent<StoryPhoneInteractionArea>();
            Check(phoneArea != null && phoneArea.CanInteract(player.gameObject), "El sillón no ofrece la interacción del celular");
            testCamera.transform.rotation = Quaternion.LookRotation(sofa.right);
            yield return null;
            Check(player.CurrentPrompt == phone.InteractionPrompt, "Acercarse al sillón sin apuntar no ofreció el celular");
            MovePlayer(player, house, office, sofa.TransformPoint(new Vector3(0f, 0f, 3.8f)));
            testCamera.transform.position = player.transform.position + Vector3.up * 1.5f;
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(.08f);
            Check(phoneArea != null && !phoneArea.CanInteract(player.gameObject) && player.CurrentPrompt != phone.InteractionPrompt,
                "Salir del trigger no retiró la interacción del celular");
            MovePlayer(player, house, office, standing);
            testCamera.transform.position = standing + Vector3.up * 1.5f;
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(.08f);
            foreach (Vector3 point in new[] { new Vector3(-.55f,.35f,.375f), new Vector3(.79f,.72f,.2f), new Vector3(0f,.85f,-.25f) })
            {
                testCamera.transform.LookAt(sofa.TransformPoint(point));
                yield return null;
                Check(player.CurrentPrompt == phone.InteractionPrompt, "Apuntar a una parte del sillón no seleccionó el celular");
            }
            player.cameraTransform = oldCamera;
            Destroy(testCamera);
            if (phoneArea != null) phoneArea.Interact(player.gameObject);
            Check(story.Objective == StoryObjective.Survive && session.HasSurvivalTimer, "Revisar el celular no inició la supervivencia");
            Check(Mathf.Abs(session.RemainingTime - 180f) < .2f, "El contador no inició en tres minutos");
            Check(phoneArea != null && !phoneArea.CanInteract(player.gameObject), "El sillón sigue ofreciendo el celular después de completar el objetivo");
            phone.Interact(player.gameObject);
            yield return new WaitForSeconds(.2f);
            Check(session.RemainingTime < 180f, "El contador no avanza");
            Check(AlienActivityDirector.Instance.Current.Phase == AlienActivityPhase.Intense,
                "Encontrar el celular no activó la fase intensa");
            AlienController[] aliens = FindObjectsByType<AlienController>(FindObjectsSortMode.None);
            foreach (AlienController alien in aliens)
            {
                alien.enabled = true; alien.SetPlayerDetectionEnabled(false); alien.SetHearingEnabled(false);
                alien.StopAllCoroutines(); alien.Motor.Stop();
            }
            Time.timeScale = 3f;
            yield return AlienCoordinationChecks.RunDoorRecoveryChecks(house, aliens, player.gameObject, Check);
            yield return CheckWaitingAlternative(house, player, aliens[0]);
            Time.timeScale = 1f;
            session.SetSurvivalDuration(.1f);
            yield return null;
            Check(session.State == GameSessionState.Victory, "La supervivencia no finalizó en victoria");
        }
        Finish();
    }
    private IEnumerator CheckWaitingAlternative(HouseFlowController house, PlayerInteractor player, AlienController alien)
    {
        foreach (RoomConnection old in house.Connections.ToArray())
        { old.PassageUsers.Clear(); old.Pursuers.Clear(); old.SourceSocket.Door.SetClosedInstant(); }
        yield return new WaitForSeconds(.5f);
        RoomModule child = house.ModuleById["child_room"];
        MovePlayer(player, house, child, child.transform.position);
        yield return null;
        DoorSocket door = child.Sockets.First();
        RoomModule normal = house.NormalDestination(door).Room;
        normal.gameObject.SetActive(false);
        normal.transform.position += Vector3.right * 160f;
        house.RegisterOccupant(alien.gameObject, normal, true);
        alien.Motor.Agent.Warp(normal.transform.position + Vector3.up * .04f);
        var config = new SerializedObject(house);
        config.FindProperty("randomizeEveryEligibleDoor").boolValue = false;
        config.FindProperty("anomalyChance").floatValue = 0f;
        config.ApplyModifiedPropertiesWithoutUndo();
        DoorSocket busy = normal.Sockets.Single(socket => socket.SocketId == "corridor_north_right");
        house.RequestOpen(busy, alien.gameObject);
        yield return new WaitForSeconds(.6f);
        Check(house.ConnectionFor(busy) != null, "No se pudo preparar la habitación normal ocupada");
        // Mantiene un paso realmente en uso: de otro modo la cola lo cierra y recupera el destino normal.
        RoomConnection held = house.ConnectionFor(busy);
        if (held != null) house.ReservePassage(held, alien.gameObject);
        float started = Time.time;
        house.RequestOpen(door, player.gameObject);
        float deadline = Time.time + 3f;
        while (house.ConnectionFor(door) == null && Time.time < deadline) yield return null;
        RoomConnection alternative = house.ConnectionFor(door);
        Check(alternative != null && alternative.TargetRoom != normal && alternative.IsAnomalous
            && door.IsAnomalyCompatibleWith(alternative.TargetSocket),
            "La solicitud esperando no encontró otro destino seguro compatible");
        Check(alternative != null && Time.time - started < 3f && house.PendingRequestCount == 0,
            "La solicitud de puerta siguió esperando pese a existir una alternativa");
    }
    private IEnumerator CheckThresholdClose(HouseFlowController house, PlayerInteractor player, RoomConnection connection)
    {
        DoorSocket source = connection.SourceSocket;
        MovePlayer(player, house, source.Room, source.Position - source.Outward * .02f);
        yield return new WaitForFixedUpdate();
        foreach (RoomModule room in new[] { connection.SourceRoom, connection.TargetRoom })
            foreach (DoorThresholdSensor sensor in room.GetComponentsInChildren<DoorThresholdSensor>())
                sensor.ReconcileOccupants();
        Check(source.IsThresholdOccupied || connection.TargetSocket.IsThresholdOccupied,
            "La prueba no registró al Player dentro del umbral");
        Check(house.RequestClose(source, player.gameObject), "El cierre pendiente no se aceptó");
        yield return new WaitForSeconds(.6f);
        Check(source.Door.State == DoorState.Closed && house.ConnectionFor(source) == connection
            && connection.SourceRoom.gameObject.activeSelf && connection.TargetRoom.gameObject.activeSelf,
            "Cerrar desde el umbral se retrasó o quitó el suelo bajo el Player");

        // Simula entrar durante una animación que ya comenzó: aun cerrada, no debe retirar el suelo.
        source.Door.BeginOpen(player.gameObject);
        yield return new WaitForSeconds(.6f);
        source.Door.BeginClose(player.gameObject);
        yield return new WaitForSeconds(.6f);
        Check(house.ConnectionFor(source) == connection
            && connection.SourceRoom.gameObject.activeSelf && connection.TargetRoom.gameObject.activeSelf,
            "El final de la animación liberó una habitación con el umbral ocupado");
        MovePlayer(player, house, source.Room, source.ClearApproachPoint);
        // Reabre el mismo paso antes de continuar la prueba narrativa.
        Check(house.RequestOpen(source, player.gameObject), "No pudo reabrir el paso conservado");
        yield return new WaitForSeconds(.6f);
    }
    private void CheckRandomRoomSelection(HouseFlowController house)
    {
        // Comprueba el sorteo sin mover habitaciones ni abrir puertas.
        RoomModule child = house.ModuleById["child_room"];
        RoomModule kitchen = house.ModuleById["kitchen"];
        var candidates = new List<RoomConnectionCandidate>
        {
            new RoomConnectionCandidate(child, child.Sockets.First()),
            new RoomConnectionCandidate(kitchen, kitchen.Sockets.First())
        };
        var recent = new[] { child.RoomId };
        var first = new RandomRoomSelectionStrategy(12345);
        var replay = new RandomRoomSelectionStrategy(12345);
        var seen = new HashSet<RoomModule>();
        for (int i = 0; i < 128; i++)
        {
            RoomConnectionCandidate selection = first.Select(candidates, recent);
            Check(selection.Room == replay.Select(candidates, recent).Room,
                "La misma semilla no reproduce el sorteo con los mismos candidatos");
            seen.Add(selection.Room);
        }
        Check(seen.Contains(child) && seen.Contains(kitchen),
            "El sorteo excluye una habitación segura por haber aparecido recientemente");
        Check(first.Select(new List<RoomConnectionCandidate>(), recent).Room == null,
            "El sorteo inventa un destino cuando no hay candidatos");
    }
    private void CheckAnomalyCompatibility()
    {
        // Usa puntos temporales: no cambia los datos ni la distribución de las habitaciones guardadas.
        GameObject sourceObject = new GameObject("PruebaConexionOrigen");
        GameObject targetObject = new GameObject("PruebaConexionDestino");
        DoorSocket source = sourceObject.AddComponent<DoorSocket>();
        DoorSocket target = targetObject.AddComponent<DoorSocket>();
        try
        {
            foreach (HouseFloor from in new[] { HouseFloor.Ground, HouseFloor.Upper })
            foreach (HouseFloor to in new[] { HouseFloor.Ground, HouseFloor.Upper })
            {
                source.Configure("test_source", from, DoorSocketKind.Interior, null, null, null, null);
                target.Configure("test_target", to, DoorSocketKind.Interior, null, null, null, null);
                Check(source.IsAnomalyCompatibleWith(target), "La anomalía rechaza la conexión " + from + " -> " + to);
                target.Configure("test_target", to, DoorSocketKind.HallAccess, null, null, null, null);
                Check(!source.IsAnomalyCompatibleWith(target), "La anomalía acepta un acceso protegido de escalera");
            }
            target.Configure("test_target", HouseFloor.Unspecified, DoorSocketKind.Interior, null, null, null, null);
            Check(!source.IsAnomalyCompatibleWith(target), "La anomalía acepta un destino sin planta definida");
            Check(!source.IsAnomalyCompatibleWith(null), "La anomalía acepta un destino inexistente");
        }
        finally
        {
            Destroy(sourceObject);
            Destroy(targetObject);
        }
    }
    private static void MovePlayer(PlayerInteractor player, HouseFlowController house, RoomModule room, Vector3 position)
    {
        room.gameObject.SetActive(true);
        var character = player.GetComponent<CharacterController>();
        if (character != null) character.enabled = false;
        player.transform.position = position + Vector3.up * .04f;
        if (character != null) character.enabled = true;
        house.RegisterOccupant(player.gameObject, room, false);
        Physics.SyncTransforms();
    }
    private void Finish()
    {
        Application.logMessageReceived -= Capture;
        File.WriteAllText("Temp/StoryTestResult.txt", "FAILURES=" + failures.Count + "\n" + string.Join("\n", failures));
        Time.timeScale = 1f;
        EditorApplication.ExitPlaymode();
    }
    private void Capture(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error) failures.Add(message);
    }
}
#endif
