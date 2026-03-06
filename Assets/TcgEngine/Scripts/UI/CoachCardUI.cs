using UnityEngine;
using UnityEngine.UI;
using TcgEngine;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;

/// <summary>
/// Displays a coach card in the play call panel.
/// Wire fields in Inspector on a CoachCard prefab (duplicated from player card prefab).
/// </summary>
public class CoachCardUI : MonoBehaviour
{
    [Header("Card Fields")]
    public Image  card_image;
    public Image  frame_image;
    public Text   card_title;
    public Text   card_text;
    public Text   run_bonus_text;
    public Text   short_pass_bonus_text;
    public Text   long_pass_bonus_text;
    public Text   position_label;   // "OFF COACH" / "DEF COACH"
    public Text   scheme_text;      // "1 QB  2 WR  2 OL ..."

    public void SetCoach(HeadCoachCard runtime, CoachCardData asset, bool isOffense)
    {
        if (asset == null) return;

        if (card_title  != null) card_title.text  = asset.title;
        if (card_text   != null) card_text.text   = asset.ability_text;
        if (card_image  != null && asset.card_image != null) card_image.sprite = asset.card_image;
        if (position_label != null) position_label.text = isOffense ? "OFF COACH" : "DEF COACH";

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

    private void SetStat(Text field, string label, int val, string sign, string unit)
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
