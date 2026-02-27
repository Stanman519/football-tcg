using UnityEngine;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;

/// <summary>
/// LOSMarker sits at the current line of scrimmage in world space.
/// All BoardSlots are children, so their local Y = yards from LOS.
/// The marker lerps to raw_ball_on * unitsPerYard each frame, and
/// GameCamera follows its Y position to keep the LOS on screen.
/// </summary>
public class LOSMarker : MonoBehaviour
{
    [Tooltip("World units per yard. 1 = 1 unit per yard.")]
    public float unitsPerYard = 1.0f;

    [Tooltip("Lerp speed when ball position changes.")]
    public float moveSpeed = 2.5f;

    public static LOSMarker Instance { get; private set; }

    private float targetY;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Game g = GameClient.Get()?.GetGameData();
        int ballOn = g != null ? g.raw_ball_on : 25;
        targetY = ballOn * unitsPerYard;
        transform.position = new Vector3(0f, targetY, 0f);
    }

    void Update()
    {
        Game g = GameClient.Get()?.GetGameData();
        if (g == null) return;

        targetY = g.raw_ball_on * unitsPerYard;
        float y = Mathf.Lerp(transform.position.y, targetY, moveSpeed * Time.deltaTime);
        transform.position = new Vector3(0f, y, 0f);
    }
}
