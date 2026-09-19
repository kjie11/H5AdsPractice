using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public sealed class CountdownTimer : MonoBehaviour
{
    [SerializeField, Min(1f)] private float durationSeconds = 120f;
    [SerializeField] private RectTransform progressFill;

    private TMP_Text timeText;
    private float remainingSeconds;
    private int displayedSeconds = -1;

    public float RemainingSeconds => remainingSeconds;
    public bool IsFinished => remainingSeconds <= 0f;

    private void Awake()
    {
        timeText = GetComponent<TMP_Text>();
        ResetCountdown();
    }

    private void Update()
    {
        if (IsFinished)
        {
            return;
        }

        remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
        RefreshDisplay();
    }

    public void ResetCountdown()
    {
        remainingSeconds = Mathf.Max(0f, durationSeconds);
        displayedSeconds = -1;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        int wholeSeconds = Mathf.CeilToInt(remainingSeconds);
        if (wholeSeconds != displayedSeconds)
        {
            displayedSeconds = wholeSeconds;
            timeText.text = $"{wholeSeconds / 60:00}:{wholeSeconds % 60:00}";
        }

        if (progressFill == null)
        {
            return;
        }

        float progress = durationSeconds > 0f
            ? Mathf.Clamp01(remainingSeconds / durationSeconds)
            : 0f;

        Vector2 anchorMax = progressFill.anchorMax;
        anchorMax.x = progress;
        progressFill.anchorMax = anchorMax;
    }
}
