#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HideSpot))]
public sealed class HideSpotEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "Los puntos Entrada, Oculto y Salida se pueden mover directamente con Handles en la Scene.",
            MessageType.Info);
    }

    private void OnSceneGUI()
    {
        HideSpot spot = (HideSpot)target;
        DrawPointHandle(spot.EntryPoint, "Entrada", new Color(0.20f, 0.85f, 1f));
        DrawPointHandle(spot.HiddenPoint, "Oculto", new Color(0.30f, 1f, 0.40f));
        DrawPointHandle(spot.ExitPoint, "Salida", new Color(1f, 0.70f, 0.20f));

        if (spot.EntryPoint != null && spot.HiddenPoint != null)
        {
            Handles.color = Color.cyan;
            Handles.DrawDottedLine(spot.EntryPoint.position, spot.HiddenPoint.position, 4f);
            Handles.ArrowHandleCap(0, spot.EntryPoint.position,
                Quaternion.LookRotation(spot.HiddenPoint.position - spot.EntryPoint.position),
                0.45f, EventType.Repaint);
        }
    }

    private static void DrawPointHandle(Transform point, string label, Color color)
    {
        if (point == null)
        {
            return;
        }

        Handles.color = color;
        Handles.Label(point.position + Vector3.up * 0.16f, label);
        EditorGUI.BeginChangeCheck();
        Vector3 position = Handles.PositionHandle(point.position, point.rotation);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(point, "Move hide spot " + label);
            point.position = position;
            EditorUtility.SetDirty(point);
        }
    }
}
#endif
