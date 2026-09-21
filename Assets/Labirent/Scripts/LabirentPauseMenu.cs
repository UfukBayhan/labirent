using System.Collections;
using UnityEngine;

public class LabirentPauseMenu : MonoBehaviour
{
    [SerializeField]
    private LabirentGameManager gameManager;

    [SerializeField]
    private GameObject menuRoot;

    [SerializeField]
    private CanvasGroup canvasGroup;

    [SerializeField]
    private RectTransform panelRect;

    [SerializeField]
    private GameObject firstLevelButton;

    [SerializeField]
    private float animationDuration = 0.18f;

    private Coroutine animationRoutine;
    private bool isPaused;

    private void Awake()
    {
        if (!gameManager)
            gameManager = FindObjectOfType<LabirentGameManager>();

        CloseImmediately();
    }

    public void OpenPauseMenu()
    {
        if (isPaused || !gameManager || gameManager.IsTransitioning || !menuRoot)
            return;

        isPaused = true;
        Time.timeScale = 0f;

        if (firstLevelButton)
            firstLevelButton.SetActive(gameManager.CurrentLevelIndex > 0);

        menuRoot.SetActive(true);

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(AnimateOpen());
    }

    public void ResumeGame()
    {
        if (!isPaused)
            return;

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(AnimateClose());
    }

    public void RestartCurrentLevel()
    {
        if (!isPaused || !gameManager)
            return;

        StopActiveAnimation();
        CloseImmediately();
        gameManager.RestartLevel();
    }

    public void ReturnToFirstLevel()
    {
        if (!isPaused || !gameManager)
            return;

        StopActiveAnimation();
        CloseImmediately();
        gameManager.SetLevel(0);
    }

    private IEnumerator AnimateOpen()
    {
        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;
        }

        if (panelRect)
            panelRect.localScale = Vector3.one * 0.92f;

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.01f, animationDuration));

            if (canvasGroup)
                canvasGroup.alpha = t;
            if (panelRect)
                panelRect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, t);

            yield return null;
        }

        if (canvasGroup)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
        }

        if (panelRect)
            panelRect.localScale = Vector3.one;

        animationRoutine = null;
    }

    private IEnumerator AnimateClose()
    {
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.01f, animationDuration));

            if (canvasGroup)
                canvasGroup.alpha = 1f - t;
            if (panelRect)
                panelRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.92f, t);

            yield return null;
        }

        CloseImmediately();
    }

    private void CloseImmediately()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (panelRect)
            panelRect.localScale = Vector3.one;

        if (menuRoot)
            menuRoot.SetActive(false);

        animationRoutine = null;
    }

    private void StopActiveAnimation()
    {
        if (animationRoutine == null)
            return;

        StopCoroutine(animationRoutine);
        animationRoutine = null;
    }

    private void OnDestroy()
    {
        if (isPaused)
            Time.timeScale = 1f;
    }
}
