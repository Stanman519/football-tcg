#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TcgEngine.Dev
{
    // Loads scenario from EditorPrefs into DevState on play start; clears on exit.
    [InitializeOnLoad]
    public static class DevPlayModeHandler
    {
        static DevPlayModeHandler()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                string path = EditorPrefs.GetString(DevBootstrap.PrefKey, "");
                if (string.IsNullOrEmpty(path)) return;
                var scenario = AssetDatabase.LoadAssetAtPath<DevScenario>(path);
                DevState.PendingScenario = scenario;
                DevState.ScenarioApplied = false;
                DevState.AIPaused = scenario != null && scenario.pauseAI;
                if (scenario != null)
                    Debug.Log($"[DevBootstrap] Loaded '{scenario.scenarioName}' (target: {scenario.targetPhase})");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorPrefs.DeleteKey(DevBootstrap.PrefKey);
                DevState.PendingScenario = null;
                DevState.ScenarioApplied = false;
                DevState.AIPaused = false;
            }
        }
    }
}
#endif
