using System.Collections;
using TMPro;
using UnityEngine;

public class LabirentLevelHud : MonoBehaviour
{
    [SerializeField]
    private LabirentGameManager gameManager;

    [Header("Texts")]
    [SerializeField]
    private TextMeshProUGUI levelText;

    [SerializeField]
    private TextMeshProUGUI moveText;

    [SerializeField]
    private TextMeshProUGUI timeText;

    [SerializeField]
    private TextMeshProUGUI infoText;

    [Header("Completion Info")]
    [SerializeField]
    private float infoTextMinVisibleSeconds = 0.75f;

    [SerializeField]
    private float infoTextMaxVisibleSeconds = 5f;

    [SerializeField]
    private float infoTextMoveSpeed = 700f;

    private RectTransform infoTextRect;
    private Vector2 infoTextStartPosition;
    private MonoBehaviour[] infoTextMovers;

    [System.Obsolete]
    private void Awake()
    {
        if (!gameManager)
            gameManager = FindObjectOfType<LabirentGameManager>();

        CacheInfoTextStart();
        HideInfoText();
    }

    [System.Obsolete]
    private void OnEnable()
    {
        if (!gameManager)
            gameManager = FindObjectOfType<LabirentGameManager>();

        if (gameManager)
        {
            gameManager.OnLevelStateChanged += HandleLevelStateChanged;
            HandleLevelStateChanged(gameManager.CurrentState);
        }
    }

    private void OnDisable()
    {
        if (gameManager)
            gameManager.OnLevelStateChanged -= HandleLevelStateChanged;
    }
    private void HandleLevelStateChanged(LabirentLevelState state)
    {
        if (state == null || state.levelNumber <= 0)
            return;

        string time = $"{Mathf.FloorToInt(state.elapsedSeconds)}/{state.targetSeconds}";
        string moves = $"{state.moveCount}/{state.targetMoves}";

        SetText(levelText, $"Seviye {state.levelNumber}");
        SetText(moveText, $"Hamle {moves}");
        SetText(timeText, $"S\u00fcre {time}");

        if (!state.completed)
            HideInfoText();
        else
            SetText(infoText, state.completionRank);
    }

    public IEnumerator PlayCompletionInfoRoutine(string message)
    {
        if (!infoText)
            yield break;

        CacheInfoTextStart();
        SetText(infoText, message);

        GameObject infoObject = infoText.gameObject;
        infoTextRect.anchoredPosition = infoTextStartPosition;
        SetInfoTextMoversEnabled(false);
        infoObject.SetActive(true);

        yield return null;

        float startTime = Time.time;
        while (Time.time - startTime < infoTextMinVisibleSeconds)
        {
            MoveInfoTextUp();
            yield return null;
        }

        while (!IsInfoTextAboveScreen() && Time.time - startTime < infoTextMaxVisibleSeconds)
        {
            MoveInfoTextUp();
            yield return null;
        }

        HideInfoText();
    }

    private void SetText(TextMeshProUGUI target, string value)
    {
        if (target)
            target.text = value;
    }

    private void CacheInfoTextStart()
    {
        if (!infoText)
            return;

        if (!infoTextRect)
        {
            infoTextRect = infoText.rectTransform;
            infoTextStartPosition = infoTextRect.anchoredPosition;
        }
    }

    private void HideInfoText()
    {
        if (!infoText)
            return;

        CacheInfoTextStart();
        SetText(infoText, string.Empty);
        infoTextRect.anchoredPosition = infoTextStartPosition;
        infoText.gameObject.SetActive(false);
        SetInfoTextMoversEnabled(true);
    }

    private bool IsInfoTextAboveScreen()
    {
        if (!infoTextRect)
            return true;

        Vector3[] corners = new Vector3[4];
        infoTextRect.GetWorldCorners(corners);
        Camera uiCamera = GetUiCamera();
        float bottomY = float.MaxValue;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);
            bottomY = Mathf.Min(bottomY, screenPoint.y);
        }

        return bottomY > Screen.height;
    }

    private void MoveInfoTextUp()
    {
        if (!infoTextRect)
            return;

        infoTextRect.anchoredPosition += Vector2.up * infoTextMoveSpeed * Time.deltaTime;
    }

    private void SetInfoTextMoversEnabled(bool enabled)
    {
        if (!infoText)
            return;

        if (infoTextMovers == null)
            infoTextMovers = infoText.GetComponents<MonoBehaviour>();

        for (int i = 0; i < infoTextMovers.Length; i++)
        {
            MonoBehaviour behaviour = infoTextMovers[i];
            if (!behaviour || behaviour == this)
                continue;

            if (behaviour.GetType().Name == "MovableObject")
                behaviour.enabled = enabled;
        }
    }

    private Camera GetUiCamera()
    {
        Canvas canvas = infoText ? infoText.canvas : null;
        if (!canvas || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }
}
