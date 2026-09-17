using UnityEditor;
using UnityEngine;

/// <summary>Las líneas permiten ver la bisagra y su giro sin cambiar la puerta ni agregar trabajo durante el juego.</summary>
public static class HingeGizmoEditor
{
    [DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy)]
    private static void DrawDoor(DoorInteractable door, GizmoType type)
    {
        var data = new SerializedObject(door);
        float angle = data.FindProperty("openAngle").floatValue;
        // El Transform ya es el pivote real. El dibujo muestra el eje que utiliza la animación.
        Draw(door.transform, Vector3.up, angle, "Bisagra de puerta");
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy)]
    private static void DrawFurniture(HingedHideFurniture furniture, GizmoType type)
    {
        var data = new SerializedObject(furniture);
        SerializedProperty pivots = data.FindProperty("pivots");
        SerializedProperty closed = data.FindProperty("closedEulerAngles");
        SerializedProperty open = data.FindProperty("openEulerAngles");
        for (int i = 0; i < pivots.arraySize && i < closed.arraySize && i < open.arraySize; i++)
        {
            Transform pivot = pivots.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (pivot == null) continue;
            Quaternion start = Quaternion.Euler(closed.GetArrayElementAtIndex(i).vector3Value);
            Quaternion end = Quaternion.Euler(open.GetArrayElementAtIndex(i).vector3Value);
            (Quaternion.Inverse(start) * end).ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (Mathf.Abs(angle) < .01f) continue;
            Draw(pivot, axis, angle, "Bisagra de mueble");
        }
    }

    private static void Draw(Transform pivot, Vector3 axis, float angle, string label)
    {
        Color previous = Handles.color;
        Vector3 center = pivot.position;
        Vector3 normal = pivot.TransformDirection(axis).normalized;
        Vector3 from = Vector3.Cross(normal, pivot.forward);
        if (from.sqrMagnitude < .01f) from = Vector3.Cross(normal, pivot.right);
        Handles.color = Color.cyan;
        Handles.DrawLine(center - normal * .25f, center + normal * .45f);
        Handles.SphereHandleCap(0, center, Quaternion.identity, .08f, EventType.Repaint);
        Handles.DrawWireArc(center, normal, from.normalized, angle, .5f);
        Handles.Label(center + normal * .48f, label + " (" + angle.ToString("0") + "°)");
        Handles.color = previous;
    }
}
