using UnityEngine;

public class ExitFromToMaze : MonoBehaviour
{
    [Header("Maze Generator")]
    [SerializeField]
    private MazeGenerator mazeGenerator;

    [Header("Game Manager")]
    [SerializeField]
    private LabirentGameManager gameManager;

    [Header("Player (bossa otomatik bulunur)")]
    [SerializeField]
    private PlayerController player;

    [Header("Seviye Bilgisi")]
    [Tooltip("GameManager tarafindan set edilir. 0'dan baslar.")]
    public int levelIndex = 0;

    [Header("Maze Frame")]
    public SpriteRenderer mazeFrame;
    public float frameMarginX = 0f;
    public float frameMarginY = 0f;

    [Header("Cikis / Baslangic Isaretleri")]
    public Transform exitMarker;
    public Transform startMarker;

    [Header("Gorsel Tema Ayarlari")]
    public MazeTheme[] themes;

    [Header("Kamera & Safe Area")]
    public Camera mainCamera;

    [SerializeField]
    [Tooltip("Labirentin etrafinda birakilan bosluk (dunya birimi).")]
    private float cameraPadding = 0.75f;

    [Tooltip("iPhone centik ve Android gesture bar gibi safe area sinirlari dikkate alinsin mi?")]
    public bool applySafeArea = true;

    [Tooltip("Labirenti ekran kenarlarindan uzak tutan ekstra viewport boslugu.")]
    public Vector2 viewportPadding = new Vector2(0.04f, 0.06f);

    [Tooltip("Ust UI (geri butonu, skor) icin ayrilan viewport yuksekligi.")]
    [Range(0f, 0.3f)]
    public float topHudReserve = 0.1f;

    [Tooltip("Alt UI/logo icin ayrilan viewport yuksekligi.")]
    [Range(0f, 0.3f)]
    public float bottomHudReserve = 0.08f;

    [Tooltip("Tema yoksa kameranin kullanacagi arka plan rengi.")]
    public Color borderColor = new Color(0.1f, 0.28f, 0.18f, 1f);

    private static readonly MazeTheme[] DefaultThemes =
    {
        new MazeTheme
        {
            wallColor = new Color(0.07f, 0.30f, 0.19f, 1f),
            floorColor = new Color(0.28f, 0.78f, 0.52f, 1f),
            mazeFrameColor = new Color(0.05f, 0.24f, 0.15f, 1f),
            backgroundColor = new Color(0.28f, 0.78f, 0.52f, 1f),
        },
        new MazeTheme
        {
            wallColor = new Color(0.43f, 0.02f, 0.07f, 1f),
            floorColor = new Color(1.00f, 0.24f, 0.33f, 1f),
            mazeFrameColor = new Color(0.32f, 0.01f, 0.04f, 1f),
            backgroundColor = new Color(1.00f, 0.24f, 0.33f, 1f),
        },
        new MazeTheme
        {
            wallColor = new Color(0.02f, 0.18f, 0.34f, 1f),
            floorColor = new Color(0.26f, 0.70f, 1.00f, 1f),
            mazeFrameColor = new Color(0.01f, 0.13f, 0.25f, 1f),
            backgroundColor = new Color(0.26f, 0.70f, 1.00f, 1f),
        },
        new MazeTheme
        {
            wallColor = new Color(0.40f, 0.20f, 0.03f, 1f),
            floorColor = new Color(1.00f, 0.70f, 0.22f, 1f),
            mazeFrameColor = new Color(0.30f, 0.14f, 0.02f, 1f),
            backgroundColor = new Color(1.00f, 0.70f, 0.22f, 1f),
        },
        new MazeTheme
        {
            wallColor = new Color(0.18f, 0.05f, 0.32f, 1f),
            floorColor = new Color(0.72f, 0.44f, 1.00f, 1f),
            mazeFrameColor = new Color(0.12f, 0.03f, 0.24f, 1f),
            backgroundColor = new Color(0.72f, 0.44f, 1.00f, 1f),
        },
    };

    private Rect lastSafeArea = Rect.zero;
    private Vector2Int lastScreen = Vector2Int.zero;
    private int currentLogicalWidth;
    private int currentLogicalHeight;

