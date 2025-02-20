using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Client;
using TcgEngine;

public class FieldSlotManager : MonoBehaviour
{
    public GameObject slotPrefab; // Reference to the BoardSlot prefab
    public GameObject replacementPrefab; // X or O placeholder prefab
    private Dictionary<PlayerPositionGrp, List<BoardSlot>> slotMap = new Dictionary<PlayerPositionGrp, List<BoardSlot>>();

    private Formation currentFormation;

    public void InitializeFormation(Formation formation)
    {
        currentFormation = formation;
        GenerateSlots();
    }

    private void GenerateSlots()
    {
        foreach (var entry in currentFormation.slotPositions)
        {
            PlayerPositionGrp position = entry.Key;
            Vector3[] positions = entry.Value;

            slotMap[position] = new List<BoardSlot>();

            foreach (Vector3 pos in positions)
            {
                GameObject slotObj = Instantiate(slotPrefab, pos, Quaternion.identity, transform);
                BoardSlot slot = slotObj.GetComponent<BoardSlot>();

                slot.player_position_type = position;
                slotMap[position].Add(slot);

                // Create a replacement player (X or O)
                GameObject replacement = Instantiate(replacementPrefab, pos, Quaternion.identity, transform);
                slot.SetReplacement(replacement);
            }
        }
    }

    public List<BoardSlot> GetSlotsForPosition(PlayerPositionGrp position)
    {
        return slotMap.ContainsKey(position) ? slotMap[position] : new List<BoardSlot>();
    }
