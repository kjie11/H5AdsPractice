using TMPro;
using UnityEngine;

public sealed class ItemCounterUI : MonoBehaviour
{
    [SerializeField] private SlotManager slotManager;
    [SerializeField] private TMP_Text bowlingBallText;
    [SerializeField] private TMP_Text bowlingPinText;
    [SerializeField] private TMP_Text cricketBatText;

    private int bowlingBalls;
    private int bowlingPins;
    private int cricketBats;

    private void Awake()
    {
        if (slotManager == null)
        {
            slotManager = FindFirstObjectByType<SlotManager>();
        }

        foreach (ClickItem item in
                 FindObjectsByType<ClickItem>(FindObjectsSortMode.None))
        {
            switch (item.Type)
            {
                case ItemType.Baolingqiu:
                    bowlingBalls++;
                    break;
                case ItemType.Baolingqiuping:
                    bowlingPins++;
                    break;
                case ItemType.Banqiupai:
                    cricketBats++;
                    break;
            }
        }

        RefreshText();
    }

    private void OnEnable()
    {
        if (slotManager != null)
        {
            slotManager.ItemPlaced += OnItemPlaced;
        }
    }

    private void OnDisable()
    {
        if (slotManager != null)
        {
            slotManager.ItemPlaced -= OnItemPlaced;
        }
    }

    private void OnItemPlaced(ItemType type)
    {
        switch (type)
        {
            case ItemType.Baolingqiu:
                bowlingBalls = Mathf.Max(0, bowlingBalls - 1);
                break;
            case ItemType.Baolingqiuping:
                bowlingPins = Mathf.Max(0, bowlingPins - 1);
                break;
            case ItemType.Banqiupai:
                cricketBats = Mathf.Max(0, cricketBats - 1);
                break;
            default:
                return;
        }

        RefreshText();
    }

    private void RefreshText()
    {
        bowlingBallText.text = bowlingBalls.ToString();
        bowlingPinText.text = bowlingPins.ToString();
        cricketBatText.text = cricketBats.ToString();
    }
}
