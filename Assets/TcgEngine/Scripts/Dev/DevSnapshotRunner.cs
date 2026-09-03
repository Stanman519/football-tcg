#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using TcgEngine.Client;

namespace TcgEngine.Dev
{
    // Spawned at scene load when a scenario is pending. Waits for target phase to be
    // stable for ~1.5s, then writes a screenshot and pauses the editor.
    public class DevSnapshotRunner : MonoBehaviour
    {
        private const float StableThreshold = 1.5f;
        private float stableTime;
        private bool snapped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            if (DevState.PendingScenario == null) return;
            var go = new GameObject("DevSnapshotRunner");
            go.AddComponent<DevSnapshotRunner>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            if (snapped) return;
            var scenario = DevState.PendingScenario;
            if (scenario == null) { Destroy(gameObject); return; }

            var client = GameClient.Get();
            if (client == null || !client.IsReady()) { stableTime = 0f; return; }
            var g = client.GetGameData();
            if (g == null || g.phase != scenario.targetPhase) { stableTime = 0f; return; }

            stableTime += Time.deltaTime;
            if (stableTime >= StableThreshold)
            {
                snapped = true;
                Capture(scenario);
            }
        }

        private static void Capture(DevScenario scenario)
        {
            string dir = Path.Combine(Application.dataPath, "..", "_DevSnapshots");
            Directory.CreateDirectory(dir);
            string filename = $"{Sanitize(scenario.scenarioName)}_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            string path = Path.Combine(dir, filename);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[DevSnapshot] {scenario.targetPhase} captured: {path}");
            EditorApplication.delayCall += () => { EditorApplication.isPaused = true; };
        }

        private static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }
    }
}
#endif
