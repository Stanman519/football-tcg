using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine;
using UnityEngine.UI;

public class FieldManager : MonoBehaviour
{
    public RectTransform fieldPanel; // The UI Panel containing the field image
    public RectTransform fieldBackground; // The actual field image
    public GameObject slotPrefab; // Prefab for player slots
    public Transform slotParent; // Parent to hold all slots
    public float yardToPixelRatio = 10f; // Adjust based on field size
    public int maxYardView = 40; // How many yards are visible at a time

    private int currentBallYardLine = 25; // Current line of scrimmage (centered)
    private List<BoardSlot> activeSlots = new List<BoardSlot>(); // Tracks active slots

    void Start()
    {
        yardToPixelRatio = CalculateYardToPixelRatio();
        CenterFieldOnYardLine(currentBallYardLine);
    }

    public IEnumerator SmoothMoveField(int newBallYardLine)
    {
        int yardageDifference = newBallYardLine - currentBallYardLine;
        float pixelMovement = yardageDifference * yardToPixelRatio;

        Vector2 targetPos = new Vector2(fieldPanel.anchoredPosition.x, fieldPanel.anchoredPosition.y - pixelMovement);

        float duration = 0.5f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            fieldPanel.anchoredPosition = Vector2.Lerp(fieldPanel.anchoredPosition, targetPos, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        fieldPanel.anchoredPosition = targetPos;
        currentBallYardLine = newBallYardLine;
    }

    public void CenterFieldOnYardLine(int yardLine)
    {
        float centerPixelOffset = (yardLine - 50) * yardToPixelRatio; // 50-yard line is center
        fieldPanel.anchoredPosition = new Vector2(fieldPanel.anchoredPosition.x, centerPixelOffset);
    }

    public void UpdateSlotsForFormation(Formation formation, bool isOffense)
    {
        ClearSlots(); // Remove old slots before placing new ones

        foreach (FormationSlotData slotData in formation.slots)
        {
            if ((isOffense && slotData.isOffense) || (!isOffense && !slotData.isOffense))
            {
                CreateSlot(slotData);
            }
        }
    }
    public float CalculateYardToPixelRatio()
    {
        float scaleY = fieldBackground.lossyScale.y;
        return (fieldBackground.rect.height * scaleY) / 100f;
    }
    private void CreateSlot(FormationSlotData slotData)
    {
        GameObject newSlotObj = Instantiate(slotPrefab, slotParent);
        RectTransform slotTransform = newSlotObj.GetComponent<RectTransform>();

        float xPosition = slotData.xOffset * yardToPixelRatio;
        float yPosition = (currentBallYardLine - slotData.yardLine) * yardToPixelRatio;

        slotTransform.anchoredPosition = new Vector2(xPosition, yPosition);

        BoardSlot boardSlot = newSlotObj.GetComponent<BoardSlot>();
        boardSlot.Initialize(slotData.positionGroup);

        activeSlots.Add(boardSlot);
    }

    private void ClearSlots()
    {
        foreach (BoardSlot slot in activeSlots)
        {
            Destroy(slot.gameObject);
        }
        activeSlots.Clear();
    }
}
