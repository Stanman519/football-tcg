using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;

/// <summary>
/// Subscribes to GameClient ability events and drives animated slot movement
/// for any AbilityData that has a RouteData assigned.
///
/// Attach this MonoBehaviour to the same GameObject as FieldSlotManager.
/// </summary>
public class FieldAnimationController : MonoBehaviour
{
    private void Awake()
    {
        GameClient client = GameClient.Get();
        if (client != null)
            client.onAbilityEnd += OnAbilityEnd;
    }

    private void OnDestroy()
    {
        GameClient client = GameClient.Get();
        if (client != null)
            client.onAbilityEnd -= OnAbilityEnd;
    }

    // -------------------------------------------------------

    private void OnAbilityEnd(AbilityData ability, Card caster)
    {
        if (ability == null || ability.animationData == null) return;

        List<BoardSlot> targets = ResolveTargetSlots(ability, caster);
        foreach (BoardSlot slot in targets)
            StartCoroutine(PlayRoute(slot, ability.animationData));
    }

    // -------------------------------------------------------
    // Slot resolution

    private List<BoardSlot> ResolveTargetSlots(AbilityData ability, Card caster)
    {
        var result = new List<BoardSlot>();
        if (FieldSlotManager.Instance == null) return result;

        switch (ability.animTarget)
        {
            case AnimTargetType.Self:
            {
                // Find the BoardSlot that currently holds the caster card
                BoardSlot slot = FindSlotForCard(caster);
                if (slot != null) result.Add(slot);
                break;
            }

            case AnimTargetType.SlotIndex:
            {
                // Use the posGroup inferred from the caster's position group
                if (caster?.CardData != null)
                {
                    PlayerPositionGrp posGroup = caster.CardData.playerPosition;
                    var slots = FieldSlotManager.Instance.GetSlotsForPosition(posGroup, caster.player_id);
                    if (ability.animTargetSlotIndex < slots.Count)
                        result.Add(slots[ability.animTargetSlotIndex]);
                }
                break;
            }

            case AnimTargetType.AllInGroup:
            {
                if (caster?.CardData != null)
                {
                    PlayerPositionGrp posGroup = caster.CardData.playerPosition;
                    result.AddRange(FieldSlotManager.Instance.GetSlotsForPosition(posGroup, caster.player_id));
                }
                break;
            }

            case AnimTargetType.TopReceiver:
                // Future: query ReceiverRankingSystem — no-op for now
                break;
        }

        return result;
    }

    private BoardSlot FindSlotForCard(Card card)
    {
        if (card == null || FieldSlotManager.Instance == null) return null;

        // The caster's position group tells us which slot list to search
        if (card.CardData == null) return null;
        var slots = FieldSlotManager.Instance.GetSlotsForPosition(card.CardData.playerPosition, card.player_id);
        // Return the first occupied slot (simple heuristic; extend if needed)
        return slots.Count > 0 ? slots[0] : null;
    }

    // -------------------------------------------------------
    // Route playback coroutine

    private IEnumerator PlayRoute(BoardSlot slot, RouteData route)
    {
        if (slot == null || route == null) yield break;

        float routeStart = Time.time;

        foreach (RouteKeyframe frame in route.keyframes)
        {
            // Wait until this frame's time offset
            float waitUntil = routeStart + frame.timeOffset;
            while (Time.time < waitUntil)
                yield return null;

            // Find the waypoint that matches this slot's posGroup + slotIndex
            foreach (SlotWaypoint wp in frame.waypoints)
            {
                if (wp.posGroup == slot.player_position_type && wp.slotIndex == slot.slotIndex)
                {
                    Vector3 targetLocal = FieldSlotManager.Instance != null
                        ? FieldSlotManager.Instance.ToLocalPosPublic(new Vector2(wp.xFraction, wp.yardsFromLOS))
                        : Vector3.zero;
                    slot.SetTargetPosition(targetLocal);
                    break;
                }
            }
        }
    }
}
