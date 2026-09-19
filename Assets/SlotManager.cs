using System;
using UnityEngine;
using DG.Tweening;

public class SlotManager : MonoBehaviour
{
    public event Action MatchCompleted;
    public event Action<ItemType> ItemPlaced;

    [SerializeField] private RectTransform[] slots;

    [Header("Slot Bounce")]
    [SerializeField] private float pressPixels = 12f;
    [SerializeField] private float pressDuration = 0.04f;
    [SerializeField] private float bounceDuration = 0.08f;

    [Header("Match Animation")]
    [SerializeField] private float mergeHeightPixels = 250f;
    [SerializeField] private float mergeDuration = 0.22f;
    [SerializeField] private float disappearDuration = 0.12f;

    private ClickItem[] slotItems;
    private bool[] occupied;
    private bool[] landed;

    private bool isMatching = false;
    private bool waitingForSettleAfterMatch = false;

    private Camera mainCamera;


    private void Awake()
    {
        slotItems = new ClickItem[slots.Length];
        occupied = new bool[slots.Length];
        landed = new bool[slots.Length];

        mainCamera = Camera.main;
    }


    // 点击时立即预占 Slot，防止多个飞行动画使用同一个位置
    public bool TryReserveSlot(
        ClickItem item,
        out int slotIndex
    )
    {
        slotIndex = -1;

        if (item == null)
        {
            return false;
        }

        for (int i = 0; i < occupied.Length; i++)
        {
            if (!occupied[i] && slots[i] != null)
            {
                occupied[i] = true;
                landed[i] = false;
                slotItems[i] = item;
                slotIndex = i;

                return true;
            }
        }

        Debug.Log("No empty slot left.");

        return false;
    }


    // 根据 Index 获取对应 Slot
    public RectTransform GetSlot(int index)
    {
        if (index < 0 || index >= slots.Length)
        {
            return null;
        }

        return slots[index];
    }


    // ClickItem 落稳后调用
    public void NotifyItemLanded(
        int slotIndex,
        ClickItem item
    )
    {
        if (slotIndex < 0 ||
            slotIndex >= slotItems.Length ||
            !occupied[slotIndex] ||
            slotItems[slotIndex] != item)
        {
            return;
        }

        if (landed[slotIndex])
        {
            return;
        }

        landed[slotIndex] = true;
        ItemPlaced?.Invoke(item.Type);

        if (waitingForSettleAfterMatch)
        {
            TryFinishMatchCleanup();
            return;
        }

        if (!isMatching)
        {
            CheckForMatch();
        }
    }


    // 检查是否有连续三个相同物品
    private void CheckForMatch()
    {
        for (int i = 0; i <= slotItems.Length - 3; i++)
        {
            ClickItem left =
                slotItems[i];

            ClickItem middle =
                slotItems[i + 1];

            ClickItem right =
                slotItems[i + 2];


            if (!occupied[i] ||
                !occupied[i + 1] ||
                !occupied[i + 2] ||
                !landed[i] ||
                !landed[i + 1] ||
                !landed[i + 2] ||
                left == null ||
                middle == null ||
                right == null)
            {
                continue;
            }


            if (left.Type == middle.Type &&
                middle.Type == right.Type)
            {
                Debug.Log(
                    $"Match Found: {left.Type}"
                );

                PlayMatchAnimation(
                    i,
                    i + 1,
                    i + 2,
                    left,
                    middle,
                    right
                );

                return;
            }
        }
    }


    // 三消动画
    private void PlayMatchAnimation(
        int leftIndex,
        int middleIndex,
        int rightIndex,
        ClickItem left,
        ClickItem middle,
        ClickItem right
    )
    {
        isMatching = true;


        Transform leftTransform =
            left.transform;

        Transform middleTransform =
            middle.transform;

        Transform rightTransform =
            right.transform;


        // 防止之前 Tween 还在运行
        leftTransform.DOKill();
        middleTransform.DOKill();
        rightTransform.DOKill();


        // ==============================
        // 计算屏幕上的汇合点
        // ==============================

        RectTransform middleSlot =
            slots[middleIndex];


        Canvas canvas =
            middleSlot.GetComponentInParent<Canvas>();


        Camera uiCamera = null;


        if (canvas != null &&
            canvas.renderMode !=
            RenderMode.ScreenSpaceOverlay)
        {
            uiCamera =
                canvas.worldCamera;
        }


        // 中间 Slot 当前屏幕位置
        Vector2 middleScreenPos =
            RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                middleSlot.position
            );


        // 往屏幕上方移动指定像素
        Vector2 mergeScreenPos =
            middleScreenPos +
            Vector2.up *
            mergeHeightPixels;


        // 获取中间物体相对于摄像机的深度
        float depth =
            Vector3.Dot(
                middleTransform.position -
                mainCamera.transform.position,

                mainCamera.transform.forward
            );


        // 屏幕坐标 -> 3D 世界坐标
        Vector3 mergePosition =
            mainCamera.ScreenToWorldPoint(
                new Vector3(
                    mergeScreenPos.x,
                    mergeScreenPos.y,
                    depth
                )
            );


