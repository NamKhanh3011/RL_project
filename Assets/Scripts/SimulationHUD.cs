using UnityEngine;

/// <summary>
/// Renders the bottom HUD bar matching the exact visual style in the reference image:
/// - Dark navy translucent bar pinned to the bottom of the screen.
/// - Top row: Episode, Time, Rewards
/// - Bottom row: Steps, Best, Dirt
/// </summary>
public class SimulationHUD : MonoBehaviour
{
    [Header("Current Simulation Stats")]
    public int episode = 1;
    public int steps = 0;
    public float elapsedTime = 0f;
    public float bestTime = 0f;
    public float rewards = 0f;
    public int dirtRemaining = 0;

    [Header("Customization")]
    public Color barColor = new Color(0.12f, 0.16f, 0.26f, 0.85f);
    public int fontSize = 20;

    private Texture2D bgTexture;
    private GUIStyle labelStyle;
    private GUIStyle barStyle;

    void Awake()
    {
        bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, barColor);
        bgTexture.Apply();
    }

    public void UpdateDisplay(int ep, int st, float time, float best, float rew, int dirt)
    {
        episode = ep;
        steps = st;
        elapsedTime = time;
        bestTime = best;
        rewards = rew;
        dirtRemaining = dirt;
    }

    void OnGUI()
    {
        InitStyles();

        float barHeight = 65f;
        float y = Screen.height - barHeight;
        float width = Screen.width;

        // Draw background bar
        GUI.Box(new Rect(0, y, width, barHeight), GUIContent.none, barStyle);

        // Columns setup (3 columns: Left, Center, Right)
        float colWidth = width / 3f;
        float padding = 35f;
        float line1Y = y + 8f;
        float line2Y = y + 34f;

        // Row 1: Episode | Time | Rewards
        GUI.Label(new Rect(padding, line1Y, colWidth, 25f), $"Episode: {episode}", labelStyle);
        GUI.Label(new Rect(colWidth + padding, line1Y, colWidth, 25f), $"Time: {FormatTimer(elapsedTime)}", labelStyle);
        GUI.Label(new Rect(colWidth * 2f + padding, line1Y, colWidth, 25f), $"Rewards: {rewards:F2}", labelStyle);

        // Row 2: Steps | Best | Dirt
        GUI.Label(new Rect(padding, line2Y, colWidth, 25f), $"Steps: {steps}", labelStyle);
        GUI.Label(new Rect(colWidth + padding, line2Y, colWidth, 25f), $"Best: {FormatTimer(bestTime)}", labelStyle);
        GUI.Label(new Rect(colWidth * 2f + padding, line2Y, colWidth, 25f), $"Dirt: {dirtRemaining}", labelStyle);
    }

    private void InitStyles()
    {
        if (barStyle == null)
        {
            barStyle = new GUIStyle();
            barStyle.normal.background = bgTexture;
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = fontSize;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.normal.textColor = Color.white;
            labelStyle.alignment = TextAnchor.MiddleLeft;
        }
    }

    private string FormatTimer(float seconds)
    {
        int min = Mathf.FloorToInt(seconds / 60f);
        int sec = Mathf.FloorToInt(seconds % 60f);
        return $"{min:00}:{sec:00}";
    }

    void OnDestroy()
    {
        if (bgTexture != null) Destroy(bgTexture);
    }
}
