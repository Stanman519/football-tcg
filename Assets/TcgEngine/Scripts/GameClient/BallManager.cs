using UnityEngine;

/// <summary>
/// Holds the ball sprite visual. Attach to the BallMarker child of LOSMarker
/// (local position (0, 0, -0.5) so it renders in front of field slots).
/// </summary>
public class BallManager : MonoBehaviour
{
    public SpriteRenderer ballSprite;

    void Awake()
    {
        if (ballSprite == null)
            ballSprite = GetComponent<SpriteRenderer>();
    }
}
