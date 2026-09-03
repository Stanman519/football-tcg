using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Single source of truth for slot icon sprites, TMP inline tags, and display names.
    /// All consumers (slot reels, card UI, inline text) go through this class.
    /// </summary>
    public static class SlotIconRegistry
    {
        private static readonly Dictionary<SlotMachineIconType, Sprite> spriteCache = new();

        private static readonly Dictionary<SlotMachineIconType, string> resourceNames = new()
        {
            { SlotMachineIconType.Football, "football" },
            { SlotMachineIconType.Helmet,   "helmet" },
            { SlotMachineIconType.Star,     "star" },
            { SlotMachineIconType.WildCard, "wild" },
            { SlotMachineIconType.Wrench,   "wrench" },
        };

        private static readonly Dictionary<SlotMachineIconType, string> displayNames = new()
        {
            { SlotMachineIconType.Football, "Footballs" },
            { SlotMachineIconType.Helmet,   "Helmets" },
            { SlotMachineIconType.Star,     "Stars" },
            { SlotMachineIconType.WildCard, "Wilds" },
            { SlotMachineIconType.Wrench,   "Wrenches" },
        };

        /// <summary>Load sprite from the canonical Resources/SlotMachine/ folder (cached).</summary>
        public static Sprite GetSprite(SlotMachineIconType icon)
        {
            if (icon == SlotMachineIconType.None) return null;
            if (spriteCache.TryGetValue(icon, out var cached)) return cached;

            string name = resourceNames.TryGetValue(icon, out var n) ? n : icon.ToString().ToLower();
            var sprite = Resources.Load<Sprite>($"SlotMachine/slot-{name}-icon");
            spriteCache[icon] = sprite;
            return sprite;
        }

        /// <summary>TMP rich-text sprite tag for inline icon rendering, e.g. &lt;sprite name="star"&gt;</summary>
        public static string GetSpriteTag(SlotMachineIconType icon)
        {
            if (icon == SlotMachineIconType.None) return "";
            string name = resourceNames.TryGetValue(icon, out var n) ? n : icon.ToString().ToLower();
            return $"<sprite name=\"{name}\">";
        }

        /// <summary>Human-readable plural name, e.g. "Stars", "Helmets".</summary>
        public static string GetDisplayName(SlotMachineIconType icon)
        {
            return displayNames.TryGetValue(icon, out var name) ? name : icon.ToString();
        }

        /// <summary>
        /// Replace {star}, {football}, {helmet}, {wild}, {wrench} tokens in text
        /// with TMP sprite tags for inline icon rendering.
        /// </summary>
        public static string ResolveTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            foreach (var kvp in resourceNames)
            {
                string token = $"{{{kvp.Value}}}";
                if (text.Contains(token))
                    text = text.Replace(token, GetSpriteTag(kvp.Key));
            }
            return text;
        }
    }
}