        Debug.Log(
            $"Merge Height Pixels: {mergeHeightPixels}"
        );

        Debug.Log(
            $"Merge Position: {mergePosition}"
        );


        // ==============================
        // 创建动画 Sequence
        // ==============================

        Sequence sequence =
            DOTween.Sequence();


        // ==============================
        // 1. 三个一起向中间汇合
        // ==============================

        sequence.Append(
            leftTransform.DOMove(
                mergePosition,
                mergeDuration
            )
            .SetEase(Ease.InOutQuad)
        );


        sequence.Join(
            middleTransform.DOMove(
                mergePosition,
                mergeDuration
            )
            .SetEase(Ease.InOutQuad)
        );


        sequence.Join(
            rightTransform.DOMove(
                mergePosition,
                mergeDuration
            )
            .SetEase(Ease.InOutQuad)
        );


        // 左边稍微向内旋转
        sequence.Join(
            leftTransform.DORotate(
                new Vector3(
                    0f,
                    0f,
                    -15f
                ),
                mergeDuration,
                RotateMode.Fast
            )
            .SetEase(Ease.OutQuad)
        );


        // 右边稍微向内旋转
        sequence.Join(
            rightTransform.DORotate(
                new Vector3(
                    0f,
                    0f,
                    15f
                ),
                mergeDuration,
                RotateMode.Fast
            )
            .SetEase(Ease.OutQuad)
        );


        // ==============================
        // 2. 汇合后一起缩小
        // ==============================

        sequence.Append(
            leftTransform.DOScale(
                Vector3.zero,
                disappearDuration
            )
            .SetEase(Ease.InBack)
        );


        sequence.Join(
            middleTransform.DOScale(
                Vector3.zero,
                disappearDuration
            )
            .SetEase(Ease.InBack)
        );


        sequence.Join(
            rightTransform.DOScale(
                Vector3.zero,
                disappearDuration
            )
            .SetEase(Ease.InBack)
        );


        // ==============================
        // 3. Destroy
        // ==============================

        sequence.OnComplete(() =>
        {
            Destroy(
                left.gameObject
            );

            Destroy(
                middle.gameObject
            );

            Destroy(
                right.gameObject
            );

            MatchCompleted?.Invoke();

            slotItems[leftIndex] = null;
            slotItems[middleIndex] = null;
            slotItems[rightIndex] = null;

            landed[leftIndex] = false;
            landed[middleIndex] = false;
            landed[rightIndex] = false;

            // 匹配位置继续保持 occupied，直到后续在途物体全部落稳并完成补位
            waitingForSettleAfterMatch = true;
            TryFinishMatchCleanup();
        });
    }


    private void TryFinishMatchCleanup()
    {
        if (!waitingForSettleAfterMatch)
        {
            return;
        }

        for (int i = 0; i < slotItems.Length; i++)
        {
            if (occupied[i] &&
                slotItems[i] != null &&
                !landed[i])
            {
                return;
            }
        }

        waitingForSettleAfterMatch = false;
        CompactSlots();
    }


    private void CompactSlots()
    {
        int writeIndex = 0;
        Sequence shiftSequence = null;

        for (int readIndex = 0;
             readIndex < slotItems.Length;
             readIndex++)
        {
            ClickItem item = slotItems[readIndex];

            if (item == null)
            {
                continue;
            }

            slotItems[writeIndex] = item;
            occupied[writeIndex] = true;
            landed[writeIndex] = true;

            if (writeIndex != readIndex)
            {
                Tween shift = item.MoveWithinSlots(
                    slots[writeIndex]
                );

                if (shift != null)
                {
                    shiftSequence ??= DOTween.Sequence();
                    shiftSequence.Join(shift);
                }
            }

            writeIndex++;
        }

        for (int i = writeIndex; i < slotItems.Length; i++)
        {
            slotItems[i] = null;
            occupied[i] = false;
            landed[i] = false;
        }

        if (shiftSequence != null)
        {
            shiftSequence.OnComplete(FinishMatchCycle);
            return;
        }

        FinishMatchCycle();
    }


    private void FinishMatchCycle()
    {
        isMatching = false;
        CheckForMatch();
    }


    // Slot 被物品压下去再回弹
    public Sequence PlaySlotBounce(
        RectTransform slot
    )
    {
        if (slot == null)
        {
            return null;
        }


        slot.DOKill();


        Vector2 startPos =
            slot.anchoredPosition;


        Sequence seq =
            DOTween.Sequence();


        // 向下压
        seq.Append(
            slot.DOAnchorPosY(
                startPos.y -
                pressPixels,

                pressDuration
            )
            .SetEase(
                Ease.OutQuad
            )
        );


        // 回弹
        seq.Append(
            slot.DOAnchorPosY(
                startPos.y,

                bounceDuration
            )
            .SetEase(
                Ease.OutBack,
                1.2f
            )
        );


        return seq;
    }
}
