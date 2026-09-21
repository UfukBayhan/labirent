using UnityEngine;

// Oyuncunun arkasında sönen bir iz bırakır.
// Player objesine eklenir. Level başında ışınlanma çizgisi oluşmaması için
// maze yeniden üretildiğinde izi temizler.
public class PlayerTrail : MonoBehaviour
{
    [Header("Referans (boşsa otomatik bulunur)")]
    public MazeGenerator mazeGenerator;

    [Header("İz Ayarları")]
    public Color trailColor = new Color(1f, 1f, 1f, 0.85f);

    [Tooltip("İzin yaşam süresi (sn). Büyük = uzun kuyruk.")]
    public float trailTime = 0.35f;

    [Tooltip("İz başlangıç genişliği (birim). Player ~0.8 ise 0.5-0.6 iyi.")]
    public float startWidth = 0.55f;
    public float endWidth = 0f;

    [Tooltip("Player SpriteRenderer'ı 10'daysa 9 = tam altında.")]
    public int sortingOrder = 9;

    [Tooltip(
        "Boş bırakırsan Sprites/Default kullanılır. Daha güçlü parlama için additive bir materyal ver."
    )]
    public Material trailMaterial;

    private TrailRenderer tr;
    private bool clearPending;

    [System.Obsolete]
    private void Awake()
    {
        if (!mazeGenerator)
            mazeGenerator = FindObjectOfType<MazeGenerator>();

        tr = GetComponent<TrailRenderer>();
        if (tr == null)
            tr = gameObject.AddComponent<TrailRenderer>();

        tr.time = trailTime;
        tr.startWidth = startWidth;
        tr.endWidth = endWidth;
        tr.numCapVertices = 8;
        tr.numCornerVertices = 4;
        tr.minVertexDistance = 0.02f;
        tr.autodestruct = false;
        tr.emitting = true;
        tr.alignment = LineAlignment.View;

        // Renk geçişi: dolu -> şeffaf
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(trailColor, 0f),
                new GradientColorKey(trailColor, 1f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(trailColor.a, 0f),
                new GradientAlphaKey(0f, 1f),
            }
        );
        tr.colorGradient = grad;

        if (trailMaterial != null)
            tr.material = trailMaterial;
        else
            tr.material = new Material(Shader.Find("Sprites/Default"));

        tr.sortingOrder = sortingOrder;
    }

    private void OnEnable()
    {
        if (mazeGenerator != null)
            mazeGenerator.OnMazeGenerated += OnMazeGenerated;
    }

    private void OnDisable()
    {
        if (mazeGenerator != null)
            mazeGenerator.OnMazeGenerated -= OnMazeGenerated;
    }

    private void OnMazeGenerated()
    {
        // Player bu event'te başa ışınlanıyor; izi bir sonraki LateUpdate'te
        // (konum oturduktan sonra) temizle ki çizgi oluşmasın.
        clearPending = true;
    }

    private void LateUpdate()
    {
        if (clearPending && tr != null)
        {
            tr.Clear();
            clearPending = false;
        }
    }
}
