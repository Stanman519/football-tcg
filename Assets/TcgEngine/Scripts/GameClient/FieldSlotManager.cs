using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;

public class FieldSlotManager : MonoBehaviour
{
    public GameObject slotPrefab; // Reference to the BoardSlot prefab
    public RectTransform fieldPanel;
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
                // Convert to world position if needed
                Vector3 worldPos = ConvertToWorldPosition(pos);

                // Instantiate slot at correct position
                GameObject slotObj = Instantiate(slotPrefab, worldPos, Quaternion.identity);
                slotObj.transform.SetParent(null);  // Prevents it from inheriting unwanted transforms

                BoardSlot slot = slotObj.GetComponent<BoardSlot>();

                // Assign position type
                slot.player_position_type = position;
                slotMap[position].Add(slot);
            }
        }
    }

    public void GenerateSlotsForPlayer(Player player)
    {
        // Get formation data from the player's Head Coach
        HeadCoachCard headCoach = player.head_coach;
        if (headCoach == null || headCoach.positional_Scheme == null)
        {
            Debug.LogError($"Player {player.player_id} has no head coach assigned!");
            return;
        }

        foreach (var entry in headCoach.positional_Scheme)
        {
            PlayerPositionGrp position = entry.Key;
            int maxPlayers = entry.Value.pos_max;

            slotMap[position] = new List<BoardSlot>();

            for (int i = 0; i < maxPlayers; i++)
            {
                Vector3[] slotPositions = GetSlotPosition(position, i);

                for (int j = 0; j < slotPositions.Length; j++)
                {
                    slotPositions[j].z = -1;
                    GameObject slotObj = Instantiate(slotPrefab, slotPositions[j], Quaternion.identity);
                    slotObj.transform.SetParent(fieldPanel, false);


                    slotObj.transform.localScale = Vector3.one * 60f;
                    slotObj.transform.localPosition = slotPositions[j];
                    BoardSlot slot = slotObj.GetComponent<BoardSlot>();
                    slot.Initialize(position, i, player.player_id);

                    slotObj.transform.localPosition = ConvertToWorldPosition(slotPositions[j]);

                    slotMap[position].Add(slot);
                }

            }
        }
    }
    private Vector3 ConvertToWorldPosition(Vector3 localPos)
    {
        return new Vector3(localPos.x, localPos.y, -1); 
    }
    private Vector3[] GetSlotPosition(PlayerPositionGrp group, int index)
    {
        // Define base position for each group on the field
        Dictionary<PlayerPositionGrp, Vector3[]> basePositions = GenerateFormationPositions();

        if (!basePositions.ContainsKey(group))
            return new Vector3[] { Vector3.zero };

        Vector3[] basePos = basePositions[group];

        return basePos;
    }
    private Dictionary<PlayerPositionGrp, Vector3[]> GenerateFormationPositions()
    {
        float fieldWidth = 100f;  // Approximate width of the football field in world units
        float fieldHeight = 100f; // Approximate height in world units
        float xSpacing = fieldWidth / 10f;  // Divide into logical sections
        float ySpacing = fieldHeight / 20f;

        return new Dictionary<PlayerPositionGrp, Vector3[]>
        {
            { PlayerPositionGrp.QB, new Vector3[] { 
                new Vector3(0,            -3 * ySpacing, 0) } 
            },
            { PlayerPositionGrp.WR, new Vector3[] {
                new Vector3(-20 * xSpacing, 0 * ySpacing, 0), 
                new Vector3(20 * xSpacing, 0 * ySpacing, 0) } },
            { PlayerPositionGrp.RB_TE, new Vector3[] { 
                new Vector3(0 * xSpacing, -9 * ySpacing, 0), //RB
                new Vector3(12 * xSpacing, -2 * ySpacing, 0) } }, //TE
            { PlayerPositionGrp.OL, new Vector3[] { 
                new Vector3(-8 * xSpacing, 0 * ySpacing, 0), 
                new Vector3(-4 * xSpacing, 0 * ySpacing, 0),
                new Vector3(0,            0 * ySpacing, 0), 
                new Vector3(4 * xSpacing, 0 * ySpacing, 0), 
                new Vector3(8 * xSpacing, 0 * ySpacing, 0) } },
            /*{ PlayerPositionGrp.DL, new Vector3[] { new Vector3(-4 * xSpacing, 8 * ySpacing, 0), new Vector3(-2 * xSpacing, 8 * ySpacing, 0),
                                                    new Vector3(0, 8 * ySpacing, 0), new Vector3(2 * xSpacing, 8 * ySpacing, 0), new Vector3(4 * xSpacing, 8 * ySpacing, 0) } },
            { PlayerPositionGrp.LB, new Vector3[] { new Vector3(-3 * xSpacing, 10 * ySpacing, 0), new Vector3(0, 10 * ySpacing, 0), new Vector3(3 * xSpacing, 10 * ySpacing, 0) } },
            { PlayerPositionGrp.DB, new Vector3[] { new Vector3(-5 * xSpacing, 15 * ySpacing, 0), new Vector3(-3 * xSpacing, 15 * ySpacing, 0),
                                                    new Vector3(0, 15 * ySpacing, 0), new Vector3(3 * xSpacing, 15 * ySpacing, 0), new Vector3(5 * xSpacing, 15 * ySpacing, 0) } },*/
        };
    }

    public List<BoardSlot> GetSlotsForPosition(PlayerPositionGrp position)
    {
        return slotMap.ContainsKey(position) ? slotMap[position] : new List<BoardSlot>();
    }
}
