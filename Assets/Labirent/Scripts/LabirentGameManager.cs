using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LabirentGameManager : MonoBehaviour
{
    private const string CurrentLevelPrefsKey = "LabirentOyunu.CurrentLevel";

    [Header("Maze Flow")]
    [SerializeField]
    private ExitFromToMaze mazeFlow;

    [Header("HUD")]
    [SerializeField]
    private LabirentLevelHud levelHud;

    [Header("Level System")]
    [SerializeField]
    private int currentLevelIndex = 0;

    [SerializeField]
    private bool loopLevels = true;

    [SerializeField]
    private bool useCustomLevels = false;

    [SerializeField]
    private LabirentLevelConfig[] customLevels;

    [Header("Generated Difficulty")]
    [SerializeField]
    [Tooltip("0 veya daha kucukse level uretimi sinirsiz devam eder.")]
    private int totalGeneratedLevels = 0;

    [SerializeField]
    private int baseWidth = 16;

    [SerializeField]
    private int baseHeight = 9;

    [SerializeField]
    private int widthPerStep = 2;

    [SerializeField]
    private int heightPerStep = 1;

    [SerializeField]
    private int levelsPerSizeStep = 5;

    [SerializeField]
    [Tooltip("Bu levelden sonra labirent boyutu artmaz. 1'den baslayan level numarasi.")]
    private int maxDifficultyLevel = 25;

    [SerializeField]
    private int maxWidth = 24;

    [SerializeField]
    private int maxHeight = 13;

    [SerializeField]
    private int baseSeed = 3;

    [Header("Intro Levels")]
    [SerializeField]
    private int introLevelCount = 3;

    [SerializeField]
    private int introBaseWidth = 8;

    [SerializeField]
    private int introBaseHeight = 5;

    [SerializeField]
    private int introWidthPerLevel = 2;

    [SerializeField]
    private int introHeightPerLevel = 1;

    [Header("Targets")]
    [SerializeField]
    private float targetSecondsPerMove = 2.5f;

    [SerializeField]
    private float targetSecondsPerDifficulty = 4f;

    [Header("Win SFX")]
    [SerializeField]
    private GameObject winSfxPrefab;

    [SerializeField]
    private float winSfxDuration = 1.5f;

    [SerializeField]
    private bool destroyWinSfxAfterDuration = true;

    private bool isTransitioning;
    private GameObject currentWinSfx;
    private LabirentLevelConfig currentLevelConfig;
    private LabirentLevelState currentState = new LabirentLevelState();
    private float levelStartTime;
    private float nextHudTick;

    public int CurrentLevelIndex => currentLevelIndex;
    public int CurrentLevelNumber => currentLevelIndex + 1;
    public LabirentLevelState CurrentState => currentState;
    public bool IsTransitioning => isTransitioning;
    public event Action<LabirentLevelState> OnLevelStateChanged;

    [Obsolete]
    private void Awake()
    {
        if (!mazeFlow)
            mazeFlow = FindObjectOfType<ExitFromToMaze>();

        if (!levelHud)
            levelHud = FindObjectOfType<LabirentLevelHud>();

        LoadProgress();
    }

    private void Start()
    {
        BuildCurrentLevel();
    }

    private void Update()
    {
        if (currentState == null || isTransitioning)
            return;

        currentState.elapsedSeconds = Time.time - levelStartTime;
        if (Time.time >= nextHudTick)
        {
            nextHudTick = Time.time + 0.2f;
            NotifyLevelStateChanged();
        }
    }

    public void OnMazeCompleted()
    {
        if (isTransitioning)
            return;

        StartCoroutine(CompleteMazeRoutine());
    }

    private IEnumerator CompleteMazeRoutine()
    {
        isTransitioning = true;
        float completedSeconds = Time.time - levelStartTime;
        currentState.elapsedSeconds = completedSeconds;
        currentState.completed = true;
        currentState.completionRank = GetCompletionRank();
        NotifyLevelStateChanged();

        if (levelHud)
            yield return levelHud.PlayCompletionInfoRoutine(currentState.completionRank);

        ShowWinSfx();

        if (winSfxDuration > 0f)
            yield return new WaitForSeconds(winSfxDuration);

        if (destroyWinSfxAfterDuration && currentWinSfx)
            Destroy(currentWinSfx);

        currentWinSfx = null;

        AdvanceLevelInternal();

        isTransitioning = false;
    }

    public void BuildCurrentLevel()
    {
        if (!mazeFlow)
            return;

        currentLevelConfig = CreateLevelConfig(currentLevelIndex);
        mazeFlow.BuildLevel(currentLevelConfig, currentLevelIndex);
        BeginLevelState();
    }

    public void AdvanceLevel()
    {
        if (isTransitioning)
            return;

        AdvanceLevelInternal();
    }

    private void AdvanceLevelInternal()
    {
        currentLevelIndex = GetNextLevelIndex();
        SaveProgress();
        BuildCurrentLevel();
    }

    public void RestartLevel()
    {
        if (isTransitioning)
            return;

        BuildCurrentLevel();
    }

    public void SetLevel(int levelIndex)
    {
        if (isTransitioning)
            return;

        currentLevelIndex = Mathf.Clamp(levelIndex, 0, Mathf.Max(0, GetLevelCount() - 1));
        SaveProgress();
        BuildCurrentLevel();
    }

    private void LoadProgress()
    {
        int savedLevelIndex = PlayerPrefs.GetInt(CurrentLevelPrefsKey, currentLevelIndex);
        currentLevelIndex = Mathf.Clamp(savedLevelIndex, 0, Mathf.Max(0, GetLevelCount() - 1));
    }

    private void SaveProgress()
    {
        PlayerPrefs.SetInt(CurrentLevelPrefsKey, currentLevelIndex);
        PlayerPrefs.Save();
    }

    private int GetNextLevelIndex()
    {
        int levelCount = GetLevelCount();
        if (levelCount == int.MaxValue)
            return currentLevelIndex + 1;

        int nextLevel = currentLevelIndex + 1;
        if (nextLevel < levelCount)
            return nextLevel;

        return loopLevels ? 0 : levelCount - 1;
    }

    private int GetLevelCount()
    {
        if (useCustomLevels && customLevels != null && customLevels.Length > 0)
            return customLevels.Length;

        return totalGeneratedLevels > 0 ? totalGeneratedLevels : int.MaxValue;
    }

    private LabirentLevelConfig CreateLevelConfig(int levelIndex)
    {
        if (useCustomLevels && customLevels != null && customLevels.Length > 0)
            return customLevels[Mathf.Clamp(levelIndex, 0, customLevels.Length - 1)];

        int cappedDifficultyIndex =
            maxDifficultyLevel > 0 ? Mathf.Min(levelIndex, maxDifficultyLevel - 1) : levelIndex;
        int difficultyTier = Mathf.Max(0, cappedDifficultyIndex / Mathf.Max(1, levelsPerSizeStep));
        int width = Mathf.Clamp(baseWidth + difficultyTier * widthPerStep, 2, maxWidth);
        int height = Mathf.Clamp(baseHeight + difficultyTier * heightPerStep, 2, maxHeight);

        if (levelIndex < introLevelCount)
        {
            width = Mathf.Clamp(introBaseWidth + levelIndex * introWidthPerLevel, 2, maxWidth);
            height = Mathf.Clamp(introBaseHeight + levelIndex * introHeightPerLevel, 2, maxHeight);
        }

        return new LabirentLevelConfig
        {
            width = width,
            height = height,
            seed = baseSeed == 0 ? 0 : baseSeed + levelIndex,
            difficultyTier = difficultyTier,
            endpointPattern = GetEndpointPattern(levelIndex, difficultyTier),
            extraOpeningChance = GetExtraOpeningChance(levelIndex, difficultyTier),
            themeIndex = difficultyTier,
        };
    }

    public void RegisterPlayerMove()
    {
        currentState.moveCount++;
        NotifyLevelStateChanged();
    }

    private void BeginLevelState()
    {
        levelStartTime = Time.time;
        nextHudTick = 0f;

        int minimumMoves = mazeFlow ? mazeFlow.GetMinimumSlideMoves() : 0;
        if (minimumMoves <= 0)
            minimumMoves = EstimateFallbackMinimumMoves(currentLevelConfig);

        currentState = new LabirentLevelState
        {
            levelIndex = currentLevelIndex,
            levelNumber = CurrentLevelNumber,
            difficultyTier = currentLevelConfig != null ? currentLevelConfig.difficultyTier : 0,
            width = currentLevelConfig != null ? currentLevelConfig.width : 0,
            height = currentLevelConfig != null ? currentLevelConfig.height : 0,
            seed = currentLevelConfig != null ? currentLevelConfig.seed : 0,
            minimumMoves = minimumMoves,
            targetMoves = CalculateTargetMoves(minimumMoves),
            targetSeconds = CalculateTargetSeconds(minimumMoves),
            maxDifficultyLevel = maxDifficultyLevel,
            isEndless = maxDifficultyLevel > 0 && currentLevelIndex >= maxDifficultyLevel,
            moveCount = 0,
            elapsedSeconds = 0f,
            completed = false,
            completionRank = string.Empty,
        };

        NotifyLevelStateChanged();
    }

    private MazeEndpointPattern GetEndpointPattern(int levelIndex, int difficultyTier)
    {
        if (levelIndex < introLevelCount)
            return MazeEndpointPattern.BottomLeftToTopRight;

        int patternCount = Enum.GetValues(typeof(MazeEndpointPattern)).Length;
        return (MazeEndpointPattern)((levelIndex + difficultyTier) % patternCount);
    }

    private float GetExtraOpeningChance(int levelIndex, int difficultyTier)
    {
        if (levelIndex < introLevelCount)
            return 0f;

        return Mathf.Clamp01(difficultyTier * 0.025f);
    }

    private int EstimateFallbackMinimumMoves(LabirentLevelConfig config)
    {
        if (config == null)
            return 1;

        return Mathf.Max(1, Mathf.CeilToInt((config.width + config.height) * 0.25f));
    }

    private int CalculateTargetMoves(int minimumMoves)
    {
        return Mathf.Max(1, minimumMoves * 3);
    }

    private int CalculateTargetSeconds(int minimumMoves)
    {
        float difficultyBonus =
            currentLevelConfig != null
                ? currentLevelConfig.difficultyTier * targetSecondsPerDifficulty
                : 0f;

        return Mathf.Max(
            10,
            Mathf.CeilToInt(
                CalculateTargetMoves(minimumMoves) * targetSecondsPerMove + difficultyBonus
            )
        );
    }

    private string GetCompletionRank()
    {
        if (currentState.moveCount <= currentState.minimumMoves + 2)
            return "Ola\u011fan \u00fcst\u00fc";

        if (currentState.moveCount < currentState.targetMoves)
            return "Harika";

        return "\u0130yi";
    }

    private void NotifyLevelStateChanged()
    {
        OnLevelStateChanged?.Invoke(currentState);
    }

    private void ShowWinSfx()
    {
        if (!winSfxPrefab)
            return;

        currentWinSfx = Instantiate(winSfxPrefab);
        PrepareNestedCanvas(currentWinSfx);
    }

    public void BackToSelect()
    {
        var pauseMenu = FindFirstObjectByType<LabirentPauseMenu>();
        if (pauseMenu) pauseMenu.OpenPauseMenu();
    }

    private void PrepareNestedCanvas(GameObject sfxInstance)
    {
        Canvas[] nestedCanvases = sfxInstance.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < nestedCanvases.Length; i++)
        {
            nestedCanvases[i].enabled = true;
            nestedCanvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
            nestedCanvases[i].sortingOrder = 100;
        }
    }
}

[System.Serializable]
public class LabirentLevelConfig
{
    public int width = 16;
    public int height = 9;
    public int seed = 0;
    public int difficultyTier = 0;
    public float extraOpeningChance = 0f;
    public MazeEndpointPattern endpointPattern = MazeEndpointPattern.BottomLeftToTopRight;

    [Tooltip("-1 ise level index'e gore tema secilir.")]
    public int themeIndex = -1;
}

[System.Serializable]
public class LabirentLevelState
{
    public int levelIndex;
    public int levelNumber;
    public int difficultyTier;
    public int width;
    public int height;
    public int seed;
    public int minimumMoves;
    public int targetMoves;
    public int targetSeconds;
    public int maxDifficultyLevel;
    public int moveCount;
    public float elapsedSeconds;
    public bool isEndless;
    public bool completed;
    public string completionRank;
}
