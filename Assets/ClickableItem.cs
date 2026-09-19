using UnityEngine;
using DG.Tweening;

public enum ItemType
{
    Baolingqiuping = 0,
    Banqiupai = 1,
    Baolingqiu = 2,
    Banqiu = 3,
    Bangqiu = 4
}

[RequireComponent(typeof(Rigidbody))]
public class ClickItem : MonoBehaviour
{
    [Header("Item")]
    [SerializeField] private ItemType itemType;

    public ItemType Type => itemType;

    [SerializeField] private SlotManager slotManager;

    [Header("Move To Slot")]
    [SerializeField] private float moveDuration = 0.25f;
    [SerializeField] private float jumpPower = 0.8f;

    [Header("Landing Visual")]
    [SerializeField] private Vector3 targetRotation = Vector3.zero;
    [SerializeField] private Vector3 targetScale =
        new Vector3(100f, 100f, 100f);

    [Header("Landing")]
    [SerializeField] private float slotLandingOffsetY = 0f;
    [SerializeField] private float slotLandingOffsetZ = -0.2f;

    private Rigidbody rb;
    private Collider col;
    private Camera mainCamera;

    private bool isMoving = false;
    private bool hasBeenSelected = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponentInChildren<Collider>();
        mainCamera = Camera.main;

