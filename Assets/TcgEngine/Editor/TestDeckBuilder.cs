using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using TcgEngine;

public class TestDeckBuilder
{
    [MenuItem("Tools/Build Test Decks")]
    public static void BuildTestDecks()
    {
        // Load all cards
        CardData[] allCards = Resources.LoadAll<CardData>("");
        Debug.Log($"[TestDeckBuilder] Found {allCards.Length} total cards");

        var offPlayers = allCards.Where(c => c.type == CardType.OffensivePlayer).ToList();
        var defPlayers = allCards.Where(c => c.type == CardType.DefensivePlayer).ToList();
        var offEnhancers = allCards.Where(c => c.type == CardType.OffensivePlayEnhancer).ToList();
        var defEnhancers = allCards.Where(c => c.type == CardType.DefensivePlayEnhancer).ToList();
        var offLiveBall = allCards.Where(c => c.type == CardType.OffLiveBall).ToList();
        var defLiveBall = allCards.Where(c => c.type == CardType.DefLiveBall).ToList();

        Debug.Log($"[TestDeckBuilder] Off:{offPlayers.Count} Def:{defPlayers.Count} OE:{offEnhancers.Count} DE:{defEnhancers.Count} OLB:{offLiveBall.Count} DLB:{defLiveBall.Count}");

        GameplayData gdata = Resources.Load<GameplayData>("GameplayData");
        int deckSize = gdata != null ? gdata.deck_size : 20;

        // Build deck 1 (testie_boy) — balanced mix
        List<CardData> deck1Cards = BuildBalancedDeck(offPlayers, defPlayers, offEnhancers, defEnhancers, offLiveBall, defLiveBall, deckSize, 0);
        // Build deck 2 (test_deck) — different cards where possible
        List<CardData> deck2Cards = BuildBalancedDeck(offPlayers, defPlayers, offEnhancers, defEnhancers, offLiveBall, defLiveBall, deckSize, 1);

        // Update testie_boy
        string[] deckGuids = AssetDatabase.FindAssets("t:DeckData testie_boy");
        if (deckGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(deckGuids[0]);
            DeckData deck = AssetDatabase.LoadAssetAtPath<DeckData>(path);
            deck.cards = deck1Cards.ToArray();
            deck.hero = null; // no hero card needed
            EditorUtility.SetDirty(deck);
            Debug.Log($"[TestDeckBuilder] Updated testie_boy with {deck1Cards.Count} cards");
        }

        // Update test_deck
        deckGuids = AssetDatabase.FindAssets("t:DeckData test_deck");
        foreach (string guid in deckGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            DeckData deck = AssetDatabase.LoadAssetAtPath<DeckData>(path);
            if (deck.name == "test_deck")
            {
                deck.cards = deck2Cards.ToArray();
                deck.hero = null;
                EditorUtility.SetDirty(deck);
                Debug.Log($"[TestDeckBuilder] Updated test_deck with {deck2Cards.Count} cards");
                break;
            }
        }

        // Fix ALL remaining DeckData assets that have null card refs
        string[] allDeckGuids = AssetDatabase.FindAssets("t:DeckData");
        foreach (string guid in allDeckGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            DeckData deck = AssetDatabase.LoadAssetAtPath<DeckData>(path);
            if (deck == null || deck.cards == null) continue;

            bool hasNull = deck.cards.Any(c => c == null);
            if (!hasNull) continue;

            // Replace with a balanced deck
            deck.cards = BuildBalancedDeck(offPlayers, defPlayers, offEnhancers, defEnhancers, offLiveBall, defLiveBall, deckSize, 0).ToArray();
            deck.hero = null;
            EditorUtility.SetDirty(deck);
            Debug.Log($"[TestDeckBuilder] Fixed null cards in {deck.name} ({deck.id})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TestDeckBuilder] Done!");
    }

    static List<CardData> BuildBalancedDeck(
        List<CardData> offPlayers, List<CardData> defPlayers,
        List<CardData> offEnhancers, List<CardData> defEnhancers,
        List<CardData> offLiveBall, List<CardData> defLiveBall,
        int deckSize, int offset)
    {
        var deck = new List<CardData>();

        // Take offensive players (aim for ~7)
        AddFromList(deck, offPlayers, 7, offset);
        // Take defensive players (aim for ~7)
        AddFromList(deck, defPlayers, 7, offset);
        // Take enhancers (aim for ~2 each)
        AddFromList(deck, offEnhancers, 2, offset);
        AddFromList(deck, defEnhancers, 2, offset);
        // Take live ball (aim for ~1 each)
        AddFromList(deck, offLiveBall, 1, offset);
        AddFromList(deck, defLiveBall, 1, offset);

        // Fill remaining slots with more players
        int remaining = deckSize - deck.Count;
        if (remaining > 0)
        {
            var extras = offPlayers.Concat(defPlayers)
                .Where(c => !deck.Contains(c))
                .Take(remaining)
                .ToList();
            deck.AddRange(extras);
        }

        // Trim if over
        if (deck.Count > deckSize)
            deck = deck.Take(deckSize).ToList();

        return deck;
    }

    static void AddFromList(List<CardData> deck, List<CardData> source, int count, int offset)
    {
        if (source.Count == 0) return;
        int start = Mathf.Min(offset * count, source.Count - 1);
        for (int i = 0; i < count && i + start < source.Count; i++)
        {
            var card = source[i + start];
            if (!deck.Contains(card))
                deck.Add(card);
        }
    }
}
