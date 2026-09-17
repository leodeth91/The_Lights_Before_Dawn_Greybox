using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Construye las zonas por donde se puede caminar una vez por habitación y las vuelve a ubicar cuando esta se mueve.</summary>
[DisallowMultipleComponent]
public sealed class RoomNavigation : MonoBehaviour
{
    private NavMeshData data;
    private NavMeshDataInstance instance;
    private bool prepared;
    private static NavMeshBuildSettings settings;
    public static int AgentType => settings.agentTypeID;
    public static void CreateAgentSettings()
    {
        settings = NavMesh.CreateSettings();
        // GetSettingsByID devuelve una copia. Por eso hay que pasar al constructor esta versión con los ajustes.
        settings.agentRadius = .22f; settings.agentHeight = 1.8f;
        settings.agentClimb = .24f; settings.agentSlope = 48;
        settings.overrideVoxelSize = true; settings.voxelSize = .055f;
        settings.minRegionArea = .1f;
    }
    public void Prepare()
    {
        prepared = true;
        if (gameObject.activeInHierarchy) Register();
    }
    private void Register()
    {
        if (!prepared) return;
        if (data == null)
        {
            Physics.SyncTransforms();
            var markups = new List<NavMeshBuildMarkup>();
            foreach (DoorInteractable door in GetComponentsInChildren<DoorInteractable>(true))
                markups.Add(new NavMeshBuildMarkup { root = door.transform, ignoreFromBuild = true });
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(transform, ~(1 << 2), NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            RoomModule room = GetComponent<RoomModule>();
            Vector3 size = room.LocalVolumeSize;
            data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(Vector3.up * size.y * .5f, size + Vector3.one * 2),
                transform.position, transform.rotation);
            if (data == null) { Debug.LogError("Cannot build navigation for " + room.RoomId); return; }
            data.name = "Navigation_" + room.RoomId;
        }
        if (instance.valid) instance.Remove();
        instance = NavMesh.AddNavMeshData(data, transform.position, transform.rotation);
    }
    private void OnEnable() => Register();
    public void RefreshPlacement() => Register();
    private void OnDisable() { if (instance.valid) instance.Remove(); }
    private void OnDestroy() { if (data != null) Destroy(data); }
}