    [System.Obsolete]
    private void Awake()
    {
        if (!mazeGenerator)
            mazeGenerator = FindObjectOfType<MazeGenerator>();

        if (!mazeGenerator)
            Debug.LogError("ExitFromToMaze: MazeGenerator sahnede bulunamadi!");

        if (!gameManager)
            gameManager = FindObjectOfType<LabirentGameManager>();

        if (!player)
            player = FindObjectOfType<PlayerController>();

        if (!mainCamera)
            mainCamera = Camera.main;

        if (mainCamera)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = borderColor;
            mainCamera.rect = new Rect(0f, 0f, 1f, 1f);
        }
    }

    private void Update()
    {
        if (!mainCamera || currentLogicalWidth <= 0 || currentLogicalHeight <= 0)
            return;

        var curScreen = new Vector2Int(Screen.width, Screen.height);
        if (Screen.safeArea == lastSafeArea && curScreen == lastScreen)
            return;

        FitCameraToSafeArea(currentLogicalWidth, currentLogicalHeight);
    }

    public void BuildCurrentLevel()
    {
        if (gameManager)
        {
            gameManager.BuildCurrentLevel();
            return;
        }

        if (!mazeGenerator)
            return;

        BuildLevel(
            new LabirentLevelConfig
            {
                width = 16,
                height = 9,
                seed = 0,
                endpointPattern = MazeEndpointPattern.BottomLeftToTopRight,
                themeIndex = levelIndex,
            },
            levelIndex
        );
    }

    public void BuildLevel(LabirentLevelConfig levelConfig, int newLevelIndex)
    {
        if (!mazeGenerator || levelConfig == null)
            return;

        levelIndex = Mathf.Max(0, newLevelIndex);
        ApplyTheme(levelConfig.themeIndex >= 0 ? levelConfig.themeIndex : levelIndex);

        int w = Mathf.Max(2, levelConfig.width);
        int h = Mathf.Max(2, levelConfig.height);

        currentLogicalWidth = w;
        currentLogicalHeight = h;

        mazeGenerator.InitAndGenerate(
            w,
            h,
            levelConfig.seed,
            levelConfig.endpointPattern,
            levelConfig.extraOpeningChance
        );

        int gridW = w * 2 + 1;
        int gridH = h * 2 + 1;

        if (mazeFrame)
        {
            mazeFrame.transform.position = new Vector3(gridW * 0.5f, gridH * 0.5f, 0f);
            mazeFrame.size = new Vector2(gridW + frameMarginX, gridH + frameMarginY);
        }

        if (exitMarker)
            exitMarker.position = mazeGenerator.CellToWorld(mazeGenerator.EndCell);

        if (startMarker)
            startMarker.position = mazeGenerator.CellToWorld(mazeGenerator.StartCell);

        FitCameraToSafeArea(w, h);
    }

    private void ApplyTheme(int themeIndex)
    {
        MazeTheme theme = GetTheme(themeIndex);

        if (mazeGenerator.floorTilemap)
            mazeGenerator.floorTilemap.color = theme.floorColor;

        if (mazeGenerator.wallTilemap)
            mazeGenerator.wallTilemap.color = theme.wallColor;

        if (mazeFrame)
            mazeFrame.color = theme.mazeFrameColor;

        if (mainCamera)
            mainCamera.backgroundColor = theme.backgroundColor.a > 0f ? theme.backgroundColor : theme.floorColor;
    }

    private MazeTheme GetTheme(int themeIndex)
    {
        if (themes != null && themes.Length > 0)
            return themes[Mathf.Abs(themeIndex) % themes.Length];

        return DefaultThemes[Mathf.Abs(themeIndex) % DefaultThemes.Length];
    }

    private void FitCameraToSafeArea(int logicalWidth, int logicalHeight)
    {
        if (!mainCamera)
            return;

        lastSafeArea = Screen.safeArea;
        lastScreen = new Vector2Int(Screen.width, Screen.height);

        float sw = Mathf.Max(1f, Screen.width);
        float sh = Mathf.Max(1f, Screen.height);
        float aspect = sw / sh;

        Rect safe = applySafeArea
            ? new Rect(lastSafeArea.x / sw, lastSafeArea.y / sh, lastSafeArea.width / sw, lastSafeArea.height / sh)
            : new Rect(0f, 0f, 1f, 1f);

        safe.xMin = Mathf.Clamp01(safe.xMin + viewportPadding.x);
        safe.xMax = Mathf.Clamp01(safe.xMax - viewportPadding.x);
        safe.yMin = Mathf.Clamp01(safe.yMin + viewportPadding.y + bottomHudReserve);
        safe.yMax = Mathf.Clamp01(safe.yMax - viewportPadding.y - topHudReserve);

        if (safe.width <= 0.05f || safe.height <= 0.05f)
            safe = new Rect(0.05f, 0.05f, 0.9f, 0.9f);

        int gridW = logicalWidth * 2 + 1;
        int gridH = logicalHeight * 2 + 1;
        float contentW = gridW + frameMarginX + cameraPadding * 2f;
        float contentH = gridH + frameMarginY + cameraPadding * 2f;

        float sizeFromWidth = contentW / (2f * aspect * safe.width);
        float sizeFromHeight = contentH / (2f * safe.height);
        float orthographicSize = Mathf.Max(sizeFromWidth, sizeFromHeight, 0.01f);

        float fullWorldH = orthographicSize * 2f;
        float fullWorldW = fullWorldH * aspect;
        Vector2 safeCenter = safe.center;
        Vector3 mazeCenter = new Vector3(gridW * 0.5f, gridH * 0.5f, -10f);

        mainCamera.rect = new Rect(0f, 0f, 1f, 1f);
        mainCamera.orthographicSize = orthographicSize;
        mainCamera.transform.position = new Vector3(
            mazeCenter.x - (safeCenter.x - 0.5f) * fullWorldW,
            mazeCenter.y - (safeCenter.y - 0.5f) * fullWorldH,
            -10f
        );
    }

    public void OnMazeCompleted()
    {
        Debug.Log($"Maze tamamlandi. Level: {levelIndex}");
        if (gameManager)
        {
            gameManager.OnMazeCompleted();
            return;
        }

        AdvanceLevel();
    }

    public void ReportPlayerMove()
    {
        if (gameManager)
            gameManager.RegisterPlayerMove();
    }

    public int GetMinimumSlideMoves()
    {
        if (!mazeGenerator)
            return 0;

        bool followCorners = !player || player.followCorners;
        return mazeGenerator.GetMinimumSlideMoves(followCorners);
    }

    public void AdvanceLevel()
    {
        if (gameManager)
        {
            gameManager.AdvanceLevel();
            return;
        }

        levelIndex++;
        BuildCurrentLevel();
    }
}

[System.Serializable]
public class MazeTheme
{
    public Color wallColor = Color.green;
    public Color floorColor = Color.black;
    public Color mazeFrameColor = Color.white;
    public Color backgroundColor = Color.black;
}
