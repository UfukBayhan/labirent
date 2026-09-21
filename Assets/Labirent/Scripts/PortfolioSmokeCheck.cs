#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Runs only when explicitly requested on a development player.
public class PortfolioSmokeCheck : MonoBehaviour
{
    private bool failed;
    private const string LevelKey = "LabirentOyunu.CurrentLevel";
    private const string TutorialKey = "LabirentOyunu_TutorialSeen";
    private bool hadLevel, hadTutorial;
    private int savedLevel, savedTutorial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void StartCheck()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--portfolio-smoke-test") < 0) return;
        var go = new GameObject("Portfolio Smoke Check");
        DontDestroyOnLoad(go);
        go.AddComponent<PortfolioSmokeCheck>();
    }

    private void Awake()
    {
        hadLevel = PlayerPrefs.HasKey(LevelKey);
        hadTutorial = PlayerPrefs.HasKey(TutorialKey);
        savedLevel = PlayerPrefs.GetInt(LevelKey);
        savedTutorial = PlayerPrefs.GetInt(TutorialKey);
        PlayerPrefs.SetInt(LevelKey, 0);
        PlayerPrefs.SetInt(TutorialKey, 0);
        Application.logMessageReceived += OnLog;
    }

    private void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("SMOKE FAILED: " + message);
    }

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2.5f);
        var tutorial = FindFirstObjectByType<FirstLaunchTutorial>();
        Check(tutorial && FirstLaunchTutorial.IsTutorialInputBlocked, "first-launch tutorial opens");
        Check(tutorial.TryCloseFromBack(), "tutorial closes");
        yield return new WaitForSecondsRealtime(0.3f);
        Check(!FirstLaunchTutorial.IsTutorialInputBlocked && Time.timeScale == 1f, "tutorial releases input");
        var maze = FindFirstObjectByType<MazeGenerator>();
        var manager = FindFirstObjectByType<LabirentGameManager>();
        var pause = FindFirstObjectByType<LabirentPauseMenu>();
        Check(maze && manager && pause, "scene managers exist");
        foreach (MazeEndpointPattern pattern in Enum.GetValues(typeof(MazeEndpointPattern)))
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                maze.InitAndGenerate(8, 5, seed, pattern, 0.1f);
                Check(Reachable(maze), "exit reachable: " + pattern + "/" + seed);
                Check(maze.GetMinimumSlideMoves(true) > 0 && maze.GetMinimumSlideMoves(false) > 0, "sliding solution exists");
                string first = GridSignature(maze);
                maze.InitAndGenerate(8, 5, seed, pattern, 0.1f);
                Check(first == GridSignature(maze), "seed is reproducible");
            }
        }
        manager.SetLevel(24);
        Check(Reachable(maze) && maze.GetMinimumSlideMoves(true) > 0, "advanced level is solvable");
        manager.SetLevel(0);
        manager.BackToSelect();
        yield return new WaitForSecondsRealtime(0.3f);
        Check(Time.timeScale == 0f, "Menu opens local pause menu");
        pause.ResumeGame();
        yield return new WaitForSecondsRealtime(0.3f);
        Check(Time.timeScale == 1f, "resume restores time");
        manager.RegisterPlayerMove();
        manager.RestartLevel();
        Check(manager.CurrentState.moveCount == 0, "restart resets moves");
        manager.OnMazeCompleted();
        yield return new WaitForSecondsRealtime(8f);
        Check(manager.CurrentLevelIndex == 1 && !manager.IsTransitioning, "completion advances level");
        manager.SetLevel(0);
        yield return null;
        // Capture the actual game and UI from the standalone player's camera.
        var camera = Camera.main;
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
        }
        Canvas.ForceUpdateCanvases();
        var rt = new RenderTexture(1280, 720, 24);
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllBytes(Path.Combine(root, "docs/gameplay.png"), image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        Destroy(image);
        RestorePreferences();
        Debug.Log(failed ? "PORTFOLIO_SMOKE_FAILED" : "PORTFOLIO_SMOKE_OK");
        Application.Quit(failed ? 1 : 0);
    }

    private static string GridSignature(MazeGenerator maze)
    {
        var result = new StringBuilder();
        for (int y = 0; y < maze.GridHeight; y++)
            for (int x = 0; x < maze.GridWidth; x++)
                result.Append(maze.IsCellWalkable(new Vector3Int(x, y, 0)) ? '1' : '0');
        return result.ToString();
    }

    private static bool Reachable(MazeGenerator maze)
    {
        var seen = new HashSet<Vector3Int> { maze.StartCell };
        var queue = new Queue<Vector3Int>();
        queue.Enqueue(maze.StartCell);
        var directions = new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (cell == maze.EndCell) return true;
            foreach (var direction in directions)
            {
                var next = cell + direction;
                if (maze.IsCellWalkable(next) && seen.Add(next)) queue.Enqueue(next);
            }
        }
        return false;
    }

    private void RestorePreferences()
    {
        if (hadLevel) PlayerPrefs.SetInt(LevelKey, savedLevel); else PlayerPrefs.DeleteKey(LevelKey);
        if (hadTutorial) PlayerPrefs.SetInt(TutorialKey, savedTutorial); else PlayerPrefs.DeleteKey(TutorialKey);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
        RestorePreferences();
    }
}
#endif