        if (slotManager == null)
        {
            slotManager =
                FindFirstObjectByType<SlotManager>();
        }
    }

    public void OnClicked()
    {
        if (isMoving || hasBeenSelected)
            return;

        if (slotManager == null)
        {
            Debug.LogError("SlotManager not found.");
            return;
        }

        // 点击时立即预占，避免快速点击让多个物体拿到同一个 Slot
        if (!slotManager.TryReserveSlot(
                this,
                out int slotIndex
            ))
        {
            Debug.Log("No empty slot.");
            return;
        }

        // 根据 index 获取真正的 UI Slot
        RectTransform targetSlot =
            slotManager.GetSlot(slotIndex);

        if (targetSlot == null)
            return;

        hasBeenSelected = true;

        MoveToSlot(
            targetSlot,
            slotIndex
        );
    }

    private void MoveToSlot(
        RectTransform targetSlot,
        int slotIndex
    )
    {
        isMoving = true;

        // 点击以后脱离物理
        rb.isKinematic = true;

        if (col != null)
        {
            col.enabled = false;
        }

        Vector3 targetWorldPos =
            GetSlotTargetWorldPosition(targetSlot);

        Sequence sequence =
            DOTween.Sequence();

        // 1. 飞到 Slot
        sequence.Append(
            transform.DOJump(
                targetWorldPos,
                jumpPower,
                1,
                moveDuration
            )
            .SetEase(Ease.Linear)
        );

        // 2. 飞行过程中缩放
        sequence.Join(
            transform.DOScale(
                targetScale,
                moveDuration
            )
            .SetEase(Ease.OutQuad)
        );

        // 3. 飞行过程中转正
        sequence.Join(
            transform.DORotate(
                targetRotation,
                moveDuration,
                RotateMode.Fast
            )
            .SetEase(Ease.OutQuad)
        );

        // 保证落地时位置和姿态准确
        sequence.AppendCallback(() =>
        {
            transform.position =
                targetWorldPos;

            transform.localScale =
                targetScale;

            transform.eulerAngles =
                targetRotation;
        });

        // 4. Slot 下压 + 回弹
        Sequence bounce =
            slotManager.PlaySlotBounce(
                targetSlot
            );

        if (bounce != null)
        {
            FollowSlotBounce(
                targetSlot,
                bounce,
                targetWorldPos
            );

            sequence.Append(bounce);
        }

        // 5. 全部动画完成
        sequence.OnComplete(() =>
        {
            transform.position =
                targetWorldPos;

            transform.localScale =
                targetScale;

            transform.eulerAngles =
                targetRotation;

            isMoving = false;

            // 告诉 SlotManager：
            // 这个物品正式进入了哪个 Slot
            slotManager.NotifyItemLanded(
                slotIndex,
                this
            );
        });
    }


    public Tween MoveWithinSlots(
        RectTransform targetSlot
    )
    {
        if (targetSlot == null)
        {
            return null;
        }

        Vector3 targetWorldPos =
            GetSlotTargetWorldPosition(targetSlot);

        transform.DOKill();

        return transform.DOMove(
            targetWorldPos,
            0.18f
        )
        .SetEase(Ease.OutQuad)
        .OnComplete(() =>
        {
            transform.position = targetWorldPos;
        });
    }


    private Vector3 GetSlotTargetWorldPosition(
        RectTransform targetSlot
    )
    {
        Vector3 targetWorldPos =
            GetWorldPositionFromUISlot(targetSlot);

        targetWorldPos.y +=
            GetBottomOffset() +
            slotLandingOffsetY;

        targetWorldPos.z +=
            slotLandingOffsetZ;

        return targetWorldPos;
    }

    private float GetBottomOffset()
    {
        MeshFilter meshFilter =
            GetComponentInChildren<MeshFilter>();

        if (meshFilter == null ||
            meshFilter.sharedMesh == null)
        {
            Debug.LogWarning(
                "MeshFilter or Mesh missing on: "
                + gameObject.name
            );

            return 0f;
        }

        Bounds meshBounds =
            meshFilter.sharedMesh.bounds;

        // Mesh 本地空间中：
        // 从 Pivot 到模型最底部的距离
        float localPivotToBottom =
            -meshBounds.min.y;

        // 使用落地后的最终 Scale
        float scaledPivotToBottom =
            localPivotToBottom *
            Mathf.Abs(targetScale.y);

        return scaledPivotToBottom;
    }

    private Vector3 GetWorldPositionFromUISlot(
        RectTransform slot
    )
    {
        Canvas canvas =
            slot.GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            Debug.LogError(
                "Slot has no Canvas parent."
            );

            return transform.position;
        }

        Camera uiCamera =
            canvas.worldCamera;

        // UI Slot -> Screen Position
        Vector2 screenPos =
            RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                slot.position
            );

        // 保持当前物体的世界深度
        float depth =
            Mathf.Abs(
                transform.position.z -
                mainCamera.transform.position.z
            );

        // Screen Position -> 3D World Position
        Vector3 worldPos =
            mainCamera.ScreenToWorldPoint(
                new Vector3(
                    screenPos.x,
                    screenPos.y,
                    depth
                )
            );

        return worldPos;
    }

    private void FollowSlotBounce(
        RectTransform slot,
        Sequence bounce,
        Vector3 landingWorldPos
    )
    {
        Canvas canvas =
            slot.GetComponentInParent<Canvas>();

        Camera uiCamera =
            canvas != null
                ? canvas.worldCamera
                : null;

        Vector2 slotStartScreen =
            RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                slot.position
            );

        float depth =
            Mathf.Abs(
                landingWorldPos.z -
                mainCamera.transform.position.z
            );

        bounce.OnUpdate(() =>
        {
            Vector2 currentScreen =
                RectTransformUtility.WorldToScreenPoint(
                    uiCamera,
                    slot.position
                );

            Vector3 worldStart =
                mainCamera.ScreenToWorldPoint(
                    new Vector3(
                        slotStartScreen.x,
                        slotStartScreen.y,
                        depth
                    )
                );

            Vector3 worldCurrent =
                mainCamera.ScreenToWorldPoint(
                    new Vector3(
                        currentScreen.x,
                        currentScreen.y,
                        depth
                    )
                );

            Vector3 worldDelta =
                worldCurrent -
                worldStart;

            transform.position =
                landingWorldPos +
                worldDelta;
        });

        bounce.OnComplete(() =>
        {
            transform.position =
                landingWorldPos;
        });
    }
}
