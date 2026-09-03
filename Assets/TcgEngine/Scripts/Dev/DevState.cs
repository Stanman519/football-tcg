#if UNITY_EDITOR
namespace TcgEngine.Dev
{
    // Editor-only dev flags. Kept off Game/Player to avoid network/clone drift.
    public static class DevState
    {
        public static bool AIPaused;
        public static DevScenario PendingScenario;
        public static bool ScenarioApplied;
    }
}
#endif
