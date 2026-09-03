#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine.Dev
{
    public static class DevMenuItems
    {
        private const string ScenarioDir = "Assets/TcgEngine/Editor/Dev/Scenarios";

        [MenuItem("First&Long/Dev/Snapshot/ChoosePlayers (1st & 10)")]
        public static void SnapChoosePlayers() => Launch("ChoosePlayers_1st10");

        [MenuItem("First&Long/Dev/Snapshot/ChoosePlay (3rd & 7)")]
        public static void SnapChoosePlay3rd7() => Launch("ChoosePlay_3rd7");

        [MenuItem("First&Long/Dev/Snapshot/ChoosePlay (Red Zone)")]
        public static void SnapChoosePlayRZ() => Launch("ChoosePlay_RedZone");

        [MenuItem("First&Long/Dev/Clear Scenario")]
        public static void Clear()
        {
            EditorPrefs.DeleteKey(DevBootstrap.PrefKey);
            DevState.PendingScenario = null;
            DevState.AIPaused = false;
            Debug.Log("[Dev] Scenario cleared.");
        }

        [MenuItem("First&Long/Dev/Create Default Scenarios")]
        public static void CreateDefaults()
        {
            Directory.CreateDirectory(ScenarioDir);

            Save("ChoosePlayers_1st10", s => {
                s.scenarioName = "ChoosePlayers 1st & 10";
                s.targetPhase = GamePhase.ChoosePlayers;
                s.currentDown = 1; s.ballOn = 25; s.playsLeftInHalf = 11;
            });

            Save("ChoosePlay_3rd7", s => {
                s.scenarioName = "ChoosePlay 3rd & 7";
                s.targetPhase = GamePhase.ChoosePlay;
                s.currentDown = 3; s.ballOn = 40; s.playsLeftInHalf = 8;
            });

            Save("ChoosePlay_RedZone", s => {
                s.scenarioName = "ChoosePlay Red Zone";
                s.targetPhase = GamePhase.ChoosePlay;
                s.currentDown = 2; s.ballOn = 85; s.playsLeftInHalf = 6;
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Dev] Default scenarios created in {ScenarioDir}");
        }

        private static void Launch(string name)
        {
            string path = $"{ScenarioDir}/{name}.asset";
            var s = AssetDatabase.LoadAssetAtPath<DevScenario>(path);
            if (s == null)
            {
                Debug.LogError($"Scenario '{name}' not found at {path}. Run First&Long > Dev > Create Default Scenarios first.");
                return;
            }
            EditorPrefs.SetString(DevBootstrap.PrefKey, path);
            EditorApplication.EnterPlaymode();
        }

        private static void Save(string filename, System.Action<DevScenario> populate)
        {
            string path = $"{ScenarioDir}/{filename}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DevScenario>(path);
            if (existing != null) return;
            var asset = ScriptableObject.CreateInstance<DevScenario>();
            populate(asset);
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
#endif
