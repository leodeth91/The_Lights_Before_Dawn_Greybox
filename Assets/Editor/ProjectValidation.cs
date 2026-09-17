#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>Pruebas voluntarias desde el menú de Unity. Solo se ejecutan al elegirlas, nunca durante una partida normal.</summary>
public static class ProjectValidation
{
    [MenuItem("The Lights Before Dawn/Pruebas/Historia y puertas")]
    private static void RunStory() => Run("TLBD.StorySmoke");

    [MenuItem("The Lights Before Dawn/Pruebas/Visión, sonidos y captura")]
    private static void RunDetection() => Run("TLBD.DetectionSmoke");

    [MenuItem("The Lights Before Dawn/Pruebas/Patrulla y navegación")]
    private static void RunAliens() => Run("TLBD.AlienSmoke");

    private static void Run(string key)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || SceneManager.GetActiveScene().path != "Assets/Scenes/ModularPrototype.unity") return;
        // Guardar es necesario: las pruebas terminan saliendo de Play y no deben perder la distribución editada.
        if (!EditorSceneManager.SaveOpenScenes()) return;
        SessionState.SetBool(key, true);
        EditorApplication.EnterPlaymode();
    }
}
#endif
