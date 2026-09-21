using UnityEngine;

// Çıkış portalı görseli: yumuşak bir glow + nabız (pulse) animasyonu.
// ExitFromToMaze.exitMarker olarak atanan objeye eklenir; konumu her level'de
// Manager tarafından bitiş hücresine taşınır, bu script sadece görseli yönetir.
[RequireComponent(typeof(SpriteRenderer))]
public class Portal : MonoBehaviour
{
    [Header("Renk / Görsel")]
    public Color glowColor = new Color(0.45f, 1f, 0.85f, 1f);

    [Tooltip("Boşsa otomatik yumuşak glow üretilir. İstersen kendi swirl/portal sprite'ını ver.")]
    public Sprite glowSprite;

    [Tooltip("Portalın temel boyutu (Unity birimi). 1 tile = 1 birim.")]
    public float baseSize = 0.95f;

    [Header("Animasyon")]
    public float pulseMin = 0.85f; // baseSize çarpanı
    public float pulseMax = 1.12f;
    public float pulseSpeed = 2.2f;

    [Tooltip("Sadece simetrik olmayan (swirl) sprite'larda görünür. Glow için 0 bırak.")]
    public float rotateSpeed = 0f; // derece/sn

    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (glowSprite == null)
            glowSprite = GenerateGlowSprite();
        if (sr.sprite == null)
            sr.sprite = glowSprite;
        sr.color = glowColor;
    }

    private void Update()
    {
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f; // 0..1
        float mult = Mathf.Lerp(pulseMin, pulseMax, t);
        float s = baseSize * mult;
        transform.localScale = new Vector3(s, s, 1f);

        if (rotateSpeed != 0f)
            transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);
    }

    // Parlak çekirdekli, kenara doğru sönen yumuşak glow
    private Sprite GenerateGlowSprite()
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f;
        Vector2 c = new Vector2(r, r);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / r;
                float a = Mathf.Clamp01(1f - dist);
                a = a * a; // parlak çekirdek, yumuşak kenar
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
