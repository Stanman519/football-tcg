using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine;

namespace TcgEngine.UI
{
    /// <summary>
    /// Main player UI inside the GameUI, inside the game scene
    /// there is one for each player
    /// </summary>

    public class PlayerUI : MonoBehaviour
    {
        public bool is_opponent;
        public Text pname;
        public AvatarUI avatar;
        public IconBar mana_bar;



        public GameObject dead_fx;
        public AudioClip dead_audio;
        public Sprite avatar_dead;

        private bool killed = false;
        private float timer = 0f;

        private int prev_hp = 0;
        private float delayed_damage_timer = 0f;

        private static List<PlayerUI> ui_list = new List<PlayerUI>();

        private void Awake()
        {
            ui_list.Add(this);
        }

        private void OnDestroy()
        {
            ui_list.Remove(this);
        }

        void Start()
        {
            avatar.onClick += OnClickAvatar;
            GameClient.Get().onSecretTrigger += OnSecretTrigger;
        }

        void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            Player player = GetPlayer();

            if (player != null)
            {
                pname.text = player.username;

                AvatarData adata = AvatarData.Get(player.avatar);
                if (avatar != null && adata != null && !killed)
                    avatar.SetAvatar(adata);

                delayed_damage_timer -= Time.deltaTime;
                if (!IsDamagedDelayed())
                    prev_hp = player.hp;
            }
            

            timer += Time.deltaTime;
        }


        public void Kill()
        {
            killed = true;
            avatar.SetImage(avatar_dead);
            AudioTool.Get().PlaySFX("fx", dead_audio);
            FXTool.DoFX(dead_fx, avatar.transform.position);
        }

        public void DelayDamage(int damage, float duration = 1f)
        {
            if (damage != 0)
            {
                delayed_damage_timer = duration;
            }
        }

        public bool IsDamagedDelayed()
        {
            return delayed_damage_timer > 0f;
        }

        private void OnClickAvatar(AvatarData avatar)
        {
            Game gdata = GameClient.Get().GetGameData();
            int player_id = GameClient.Get().GetPlayerID();
            if (gdata.selector == SelectorType.SelectTarget && player_id == gdata.selector_player_id)
            {
                GameClient.Get().SelectPlayer(GetPlayer());
            }
        }

        private void OnSecretTrigger(Card secret, Card triggerer)
        {
        }

        public Player GetPlayer()
        {
            int player_id = is_opponent ? GameClient.Get().GetOpponentPlayerID() : GameClient.Get().GetPlayerID();
            Game data = GameClient.Get().GetGameData();
            return data.GetPlayer(player_id);
        }

        public static PlayerUI Get(bool opponent)
        {
            foreach (PlayerUI ui in ui_list)
            {
                if (ui.is_opponent == opponent)
                    return ui;
            }
            return null;
        }

    }
}