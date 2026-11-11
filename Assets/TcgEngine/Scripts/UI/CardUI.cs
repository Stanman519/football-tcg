using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TcgEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;
using TcgEngine.Client;
using TcgEngine.Gameplay;

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
        [FormerlySerializedAs("attack")] public TMP_Text stamina;
        [FormerlySerializedAs("hp")]     public TMP_Text grit;
        [FormerlySerializedAs("cost")]   public TMP_Text position;

        [Header("Play Bonuses")]
        public TMP_Text run_bonus_text;
        public TMP_Text short_pass_bonus_text;
        public TMP_Text long_pass_bonus_text;

        [Header("Card Text")]
        public TMP_Text card_title;
        public TMP_Text card_text;

        [Header("New Card Frame")]
        public Image suit_icon;
        public Image position_badge_bg;
        public TMP_Text ability_name_text;
        public TMP_Text stat_label_1;
        public TMP_Text stat_label_2;
        public TMP_Text stat_label_3;
        public Transform slot_requirement_container;
        public GameObject slot_icon_prefab;
        public GameObject player_section;

        public TraitUI[] stats;

        public UnityAction<CardUI> onClick;
        public UnityAction<CardUI> onClickRight;

        private CardData card;
        private VariantData variant;
        private string last_card_id;

        // Suit colors for position badge background
        private static readonly Dictionary<CardSuit, Color> suit_colors = new Dictionary<CardSuit, Color>()
        {
            { CardSuit.Hearts,   new Color32(0xE5, 0x3E, 0x3E, 0xFF) },  // red
            { CardSuit.Spades,   new Color32(0x3D, 0x3D, 0x4D, 0xFF) },  // dark slate
            { CardSuit.Diamonds, new Color32(0x3B, 0x82, 0xF6, 0xFF) },  // blue
            { CardSuit.Clubs,    new Color32(0x22, 0xC5, 0x5E, 0xFF) },  // green
        };

        // -------------------------------------------------------

        public void SetCard(Card card)
        {
            if (card == null)
                return;

            SetCard(card.CardData, card.VariantData);

            // Override with runtime current_stamina (SetCard(CardData) only knows max)
            if (stamina != null && card.CardData != null && card.CardData.IsPlayer())
                stamina.text = "STAMINA " + card.current_stamina;

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
            {
                Sprite art = card.GetFullArt(variant);
                card_image.sprite = art;
                // No art yet (enhancers / live ball cards): flat type-colored placeholder instead of a blank rect
                if (art != null) card_image.color = Color.white;
                else if (card.type == CardType.OffLiveBall) card_image.color = new Color(0.16f, 0.42f, 0.26f);
                else if (card.type == CardType.DefLiveBall) card_image.color = new Color(0.48f, 0.18f, 0.18f);
                else card_image.color = new Color(0.17f, 0.27f, 0.48f);
            }
            if (frame_image != null)
                frame_image.sprite = variant.frame;

            // Title — strip position prefix for player cards ("QB Trent Hawthorne" → "Trent Hawthorne")
            if (card_title != null)
                card_title.text = GetDisplayName(card).ToUpper();

            // Ability / flavour text (resolve {star} etc tokens to inline TMP sprite tags)
            if (card_text != null)
                card_text.text = SlotIconRegistry.ResolveTokens(card.GetText());

            if (card.IsPlayer())
            {
                bool isOff = card.type == CardType.OffensivePlayer;

                if (player_section != null) player_section.SetActive(true);

                // Stamina
                if (stamina_icon != null) stamina_icon.enabled = true;
                if (stamina != null)     { stamina.enabled = true; stamina.text = "STAMINA " + card.stamina; }

                // Grit
                if (grit_icon != null) grit_icon.enabled = true;
                if (grit != null)      { grit.enabled = true; grit.text = "GRIT " + card.grit; }

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

                // Stat labels (offense vs defense)
                if (stat_label_1 != null) stat_label_1.text = isOff ? "RUN" : "RUN COV";
                if (stat_label_2 != null) stat_label_2.text = isOff ? "SHORT" : "SHORT COV";
                if (stat_label_3 != null) stat_label_3.text = isOff ? "DEEP" : "DEEP COV";

                // Suit-based badge color
                if (position_badge_bg != null && suit_colors.ContainsKey(card.suit))
                    position_badge_bg.color = suit_colors[card.suit];

                // Suit icon
                if (suit_icon != null)
                    suit_icon.sprite = GetSuitSprite(card.suit);

                // Ability name (first ability)
                if (ability_name_text != null)
                    ability_name_text.text = (card.abilities != null && card.abilities.Length > 0 && !string.IsNullOrEmpty(card.abilities[0].title))
                        ? card.abilities[0].title.ToUpper() : "";

                // Slot requirements (only repopulate when card changes)
                if (slot_requirement_container != null && last_card_id != card.id)
                    PopulateSlotRequirements(card);
            }
            else
            {
                if (player_section != null) player_section.SetActive(false);

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

            last_card_id = card.id;

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
                grit.text = "GRIT " + value;
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
            if (suit_icon != null)    suit_icon.material     = mat;
            if (position_badge_bg != null) position_badge_bg.material = mat;
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
            if (suit_icon != null)    suit_icon.color    = A(suit_icon.color);
            if (position_badge_bg != null) position_badge_bg.color = A(position_badge_bg.color);
            if (stamina != null)   stamina.color   = A(stamina.color);
            if (grit != null)      grit.color      = A(grit.color);
            if (position != null)  position.color  = A(position.color);
            if (card_title != null)   card_title.color   = A(card_title.color);
            if (card_text != null)    card_text.color    = A(card_text.color);
            if (ability_name_text != null) ability_name_text.color = A(ability_name_text.color);
            if (stat_label_1 != null) stat_label_1.color = A(stat_label_1.color);
            if (stat_label_2 != null) stat_label_2.color = A(stat_label_2.color);
            if (stat_label_3 != null) stat_label_3.color = A(stat_label_3.color);
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

        // -------------------------------------------------------

        private static Sprite GetSuitSprite(CardSuit suit)
        {
            string path = suit switch
            {
                CardSuit.Hearts   => "Sprites/CardFrame/suit_hearts",
                CardSuit.Spades   => "Sprites/CardFrame/suit_spades",
                CardSuit.Diamonds => "Sprites/CardFrame/suit_diamonds",
                CardSuit.Clubs    => "Sprites/CardFrame/suit_clubs",
                _                 => null
            };
            return path != null ? Resources.Load<Sprite>(path) : null;
        }

        private void PopulateSlotRequirements(CardData card)
        {
            foreach (Transform child in slot_requirement_container)
                Destroy(child.gameObject);

            if (card.abilities == null || slot_icon_prefab == null) return;

            foreach (var ability in card.abilities)
            {
                if (ability.slotRequirements == null) continue;
                foreach (var req in ability.slotRequirements)
                {
                    for (int i = 0; i < req.requiredCount; i++)
                    {
                        var icon = Instantiate(slot_icon_prefab, slot_requirement_container);
                        var img = icon.GetComponent<Image>();
                        if (img != null)
                            img.sprite = GetSlotIconSprite(req.icon);
                    }
                }
            }
        }

        private static Sprite GetSlotIconSprite(SlotMachineIconType icon)
        {
            return SlotIconRegistry.GetSprite(icon);
        }

        public CardData GetCard()    => card;
        public VariantData GetVariant() => variant;
    }
}
