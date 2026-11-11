#if UNITY_EDITOR
namespace TcgEngine.Dev
{
    // Editor-only dev flags. Kept off Game/Player to avoid network/clone drift.
    public static class DevState
    {
        public static bool AIPaused;
        public static DevScenario PendingScenario;
        public static bool ScenarioApplied;

        // Outcome forcing (playtest): see GameLogicService.TriggerBoardSlotAbilities
        public static bool ForceAbilityProcs; // stat abilities ignore slot requirements
        public static TcgEngine.Gameplay.FailPlayEventType ForceFailEvent; // only this fail event fires, slot reqs ignored
    }
}
#endif
