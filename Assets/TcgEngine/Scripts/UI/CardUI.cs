using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TcgEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine.UI
{
    /// <summary>
    /// Displays all stats of a card.
    /// Used by BoardCard, HandCard, CollectionCard, etc.
    ///
    /// Field mapping (set in Inspector):
    ///   card_title        → player name only (position prefix stripped)
    ///   attack  / stamina_icon → Stamina
    ///   hp      / grit_icon     → Grit
    ///   cost    / position_icon   → Position abbreviation (QB, WR, OL, etc.)
    ///   run_bonus_text        → Run bonus  (offense) / Run coverage  (defense)
    ///   short_pass_bonus_text → Short pass bonus    / Short coverage
    ///   long_pass_bonus_text  → Long pass bonus     / Deep coverage
    /// </summary>

    public class CardUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("Card Visuals")]
        public Image card_image;
        public Image frame_image;
        public Image team_icon;
        public Image rarity_icon;

        [Header("Stamina / Grit / Position")]
        [FormerlySerializedAs("stamina_icon")] public Image stamina_icon;
        [FormerlySerializedAs("grit_icon")]     public Image grit_icon;
        [FormerlySerializedAs("position_icon")]   public Image position_icon;
        [FormerlySerializedAs("attack")] public Text stamina;
        [FormerlySerializedAs("hp")]     public Text grit;
        [FormerlySerializedAs("cost")]   public Text position;

        [Header("Play Bonuses")]
        public Text run_bonus_text;
        public Text short_pass_bonus_text;
        public Text long_pass_bonus_text;

        [Header("Card Text")]
        public Text card_title;
        public Text card_text;

        public TraitUI[] stats;

        public UnityAction<CardUI> onClick;
        public UnityAction<CardUI> onClickRight;

        private CardData card;
        private VariantData variant;

        // -------------------------------------------------------

        public void SetCard(Card card)
        {
            if (card == null)
                return;

            SetCard(card.CardData, card.VariantData);

            foreach (TraitUI stat in stats)
                stat.SetCard(card);
        }

        public void SetCard(CardData card, VariantData variant)
        {
            if (card == null)
                return;

            this.card = card;
            this.variant = variant;

            // Art
            if (card_image != null)
                card_image.sprite = card.GetFullArt(variant);
            if (frame_image != null)
                frame_image.sprite = variant.frame;

            // Title — strip position prefix for player cards ("QB Trent Hawthorne" → "Trent Hawthorne")
            if (card_title != null)
                card_title.text = GetDisplayName(card).ToUpper();

            // Ability / flavour text
            if (card_text != null)
                card_text.text = card.GetText();

            if (card.IsPlayer())
            {
                bool isOff = card.type == CardType.OffensivePlayer;

                // Stamina
                if (stamina_icon != null) stamina_icon.enabled = true;
                if (stamina != null)     { stamina.enabled = true; stamina.text = card.stamina.ToString(); }

                // Grit
                if (grit_icon != null) grit_icon.enabled = true;
                if (grit != null)      { grit.enabled = true; grit.text = card.grit.ToString(); }

                // Position
                if (position_icon != null) position_icon.enabled = true;
                if (position != null)  { position.enabled = true; position.text = GetPositionAbbrev(card.playerPosition); }

                // Play bonuses (offensive values) or coverage (defensive values)
                if (run_bonus_text != null)
                    run_bonus_text.text = (isOff ? card.run_bonus : card.run_coverage_bonus).ToString();
                if (short_pass_bonus_text != null)
                    short_pass_bonus_text.text = (isOff ? card.short_pass_bonus : card.short_pass_coverage_bonus).ToString();
                if (long_pass_bonus_text != null)
                    long_pass_bonus_text.text = (isOff ? card.deep_pass_bonus : card.deep_pass_coverage_bonus).ToString();
            }
            else
            {
                // Non-player cards: hide the stat cluster
                if (stamina_icon != null) stamina_icon.enabled = false;
                if (stamina != null)     stamina.enabled = false;
                if (grit_icon != null)   grit_icon.enabled = false;
                if (grit != null)        grit.enabled = false;
                if (position_icon != null) position_icon.enabled = false;
                if (position != null)    position.enabled = false;

                if (run_bonus_text != null)        run_bonus_text.gameObject.SetActive(false);
                if (short_pass_bonus_text != null) short_pass_bonus_text.gameObject.SetActive(false);
                if (long_pass_bonus_text != null)  long_pass_bonus_text.gameObject.SetActive(false);
            }

            foreach (TraitUI stat in stats)
                stat.SetCard(card);

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
        }

        // -------------------------------------------------------

        /// <summary>Strips the position prefix from a player card title.</summary>
        private static string GetDisplayName(CardData card)
        {
            string title = card.GetTitle();
            if (!card.IsPlayer()) return title;

            int space = title.IndexOf(' ');
            if (space > 0 && space < title.Length - 1)
                return title.Substring(space + 1);
            return title;
        }

        private static string GetPositionAbbrev(PlayerPositionGrp pos)
        {
            switch (pos)
            {
                case PlayerPositionGrp.QB:    return "QB";
                case PlayerPositionGrp.RB_TE: return "RB/TE";
                case PlayerPositionGrp.WR:    return "WR";
                case PlayerPositionGrp.OL:    return "OL";
                case PlayerPositionGrp.DL:    return "DL";
                case PlayerPositionGrp.LB:    return "LB";
                case PlayerPositionGrp.DB:    return "DB";
                case PlayerPositionGrp.K:     return "K";
                case PlayerPositionGrp.P:     return "P";
                default:                      return "";
            }
        }

        // -------------------------------------------------------

        public void SetGrit(int value)
        {
            if (grit != null)
                grit.text = value.ToString();
        }

        public void SetMaterial(Material mat)
        {
            if (card_image != null)   card_image.material   = mat;
            if (frame_image != null)  frame_image.material  = mat;
            if (team_icon != null)    team_icon.material     = mat;
            if (rarity_icon != null)  rarity_icon.material   = mat;
            if (stamina_icon != null)  stamina_icon.material   = mat;
            if (grit_icon != null)      grit_icon.material       = mat;
            if (position_icon != null)    position_icon.material     = mat;
        }

        public void SetOpacity(float opacity)
        {
            Color A(Color c) => new Color(c.r, c.g, c.b, opacity);
            if (card_image != null)   card_image.color   = A(card_image.color);
            if (frame_image != null)  frame_image.color  = A(frame_image.color);
            if (team_icon != null)    team_icon.color    = A(team_icon.color);
            if (rarity_icon != null)  rarity_icon.color  = A(rarity_icon.color);
            if (stamina_icon != null)  stamina_icon.color  = A(stamina_icon.color);
            if (grit_icon != null)      grit_icon.color      = A(grit_icon.color);
            if (position_icon != null)    position_icon.color    = A(position_icon.color);
            if (stamina != null)   stamina.color   = A(stamina.color);
            if (grit != null)      grit.color      = A(grit.color);
            if (position != null)  position.color  = A(position.color);
            if (card_title != null)   card_title.color   = A(card_title.color);
            if (card_text != null)    card_text.color    = A(card_text.color);
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                onClick?.Invoke(this);
            if (eventData.button == PointerEventData.InputButton.Right)
                onClickRight?.Invoke(this);
        }

        public CardData GetCard()    => card;
        public VariantData GetVariant() => variant;
    }
}
