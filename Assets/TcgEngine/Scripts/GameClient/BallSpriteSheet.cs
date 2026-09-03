using UnityEngine;

/// <summary>
/// Data container for football sprite animation frames.
/// Assign sliced sprites from the two sprite sheets.
/// </summary>
[CreateAssetMenu(fileName = "BallSpriteSheet", menuName = "First&Long/BallSpriteSheet")]
public class BallSpriteSheet : ScriptableObject
{
    [Header("Held / Carried (subtle wobble)")]
    public Sprite[] heldFrames;    // 3 frames from fumble sheet rows 0-2
    public float heldFPS = 4f;

    [Header("Spiral / Thrown")]
    public Sprite[] spiralFrames;  // 10 frames from spiral sheet
    public float spiralFPS = 12f;

    [Header("Tumble (fumble / incomplete)")]
    public Sprite[] tumbleFrames;  // 12 frames from fumble sheet
    public float tumbleFPS = 10f;

    [Header("Loose Ball (rolling on ground)")]
    public Sprite[] looseFrames;   // middle row of fumble sheet
    public float looseFPS = 6f;

    [Header("On Ground (static)")]
    public Sprite onGroundSprite;  // single frame, flat angle
}
