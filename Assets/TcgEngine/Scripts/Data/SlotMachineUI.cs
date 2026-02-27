using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;

[Serializable]
public class ReelUI
{
    public Image TopImage;
    public Image MiddleImage;
    public Image BottomImage;
}
[Serializable]
public class SlotMachineUI : MonoBehaviour
{
    public GameObject reelPrefab; // Assign in Inspector
    public Transform reelContainer; // Parent object for reels
    public List<ReelUI> reelUIs = new List<ReelUI>();
    private GameObject slotMachinePanel; // The panel containing the slot machine

    [Header("Layout — Full (during spin)")]
    public Vector2 fullAnchorMin = new Vector2(0.15f, 0.2f);
    public Vector2 fullAnchorMax = new Vector2(0.85f, 0.8f);

    [Header("Layout — Mini (all other phases)")]
    public Vector2 miniAnchorMin = new Vector2(0.72f, 0.02f);
    public Vector2 miniAnchorMax = new Vector2(0.98f, 0.38f);

    private void Start()
    {
        slotMachinePanel = reelContainer?.parent?.gameObject;
        if (slotMachinePanel != null)
        {
            slotMachinePanel.SetActive(true); // Always visible
            SetLayout(mini: true);            // Start collapsed
        }

        GameClient client = GameClient.Get();
        if (client != null)
            client.onRefreshAll += OnGameDataRefreshed;
    }

    private void OnDestroy()
    {
        GameClient client = GameClient.Get();
        if (client != null)
            client.onRefreshAll -= OnGameDataRefreshed;
    }

    private void OnGameDataRefreshed()
    {
        Game gameData = GameClient.Get()?.GetGameData();
        if (gameData == null || slotMachinePanel == null) return;

        bool isSpinning = gameData.phase == GamePhase.SlotSpin;
        SetLayout(mini: !isSpinning);
    }

    private void SetLayout(bool mini)
    {
        if (slotMachinePanel == null) return;
        RectTransform rt = slotMachinePanel.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.anchorMin = mini ? miniAnchorMin : fullAnchorMin;
        rt.anchorMax = mini ? miniAnchorMax : fullAnchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public void InitializeReels(int numReels)
    {
        // Clear existing reels if necessary
        foreach (Transform child in reelContainer)
        {
            Destroy(child.gameObject);
        }
        reelUIs.Clear();
        float spacing = 200f;
        for (int i = 0; i < numReels; i++)
        {
            GameObject newReel = Instantiate(reelPrefab, reelContainer);
            newReel.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 200);
            // Set Position
            RectTransform reelTransform = newReel.GetComponent<RectTransform>();
            reelTransform.anchoredPosition = new Vector2(i * spacing, 0); // Spacing reels horizontally

            Debug.Log($"Reel {i} instantiated at {newReel.transform.position}");
            ReelUI reelUI = new ReelUI
            {
                TopImage = newReel.transform.Find("TopImage").GetComponent<Image>(),
                MiddleImage = newReel.transform.Find("MiddleImage").GetComponent<Image>(),
                BottomImage = newReel.transform.Find("BottomImage").GetComponent<Image>()
            };
            reelUIs.Add(reelUI);
        }
    }

    public void FireReelUI(List<ReelSpriteData> calculatedResults, List<SlotData> slotData)
    {
        for (int i = 0; i < calculatedResults.Count; i++)
        {
            StartCoroutine(DisplayReelResults(calculatedResults[i], slotData.First(d => d.id == i)));
        }
    }

    private IEnumerator DisplayReelResults(ReelSpriteData calculatedResults, SlotData slotData)
    {
        float elapsed = 0f;

        while (elapsed < slotData.stopDelay)
        {
            elapsed += Time.deltaTime;

            var topSprite = PickRandomIcon(slotData.reelIconInventory);
            var midSprite = PickRandomIcon(slotData.reelIconInventory);
            var botSprite = PickRandomIcon(slotData.reelIconInventory);

            UpdateReelUI(reelUIs[slotData.id], topSprite, midSprite, botSprite);

            yield return null; // wait a frame
        }

        // Show final result
        UpdateReelUI(reelUIs[slotData.id], calculatedResults.Top.Image, calculatedResults.Middle.Image, calculatedResults.Bottom.Image);
    }

    private Sprite PickRandomIcon(List<SlotIconData> reelIcons)
    {
        if (reelIcons == null || reelIcons.Count == 0)
            return null;

        int index = UnityEngine.Random.Range(0, reelIcons.Count);
        return reelIcons[index].IconSprite;
    }

    private void UpdateReelUI(ReelUI reelUI, Sprite top, Sprite mid, Sprite bot)
    {
        reelUI.TopImage.sprite = top;
        reelUI.MiddleImage.sprite = mid;
        reelUI.BottomImage.sprite = bot;
    }
}