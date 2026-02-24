using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Assets.TcgEngine.Scripts.Gameplay
{

    /// <summary>
    /// Represent a slot in gameplay (data only)
    /// </summary>

    [System.Serializable]
    public struct CardPositionSlot : INetworkSerializable
    {
        public int p; //0 or 1, represent player ID

        public static bool ignore_p = false; //Set to true if you dont want to use P value

        public int max_cards;
        public PlayerPositionGrp posGroupType;

        private static Dictionary<int, List<CardPositionSlot>> player_slots = new Dictionary<int, List<CardPositionSlot>>();
        private static List<CardPositionSlot> all_slots = new List<CardPositionSlot>();

        public CardPositionSlot(int pid, int max_cards = 3, PlayerPositionGrp playerPos = PlayerPositionGrp.NONE) // TODO: CHECK ALL THESE REFS
        {

            this.p = pid;
            this.max_cards = max_cards;
            this.posGroupType = playerPos;
        }






        //Check if the slot is valid one (or if out of board)
        public bool IsValid()
        {
            return posGroupType != PlayerPositionGrp.NONE && max_cards > 0;
        }

        public static int MaxP
        {
            get { return ignore_p ? 0 : 1; } 
        }

        //Return slot P-value of player, usually its same as player_id, unless we ignore P value then its 0 for all
        public static int GetP(int pid)
        {
            return ignore_p ? 0 : pid;
        }


        //Get all slots on player side
        public static List<CardPositionSlot> GetAll(int pid)
        {
            int p = GetP(pid);

            if (player_slots.ContainsKey(p))
                return player_slots[p]; //Faster access

            List<CardPositionSlot> list = new List<CardPositionSlot>();

            player_slots[p] = list;
            return list;
        }

        //Get all valid slots
        public static List<CardPositionSlot> GetAll()
        {
            if (all_slots.Count > 0)
                return all_slots; //Faster access

            return all_slots;
        }

        public static bool operator ==(CardPositionSlot slot1, CardPositionSlot slot2)
        {
            return slot1.p == slot2.p && slot1.posGroupType == slot2.posGroupType;
        }

        public static bool operator !=(CardPositionSlot slot1, CardPositionSlot slot2)
        {
            return slot1.p != slot2.p || slot1.posGroupType != slot2.posGroupType;
        }

        public override bool Equals(object o)
        {
            return base.Equals(o);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref p);
            serializer.SerializeValue(ref posGroupType);
            serializer.SerializeValue(ref max_cards);
        }

        public static CardPositionSlot None
        {
            get { return new CardPositionSlot(0, 0, 0); }
        }
    }

    [System.Serializable]
    public struct SlotXY
    {
        public int x;
        public int y;
    }
}
