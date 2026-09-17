#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AlienController))]
public sealed class AlienControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        AlienController alien = (AlienController)target;
        EditorGUILayout.LabelField("Estado", alien.StateName);
        EditorGUILayout.LabelField("Habitación", alien.Occupant?.Room?.DisplayName ?? "Sin iniciar");
        EditorGUILayout.HelpBox("Durante Play se muestran el estado, la visión y los intereses detectados. La visión respeta obstáculos y los sonidos dependen de su emisor.", MessageType.Info);
    }
    private void OnSceneGUI()
    {
        AlienController alien = (AlienController)target;
        if (alien.Head == null || alien.Perception == null) return;
        Transform head = alien.Head.Head;
        Handles.color = new Color(.3f, .9f, .9f, .12f);
        Vector3 start = Quaternion.AngleAxis(-alien.Perception.FieldOfView / 2, Vector3.up) * head.forward;
        Handles.DrawSolidArc(head.position, Vector3.up, start, alien.Perception.FieldOfView, alien.Perception.Range);
        Handles.color = Color.cyan;
        Handles.Label(head.position + Vector3.up * .4f, alien.name + ": " + alien.StateName);
        foreach (AlienInterest interest in alien.Visible)
            if (interest != null) Handles.DrawDottedLine(head.position, interest.LookPoint, 5);
        if (alien.Target != null) Handles.DrawWireDisc(alien.Target.ApproachPoint, Vector3.up, .3f);
    }
}
#endif
