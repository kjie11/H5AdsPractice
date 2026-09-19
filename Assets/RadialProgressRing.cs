using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class RadialProgressRing : MaskableGraphic
{
    [Header("Ring")]
    [SerializeField] private Color trackColor =
        new Color32(24, 36, 46, 230);
    [SerializeField] private Color fillColor =
        new Color32(86, 235, 67, 255);
    [SerializeField, Min(1f)] private float thickness = 7f;
    [SerializeField, Range(12, 128)] private int segments = 64;

    [Header("Match Progress")]
    [SerializeField, Min(1)] private int matchesToFill = 5;
    [SerializeField, Min(0f)] private float animationDuration = 0.25f;
    [SerializeField, Range(0f, 1f)] private float progress;

    [Header("Full Reward")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField, Min(0.05f)] private float launchDuration = 0.6f;
    [SerializeField, Range(0f, 15f)] private float launchAngleVariance = 5f;
    [SerializeField] private float projectileWorldZ;
    [SerializeField] private Collider[] boundaryColliders;

    [Header("Preview Ball")]
    [SerializeField] private Transform previewBall;
    [SerializeField, Min(0f)] private float previewSpinSpeed = 90f;
    [SerializeField, Min(0f)] private float previewSpinBeforeLaunchDuration = 0.6f;

    private SlotManager slotManager;
    private Tween progressTween;
    private Collider activeProjectileCollider;
    private int completedMatches;
    private bool projectileLaunched;
    private Vector3 previewBaseEulerAngles;
    private float previewSpinAngle;
    private bool previewRotationReady;
    private bool previewSpinning;
    private Coroutine previewSpinCoroutine;

    public float Progress => progress;


    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }


    protected override void OnEnable()
    {
        base.OnEnable();

        slotManager = FindFirstObjectByType<SlotManager>();
        PreparePreviewBall();

        if (slotManager != null)
        {
            slotManager.MatchCompleted += OnMatchCompleted;
        }

        SetVerticesDirty();
    }


    private void Update()
    {
        if (previewBall == null ||
            !previewRotationReady ||
            !previewSpinning)
        {
            return;
        }

        previewSpinAngle = Mathf.Repeat(
            previewSpinAngle + previewSpinSpeed * Time.deltaTime,
            360f
        );

        previewBall.localEulerAngles = new Vector3(
            previewBaseEulerAngles.x,
            Mathf.Repeat(
                previewBaseEulerAngles.y + previewSpinAngle,
                360f
            ),
            previewBaseEulerAngles.z
        );
    }


    private void PreparePreviewBall()
    {
        if (previewBall == null)
        {
            GameObject previewObject = GameObject.Find("shandianqiu");

            if (previewObject != null)
            {
                previewBall = previewObject.transform;
            }
        }

        if (previewBall == null)
        {
            previewRotationReady = false;
            Debug.LogWarning(
                "Assign the scene shandianqiu model to Preview Ball."
            );
            return;
        }

        previewBaseEulerAngles = previewBall.localEulerAngles;
        previewSpinAngle = 0f;
        previewRotationReady = true;

        Rigidbody previewBody = previewBall.GetComponent<Rigidbody>();
        if (previewBody != null)
        {
            previewBody.linearVelocity = Vector3.zero;
            previewBody.angularVelocity = Vector3.zero;
            previewBody.isKinematic = true;
        }

        Collider previewCollider = previewBall.GetComponent<Collider>();
        if (previewCollider != null)
        {
            previewCollider.enabled = false;
        }

        DepthGravity previewGravity = previewBall.GetComponent<DepthGravity>();
        if (previewGravity != null)
        {
            previewGravity.enabled = false;
        }
    }


    protected override void OnDisable()
    {
        if (slotManager != null)
        {
            slotManager.MatchCompleted -= OnMatchCompleted;
        }

        progressTween?.Kill();
        progressTween = null;

        if (previewSpinCoroutine != null)
        {
            StopCoroutine(previewSpinCoroutine);
            previewSpinCoroutine = null;
        }

        previewSpinning = false;

        if (activeProjectileCollider != null)
        {
            activeProjectileCollider.isTrigger = false;
            activeProjectileCollider = null;
        }

        projectileLaunched = false;
        RestoreBoundaryColliders();

        base.OnDisable();
    }


    private void OnMatchCompleted()
    {
        if (completedMatches >= matchesToFill)
        {
            return;
        }

        completedMatches++;

        float targetProgress =
            (float)completedMatches /
            Mathf.Max(1, matchesToFill);

        progressTween?.Kill();

        progressTween = DOTween.To(
            () => progress,
            SetProgress,
            targetProgress,
            animationDuration
        )
        .SetEase(Ease.OutQuad)
        .SetTarget(this)
        .OnComplete(BeginPreviewSpin);
    }


    private void BeginPreviewSpin()
    {
        if (projectileLaunched ||
            completedMatches < matchesToFill ||
            previewSpinCoroutine != null)
        {
            return;
        }

        if (previewBall == null ||
            !previewRotationReady ||
            previewSpinBeforeLaunchDuration <= 0f)
        {
            TryLaunchProjectile();
            return;
        }

        previewSpinCoroutine = StartCoroutine(
            SpinPreviewThenLaunch()
        );
    }


    private IEnumerator SpinPreviewThenLaunch()
    {
        previewSpinning = true;

        yield return new WaitForSeconds(
            previewSpinBeforeLaunchDuration
        );

        previewSpinning = false;
        previewSpinCoroutine = null;
        TryLaunchProjectile();
    }


    private void TryLaunchProjectile()
    {
        if (projectileLaunched ||
            completedMatches < matchesToFill ||
            projectilePrefab == null)
        {
            return;
        }

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            return;
        }

        Camera uiCamera = canvas != null &&
                          canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 startScreen =
            RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                rectTransform.position
            );

        float depth = Mathf.Abs(
            projectileWorldZ - worldCamera.transform.position.z
        );

        Vector3 startPosition = worldCamera.ScreenToWorldPoint(
            new Vector3(startScreen.x, startScreen.y, depth)
        );

        Vector3 centerPosition = worldCamera.ScreenToWorldPoint(
            new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, depth)
        );

        Vector3 launchDirection =
            (centerPosition - startPosition).normalized;

        launchDirection = Quaternion.AngleAxis(
            Random.Range(-launchAngleVariance, launchAngleVariance),
            worldCamera.transform.forward
        ) * launchDirection;

        float distanceToCenter =
            Vector3.Distance(startPosition, centerPosition);

        centerPosition = startPosition +
                         launchDirection * distanceToCenter;

        DisableBoundaryColliders();

        GameObject projectile = Instantiate(
            projectilePrefab,
            startPosition,
            Quaternion.identity
        );

        Rigidbody body = projectile.GetComponent<Rigidbody>();
        Collider projectileCollider = projectile.GetComponent<Collider>();

        if (body == null || projectileCollider == null)
        {
            Debug.LogError(
                "Lightning ball prefab needs a Rigidbody and Collider."
            );
            Destroy(projectile);
            RestoreBoundaryColliders();
            return;
        }

        projectileLaunched = true;
        completedMatches = 0;
        SetProgress(0f);
        body.isKinematic = false;
        projectileCollider.isTrigger = true;
        activeProjectileCollider = projectileCollider;

        body.linearVelocity = launchDirection *
                              (distanceToCenter / launchDuration);

        StartCoroutine(WaitForCenterCrossing(
            projectile.transform,
            projectileCollider,
            centerPosition,
            launchDirection
        ));
    }


    private IEnumerator WaitForCenterCrossing(
        Transform projectile,
        Collider projectileCollider,
        Vector3 centerPosition,
        Vector3 launchDirection
    )
    {
        WaitForFixedUpdate waitForPhysics = new WaitForFixedUpdate();

        while (projectile != null &&
               Vector3.Dot(
                   projectile.position - centerPosition,
                   launchDirection
               ) < 0f)
        {
            yield return waitForPhysics;
        }

        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = false;
        }

        activeProjectileCollider = null;
        projectileLaunched = false;
        RestoreBoundaryColliders();

        if (completedMatches >= matchesToFill)
        {
            BeginPreviewSpin();
        }
    }


    private void DisableBoundaryColliders()
    {
        if (boundaryColliders == null)
        {
            return;
        }

        for (int i = 0; i < boundaryColliders.Length; i++)
        {
            Collider boundary = boundaryColliders[i];
            if (boundary == null)
            {
                continue;
            }

            boundary.enabled = false;
        }
    }


    private void RestoreBoundaryColliders()
    {
        if (boundaryColliders == null)
        {
            return;
        }

        for (int i = 0; i < boundaryColliders.Length; i++)
        {
            if (boundaryColliders[i] != null)
            {
                boundaryColliders[i].enabled = true;
            }
        }
    }


    public void SetProgress(float value)
    {
        float clampedValue = Mathf.Clamp01(value);

        if (Mathf.Approximately(progress, clampedValue))
        {
            return;
        }

        progress = clampedValue;
        SetVerticesDirty();
    }


    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float outerRadius =
            Mathf.Min(rect.width, rect.height) * 0.5f;
        float innerRadius =
            Mathf.Max(0f, outerRadius - thickness);

        AddArc(
            vertexHelper,
            center,
            innerRadius,
            outerRadius,
            1f,
            trackColor
        );

        if (progress > 0f)
        {
            AddArc(
                vertexHelper,
                center,
                innerRadius,
                outerRadius,
                progress,
                fillColor
            );
        }
    }


    private void AddArc(
        VertexHelper vertexHelper,
        Vector2 center,
        float innerRadius,
        float outerRadius,
        float amount,
        Color32 arcColor
    )
    {
        int arcSegments = Mathf.Max(
            1,
            Mathf.CeilToInt(segments * amount)
        );

        int startVertex = vertexHelper.currentVertCount;

        for (int i = 0; i <= arcSegments; i++)
        {
            float normalized =
                amount * i / arcSegments;
            float angle =
                (90f - 360f * normalized) *
                Mathf.Deg2Rad;
            Vector2 direction = new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            );

            AddVertex(
                vertexHelper,
                center + direction * outerRadius,
                arcColor
            );

            AddVertex(
                vertexHelper,
                center + direction * innerRadius,
                arcColor
            );
        }

        for (int i = 0; i < arcSegments; i++)
        {
            int index = startVertex + i * 2;

            vertexHelper.AddTriangle(
                index,
                index + 2,
                index + 1
            );

            vertexHelper.AddTriangle(
                index + 2,
                index + 3,
                index + 1
            );
        }
    }


    private static void AddVertex(
        VertexHelper vertexHelper,
        Vector2 position,
        Color32 vertexColor
    )
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = vertexColor;
        vertexHelper.AddVert(vertex);
    }


#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        matchesToFill = Mathf.Max(1, matchesToFill);
        segments = Mathf.Clamp(segments, 12, 128);
        thickness = Mathf.Max(1f, thickness);
        progress = Mathf.Clamp01(progress);
        SetVerticesDirty();
    }
#endif
}
