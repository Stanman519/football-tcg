using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Data
{
    public class FieldUI : MonoBehaviour
    {
        public GameObject ballMarker; // The visual object representing the ball

        public void UpdateBallPosition(int rawBallOn)
        {
            string displayPosition = GetFootballYardLine(rawBallOn);
            Debug.Log($"Ball is now on {displayPosition}");

            // Move the ball marker UI accordingly
            float xPos = ConvertRawPositionToUI(rawBallOn);
            ballMarker.transform.position = new Vector3(xPos, ballMarker.transform.position.y, ballMarker.transform.position.z);
        }

        private string GetFootballYardLine(int rawYard)
        {
            if (rawYard <= 50)
            {
                return $"Own {rawYard}";
            }
            else
            {
                return $"Opponent {100 - rawYard}";
            }
        }

        private float ConvertRawPositionToUI(int rawYard)
        {
            // Assuming a normalized field range where 0 = one endzone and 100 = the other endzone
            float fieldLength = 10.0f; // Example Unity field length
            return (rawYard / 100f) * fieldLength;
        }
    }
}
