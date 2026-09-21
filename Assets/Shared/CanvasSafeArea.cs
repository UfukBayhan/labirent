using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RectTransform'u işletim sisteminin bildirdiği Screen.safeArea sınırlarına kilitler.
/// Bu component tam ekran bir Canvas'ın altındaki içerik kökünde kullanılmalıdır;
/// tam ekran BACK/background Canvas'ına eklenmemelidir.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CanvasSafeArea : MonoBehaviour
{
    private sealed class FullBleedGraphicInfo
    {
        public RectTransform RectTransform;
        public Graphic Graphic;
        public Vector2 OriginalOffsetMin;
        public Vector2 OriginalOffsetMax;
        public AspectRatioFitter AspectRatioFitter;
        public float AspectRatio;
        public bool UseManualAspectEnvelope;
    }

    private RectTransform targetRect;
    private Rect lastSafeArea = new Rect(float.MinValue, float.MinValue, 0f, 0f);
    private Vector2Int lastScreenSize = new Vector2Int(-1, -1);
    private ScreenOrientation lastOrientation;
    private bool preserveDirectChildGraphicsFullBleed;
    private readonly List<FullBleedGraphicInfo> fullBleedGraphics = new();

    protected virtual void Awake()
    {
        targetRect = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    protected virtual void OnEnable()
    {
        if (targetRect == null)
            targetRect = GetComponent<RectTransform>();

        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        Canvas.willRenderCanvases += HandleWillRenderCanvases;
        ApplySafeArea();
    }

    protected virtual void OnDisable()
    {
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
    }

    protected virtual void Update()
    {
        Rect currentSafeArea = Screen.safeArea;
        Vector2Int currentScreenSize = new Vector2Int(Screen.width, Screen.height);
        ScreenOrientation currentOrientation = Screen.orientation;

        if (currentSafeArea != lastSafeArea ||
            currentScreenSize != lastScreenSize ||
            currentOrientation != lastOrientation)
        {
            ApplySafeArea();
        }
    }

    /// <summary>
    /// MiniGameManager soru köklerinde doğrudan bağlı, tam-stretch UI görsellerini
    /// full-screen tutar. Hiyerarşiyi değiştirmez. Normal görselleri safe area'nın
    /// ters yönünde genişletir; EnvelopeParent kullanan görselleri tam Canvas'a
    /// oranı korunmuş bir "cover" dikdörtgeni olarak yerleştirir.
    /// </summary>
    public void EnableDirectChildGraphicsFullBleed()
    {
        preserveDirectChildGraphicsFullBleed = true;
        CaptureDirectFullBleedGraphics();
        ApplySafeArea();
    }

    private void HandleWillRenderCanvases()
    {
        if (!isActiveAndEnabled || !preserveDirectChildGraphicsFullBleed)
            return;

        // AspectRatioFitter ve Unity layout sistemi RectTransform'u Awake'ten sonra
        // tekrar yazabilir. Son ölçüyü UI çizilmeden hemen önce garanti ederiz.
        ApplyDirectFullBleedGraphics();
    }

    private void ApplySafeArea()
    {
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        if (targetRect == null || screenWidth <= 0f || screenHeight <= 0f)
            return;

        Rect rawSafeArea = Screen.safeArea;

        lastSafeArea = rawSafeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastOrientation = Screen.orientation;

        // Screen.safeArea mevcut yön için dört güvenli sınırı zaten içerir.
        // LandscapeLeft/Right için yeniden taraf seçmek içerik merkezini kaydırır.
        targetRect.anchorMin = new Vector2(
            rawSafeArea.xMin / screenWidth,
            rawSafeArea.yMin / screenHeight
        );
        targetRect.anchorMax = new Vector2(
            rawSafeArea.xMax / screenWidth,
            rawSafeArea.yMax / screenHeight
        );
        targetRect.offsetMin = Vector2.zero;
        targetRect.offsetMax = Vector2.zero;

        ApplyDirectFullBleedGraphics();
    }

    private void CaptureDirectFullBleedGraphics()
    {
        fullBleedGraphics.Clear();

        if (!preserveDirectChildGraphicsFullBleed || targetRect == null)
            return;

        for (int i = 0; i < targetRect.childCount; i++)
        {
            RectTransform child = targetRect.GetChild(i) as RectTransform;
            if (child == null)
                continue;

            Graphic graphic = child.GetComponent<Graphic>();
            if (graphic == null)
                continue;

            bool isFullStretch =
                Approximately(child.anchorMin, Vector2.zero) &&
                Approximately(child.anchorMax, Vector2.one);
            if (!isFullStretch)
                continue;

            AspectRatioFitter aspectRatioFitter = child.GetComponent<AspectRatioFitter>();
            bool useManualAspectEnvelope =
                aspectRatioFitter != null &&
                aspectRatioFitter.enabled &&
                aspectRatioFitter.aspectMode == AspectRatioFitter.AspectMode.EnvelopeParent &&
                aspectRatioFitter.aspectRatio > 0f;

            // AspectRatioFitter parent olarak safe-area RectTransform'unu görür ve
            // bizim full-bleed ölçümüzü layout aşamasında tekrar küçültür. Arka plan
            // için aynı EnvelopeParent hesabını aşağıda tam Canvas ölçüsüne göre yaparız.
            if (useManualAspectEnvelope)
                aspectRatioFitter.enabled = false;

            // Full-bleed arka plan bir etkileşim hedefi değildir ve üst hiyerarşide
            // sonradan eklenen bir Mask/RectMask2D varsa ondan kırpılmamalıdır.
            graphic.raycastTarget = false;
            if (graphic is MaskableGraphic maskableGraphic)
                maskableGraphic.maskable = false;

            fullBleedGraphics.Add(new FullBleedGraphicInfo
            {
                RectTransform = child,
                Graphic = graphic,
                OriginalOffsetMin = child.offsetMin,
                OriginalOffsetMax = child.offsetMax,
                AspectRatioFitter = aspectRatioFitter,
                AspectRatio = useManualAspectEnvelope ? aspectRatioFitter.aspectRatio : 0f,
                UseManualAspectEnvelope = useManualAspectEnvelope
            });
        }
    }

    private void ApplyDirectFullBleedGraphics()
    {
        if (!preserveDirectChildGraphicsFullBleed || fullBleedGraphics.Count == 0)
            return;

        RectTransform parentRect = targetRect.parent as RectTransform;
        if (parentRect == null)
            return;

        Vector2 parentSize = parentRect.rect.size;
        Vector2 minExtension = Vector2.Scale(parentSize, targetRect.anchorMin);
        Vector2 maxExtension = Vector2.Scale(parentSize, Vector2.one - targetRect.anchorMax);

        for (int i = 0; i < fullBleedGraphics.Count; i++)
        {
            FullBleedGraphicInfo info = fullBleedGraphics[i];
            RectTransform child = info.RectTransform;
            if (child == null)
                continue;

            if (info.UseManualAspectEnvelope)
            {
                ApplyManualAspectEnvelope(info, parentRect);
                continue;
            }

            child.offsetMin = info.OriginalOffsetMin - minExtension;
            child.offsetMax = info.OriginalOffsetMax + maxExtension;
        }
    }

    private void ApplyManualAspectEnvelope(
        FullBleedGraphicInfo info,
        RectTransform fullScreenParent
    )
    {
        RectTransform child = info.RectTransform;
        if (child == null || info.AspectRatio <= 0f)
            return;

        // AspectRatioFitter başka bir lifecycle çağrısıyla tekrar açılırsa aynı
        // frame'in layout aşamasında RectTransform'u yeniden küçültmesini engelle.
        if (info.AspectRatioFitter != null && info.AspectRatioFitter.enabled)
            info.AspectRatioFitter.enabled = false;

        Vector3[] parentCorners = new Vector3[4];
        fullScreenParent.GetWorldCorners(parentCorners);

        Vector2 bottomLeft = targetRect.InverseTransformPoint(parentCorners[0]);
        Vector2 topRight = targetRect.InverseTransformPoint(parentCorners[2]);
        Vector2 fullScreenSize = new Vector2(
            Mathf.Abs(topRight.x - bottomLeft.x),
            Mathf.Abs(topRight.y - bottomLeft.y)
        );
        Vector2 fullScreenCenter = (bottomLeft + topRight) * 0.5f;

        if (fullScreenSize.x <= 0f || fullScreenSize.y <= 0f)
            return;

        float fullScreenAspect = fullScreenSize.x / fullScreenSize.y;
        Vector2 coverSize;

        if (fullScreenAspect >= info.AspectRatio)
        {
            coverSize = new Vector2(
                fullScreenSize.x,
                fullScreenSize.x / info.AspectRatio
            );
        }
        else
        {
            coverSize = new Vector2(
                fullScreenSize.y * info.AspectRatio,
                fullScreenSize.y
            );
        }

        float localZ = child.localPosition.z;
        child.anchorMin = new Vector2(0.5f, 0.5f);
        child.anchorMax = new Vector2(0.5f, 0.5f);
        child.pivot = new Vector2(0.5f, 0.5f);
        child.sizeDelta = coverSize;
        child.localPosition = new Vector3(fullScreenCenter.x, fullScreenCenter.y, localZ);
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
    }
}
