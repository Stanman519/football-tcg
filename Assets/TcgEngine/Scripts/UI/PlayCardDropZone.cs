using TcgEngine.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TcgEngine.Client;

namespace TcgEngine.UI
{
    public class PlayCardDropZone : MonoBehaviour, IDropHandler
    {
        static PlayCardDropZone _instance;
        public static PlayCardDropZone Get()
        {
            if (_instance == null)
            {
                var go = new GameObject("PlayCardDropZone");
                _instance = go.AddComponent<PlayCardDropZone>();
            }
            return _instance;
        }

        void Awake() { _instance = this; }
        void OnDestroy() { if (_instance == this) _instance = null; }

        public void OnDrop(PointerEventData eventData)
        {
            HandCard handCard = eventData.pointerDrag?.GetComponent<HandCard>();
            if (handCard == null) return;

            Card card = handCard.GetCard();
            if (card == null || card.Data == null) { handCard.EndDrag(); return; }

            Game g = GameClient.Get()?.GetGameData();
            if (g == null) { handCard.EndDrag(); return; }

            // --- Player cards → board slot ---
            if (card.Data.IsPlayer() && g.phase == GamePhase.ChoosePlayers)
            {
                PlayerPositionGrp position = card.Data.playerPosition;
                int playerId = GameClient.Get().GetPlayerID();
                List<BoardSlot> slots = FieldSlotManager.Instance.GetSlotsForPosition(position, playerId);

                foreach (BoardSlot slot in slots)
                {
                    if (slot.IsEmpty())
                    {
                        GameClient.Get().PlayCard(card, slot.assignedSlot);
                        break;
                    }
                }
                handCard.EndDrag();
            }
            // --- Enhancers → PlayEnhancerSlot ---
            else if (card.Data.IsPlayEnhancer() && g.phase == GamePhase.ChoosePlay)
            {
                var enhSlot = FindFirstObjectByType<PlayEnhancerSlot>();
                if (enhSlot != null)
                    enhSlot.OnDrop(eventData);
                else
                    handCard.EndDrag();
            }
            // --- Live ball → play card + auto-commit ready ---
            else if (card.Data.IsLiveBall() && g.phase == GamePhase.LiveBall)
            {
                Player player = GameClient.Get().GetPlayer();
                if (player == null || player.IsReadyForPhase(GamePhase.LiveBall))
                {
                    handCard.EndDrag();
                    return;
                }
                CardPlayError err = g.GetCardPlayError(card, CardPositionSlot.None);
                if (err != CardPlayError.None)
                {
                    StatChangePopup.Instance?.ShowCustom(handCard.transform.position, Game.GetCardPlayErrorText(err), Color.red);
                    handCard.EndDrag();
                    return;
                }
                GameClient.Get().PlayCard(card, new CardPositionSlot());
                player.SetReadyForPhase(g.phase, true);
                GameClient.Get().SendAction(GameAction.PlayerReadyPhase);
                handCard.EndDrag();
            }
            // --- Wrong phase/type → return to hand ---
            else
            {
                handCard.EndDrag();
            }
        }
    }
}
