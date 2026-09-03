using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TcgEngine;
using TcgEngine.Client;
using TcgEngine.Gameplay;

/// <summary>
/// Displays a coach card in the play call panel.
/// Wire fields in Inspector or via PlayCallPanelBuilder.
/// </summary>
public class CoachCardUI : MonoBehaviour
{
    [Header("Card Fields")]
    public Image  card_image;
    public Image  frame_image;
    public TextMeshProUGUI card_title;
    public TextMeshProUGUI card_text;
    public TextMeshProUGUI run_bonus_text;
    public TextMeshProUGUI short_pass_bonus_text;
    public TextMeshProUGUI long_pass_bonus_text;
    public TextMeshProUGUI position_label;   // "MY COACH" / "DEF COACH"
    public TextMeshProUGUI scheme_text;      // "1 QB  2 WR  2 OL ..."

    public void SetCoach(HeadCoachCard runtime, CoachCardData asset, bool isOffense)
    {
        if (asset == null) return;

        if (card_title  != null) card_title.text  = asset.title;
        if (card_text   != null) card_text.text   = asset.ability_text;
        if (card_image  != null && asset.card_image != null) card_image.sprite = asset.card_image;
        if (position_label != null) position_label.text = isOffense ? "MY COACH" : "DEF COACH";

        if (isOffense)
        {
            SetStat(run_bonus_text,        "Run",   asset.GetOffenseYardage(PlayType.Run),       "+", "yds");
            SetStat(short_pass_bonus_text, "Short", asset.GetOffenseYardage(PlayType.ShortPass), "+", "yds");
            SetStat(long_pass_bonus_text,  "Long",  asset.GetOffenseYardage(PlayType.LongPass),  "+", "yds");
        }
        else
        {
            SetStat(run_bonus_text,        "Run",   asset.GetDefenseYardage(PlayType.Run),       "-", "yds");
            SetStat(short_pass_bonus_text, "Short", asset.GetDefenseYardage(PlayType.ShortPass), "-", "yds");
            SetStat(long_pass_bonus_text,  "Long",  asset.GetDefenseYardage(PlayType.LongPass),  "-", "yds");
        }

        if (scheme_text != null)
            scheme_text.text = BuildSchemeText(asset);
    }

    public void SetCoachFromPlayer(Player p, bool isOffense)
    {
        if (p == null || string.IsNullOrEmpty(p.coach_card_id)) return;
        CoachCardData asset = Resources.Load<CoachCardData>("Coaches/" + p.coach_card_id);
        if (asset == null)
        {
            Debug.LogWarning($"[CoachCardUI] Could not load CoachCardData at Coaches/{p.coach_card_id}");
            return;
        }
        SetCoach(p.head_coach, asset, isOffense);
    }

    private void SetStat(TextMeshProUGUI field, string label, int val, string sign, string unit)
    {
        if (field != null)
            field.text = $"{label} {sign}{val} {unit}";
    }

    private string BuildSchemeText(CoachCardData asset)
    {
        if (asset.positionalScheme == null || asset.positionalScheme.Length == 0)
            return "";
        var sb = new System.Text.StringBuilder();
        foreach (var e in asset.positionalScheme)
            sb.Append($"{e.maxCards} {e.position}  ");
        return sb.ToString().TrimEnd();
    }
}
