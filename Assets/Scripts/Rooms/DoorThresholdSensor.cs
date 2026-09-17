using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DoorThresholdSensor : MonoBehaviour
{
    private readonly HashSet<Collider> occupants = new HashSet<Collider>();
    private BoxCollider volume;
    private readonly Collider[] hits = new Collider[64];
    private bool needsReconciliation = true;

    private void Awake()
    {
        CacheVolume();
    }

    private void OnEnable()
    {
        CacheVolume();
        needsReconciliation = true;
    }

    /// <summary>Unity llama a OnTriggerEnter cuando un personaje entra al volumen del umbral. Registra su presencia; abrir sigue siendo una decisión del jugador o del alien.</summary>
    private void OnTriggerEnter(Collider other)
    {
        Track(other);
    }

    /// <summary>Quita el collider del registro cuando termina de salir del umbral.</summary>
    private void OnTriggerExit(Collider other)
    {
        if (other != null)
        {
            occupants.Remove(other);
        }
    }

    /// <summary>Vuelve a comprobar el umbral cuando una habitación se activa o se mueve de golpe. Durante el movimiento normal se actualiza con los eventos del trigger.</summary>
    public void ReconcileOccupants()
    {
        occupants.Clear();
        needsReconciliation = false;
        if (!isActiveAndEnabled) return;
        CacheVolume();
        if (volume == null || !volume.enabled) return;
        Vector3 scale = transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        int count = Physics.OverlapBoxNonAlloc(transform.TransformPoint(volume.center),
            Vector3.Scale(volume.size * .5f, scale), hits, transform.rotation,
            ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Track(hits[i]);
            hits[i] = null;
        }
    }

    public bool IsOccupied
    {
        get
        {
            EnsureCurrent();
            return occupants.Count > 0;
        }
    }

    public bool IsOccupiedByOtherThan(GameObject ignoredObject)
    {
        EnsureCurrent();
        Transform ignoredRoot = ignoredObject != null ? ignoredObject.transform : null;
        foreach (Collider occupant in occupants)
        {
            if (ResolveRelevantRoot(occupant) != ignoredRoot)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureCurrent()
    {
        if (needsReconciliation)
        {
            ReconcileOccupants();
            return;
        }

        occupants.RemoveWhere(collider => collider == null || !collider.enabled
            || !collider.gameObject.activeInHierarchy
            // Al mover habitaciones, Unity puede no emitir una salida. Descarta registros que ya no tocan el umbral.
            || (volume != null && !volume.bounds.Intersects(collider.bounds)));
    }

    private void Track(Collider other)
    {
        if (other == null || other.isTrigger || ResolveRelevantRoot(other) == null)
        {
            return;
        }

        occupants.Add(other);
    }

    private void CacheVolume()
    {
        if (volume == null)
        {
            volume = GetComponent<BoxCollider>();
        }
    }

    private static Transform ResolveRelevantRoot(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        RoomOccupant actor = other.GetComponentInParent<RoomOccupant>();
        if (actor != null) return actor.transform;
        Transform root = other.transform.root;
        if (root.CompareTag("Player") || other.attachedRigidbody != null)
        {
            return root;
        }

        return null;
    }
    private void OnDisable()
    {
        occupants.Clear();
        needsReconciliation = true;
    }
}
