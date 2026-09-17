using System.Linq;
using UnityEngine;

/// <summary>Ordena las habitaciones del pool para editarlas. Durante Play, HouseFlowController controla sus posiciones.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class RoomModulePoolLayout : MonoBehaviour
{
    [SerializeField, Min(20f)] private float editorRadius = 50f;
    [SerializeField] private Transform previewPlayer;
    [SerializeField] private string initialRoomId = "child_room";
    [Tooltip("Posicion local del jugador dentro de la habitacion inicial. " +
        "Se edita aqui porque este componente conserva al jugador junto al cuarto en el editor.")]
    [SerializeField] private Vector3 previewPlayerOffset = new Vector3(0f, 0.03f, -0.75f);

    private bool arranging;

    public float EditorRadius => editorRadius;

    public void Configure(Transform player, float radius)
    {
        previewPlayer = player;
        editorRadius = Mathf.Max(20f, radius);
        ArrangeForEditing();
    }

    public void ArrangeForEditing()
    {
        if (Application.isPlaying || arranging)
        {
            return;
        }

        arranging = true;
        RoomModule[] modules = GetComponentsInChildren<RoomModule>(true)
            .OrderBy(module => module.transform.GetSiblingIndex())
            .ToArray();

        for (int index = 0; index < modules.Length; index++)
        {
            float angle = -Mathf.PI * 0.5f + Mathf.PI * 2f * index / modules.Length;
            Vector3 expected = new Vector3(Mathf.Cos(angle) * editorRadius, 0f,
                Mathf.Sin(angle) * editorRadius);
            Transform roomTransform = modules[index].transform;
            if ((roomTransform.localPosition - expected).sqrMagnitude > 0.0001f)
            {
                roomTransform.localPosition = expected;
            }
            if (Quaternion.Angle(roomTransform.localRotation, Quaternion.identity) > 0.01f)
            {
                roomTransform.localRotation = Quaternion.identity;
            }
        }

        RoomModule initialRoom = modules.FirstOrDefault(module => module.RoomId == initialRoomId);
        if (previewPlayer != null && initialRoom != null)
        {
            Vector3 expectedPlayerPosition = initialRoom.transform.position
                + previewPlayerOffset;
            if ((previewPlayer.position - expectedPlayerPosition).sqrMagnitude > 0.0001f)
            {
                previewPlayer.position = expectedPlayerPosition;
            }
        }

        arranging = false;
    }

    private void OnEnable()
    {
        ArrangeForEditing();
    }

    private void OnValidate()
    {
        ArrangeForEditing();
    }

    private void Update()
    {
        ArrangeForEditing();
    }

    private void OnDrawGizmosSelected()
    {
        const int segments = 48;
        Gizmos.color = new Color(0.15f, 0.75f, 1f, 0.45f);
        Vector3 previous = transform.TransformPoint(new Vector3(0f, 0f, -editorRadius));
        for (int index = 1; index <= segments; index++)
        {
            float angle = -Mathf.PI * 0.5f + Mathf.PI * 2f * index / segments;
            Vector3 next = transform.TransformPoint(new Vector3(Mathf.Cos(angle) * editorRadius,
                0f, Mathf.Sin(angle) * editorRadius));
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
