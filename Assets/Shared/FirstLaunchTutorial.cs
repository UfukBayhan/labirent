using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

public class FirstLaunchTutorial : MonoBehaviour
{
    public static bool IsTutorialInputBlocked { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private Image tutorialImage;

    [Header("Tutorial Images")]
    [SerializeField] private Sprite[] tutorialSprites;

    [Header("Timing")]
    [SerializeField] private float tutorialDelay = 1.5f;

    [Header("PlayerPrefs")]
    [SerializeField] private string tutorialKey = "ColorConnect_TutorialSeen";

    private GameObject helpButton;
    private int currentIndex = 0;
    private float previousTimeScale = 1f;
    private bool isTutorialOpen;

    private void OnValidate()
    {
        if (!TryValidateConfiguration(out string errorMessage))
            Debug.LogWarning($"FirstLaunchTutorial: {errorMessage}", this);
    }

    private void Start()
    {
        if (!TryValidateConfiguration(out string errorMessage))
        {
            Debug.LogError($"FirstLaunchTutorial devre disi birakildi: {errorMessage}", this);
            IsTutorialInputBlocked = false;
            enabled = false;
            return;
        }

        helpButton = GameObject.Find("HelpButton");

        tutorialPanel.SetActive(false);

        if (helpButton != null)
            helpButton.SetActive(true);

        IsTutorialInputBlocked = false;

        if (PlayerPrefs.GetInt(tutorialKey, 0) == 0)
            StartCoroutine(OpenTutorialAfterDelay());
    }

    private IEnumerator OpenTutorialAfterDelay()
    {
        yield return new WaitForSecondsRealtime(tutorialDelay);
        OpenTutorial();
    }

    private void Update()
    {
        if (!tutorialPanel.activeSelf) return;

        bool touched = false;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            touched = true;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            touched = true;

        if (touched)
            NextTutorial();
    }

    public void OpenTutorial()
    {
        if (!enabled || isTutorialOpen)
            return;

        currentIndex = 0;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        isTutorialOpen = true;
        IsTutorialInputBlocked = true;

        tutorialPanel.SetActive(true);

        if (helpButton != null)
            helpButton.SetActive(false);

        ShowCurrentTutorial();
    }

    public bool TryCloseFromBack()
    {
        if (!isTutorialOpen)
            return false;

        CloseTutorial();
        return true;
    }

    private void NextTutorial()
    {
        currentIndex++;

        if (currentIndex >= tutorialSprites.Length)
        {
            CloseTutorial();
            return;
        }

        ShowCurrentTutorial();
    }

    private void ShowCurrentTutorial()
    {
        if (tutorialSprites == null || tutorialSprites.Length == 0) return;

        tutorialImage.sprite = tutorialSprites[currentIndex];
    }

    private void CloseTutorial()
    {
        PlayerPrefs.SetInt(tutorialKey, 1);
        PlayerPrefs.Save();

        tutorialPanel.SetActive(false);

        if (helpButton != null)
            helpButton.SetActive(true);

        Time.timeScale = previousTimeScale;
        isTutorialOpen = false;

        StartCoroutine(ReleaseInputAfterPointerUp());
    }

    private void OnDisable()
    {
        if (isTutorialOpen)
            Time.timeScale = previousTimeScale;

        isTutorialOpen = false;
        IsTutorialInputBlocked = false;
    }

    private IEnumerator ReleaseInputAfterPointerUp()
    {
        while (Mouse.current != null && Mouse.current.leftButton.isPressed)
            yield return null;

        while (Touchscreen.current != null &&
               Touchscreen.current.primaryTouch.press.isPressed)
            yield return null;

        yield return new WaitForSecondsRealtime(0.15f);

        IsTutorialInputBlocked = false;
    }

    private bool TryValidateConfiguration(out string errorMessage)
    {
        if (!tutorialPanel)
            errorMessage = "TutorialPanel referansi eksik.";
        else if (!tutorialImage)
            errorMessage = "TutorialImage referansi eksik.";
        else if (tutorialSprites == null || tutorialSprites.Length == 0)
            errorMessage = "En az bir tutorial gorseli atanmalidir.";
        else if (string.IsNullOrWhiteSpace(tutorialKey))
            errorMessage = "Tutorial PlayerPrefs anahtari bos olamaz.";
        else if (tutorialDelay < 0f)
            errorMessage = "Tutorial gecikmesi negatif olamaz.";
        else
        {
            errorMessage = string.Empty;
            return true;
        }

        return false;
    }
}
