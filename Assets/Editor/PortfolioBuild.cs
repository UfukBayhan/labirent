using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PortfolioBuild
{
    public const string ScenePath = "Assets/Labirent/LabirentOyunu.unity";

    public static void ConfigureAndBuild()
    {
        PlayerSettings.companyName = "Ufuk Bayhan";
        PlayerSettings.productName = "Labirent";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.ufukbayhan.labirent");
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        settings.FindProperty("activeInputHandler").intValue = 2;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSettings.serializationMode = SerializationMode.ForceText;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        const string fontPath = "Assets/Labirent/PortfolioFont.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        if (!font)
        {
            font = TMP_FontAsset.CreateFontAsset(sourceFont);
            font.name = "Labirent UI";
            AssetDatabase.CreateAsset(font, fontPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
        }
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject) > 0)
                    throw new Exception("Missing script: " + tr.name);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = font;
                text.fontSharedMaterial = font.material;
            }
        }
        MakeButtonLabel("BackToSellect", "Menu", font);
        MakeButtonLabel("PauseButton", "II", font);
        var exit = GameObject.Find("Exit").GetComponent<SpriteRenderer>();
        exit.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Labirent/Tiles/DAIRE_BEYAZ.asset");
        exit.color = new Color(1f, 0.78f, 0.16f, 1f);
        var portal = exit.GetComponent<Portal>();
        portal.glowColor = exit.color;
        portal.baseSize = 0.75f / exit.sprite.bounds.size.x;
        var player = GameObject.Find("Player").GetComponent<SpriteRenderer>();
        float playerScale = 0.7f / player.sprite.bounds.size.x;
        player.transform.localScale = new Vector3(playerScale, playerScale, 1f);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/Windows/Labirent.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Build failed: " + report.summary.result);
        Debug.Log("PORTFOLIO_BUILD_OK");
    }

    private static void MakeButtonLabel(string name, string label, TMP_FontAsset font)
    {
        var go = GameObject.Find(name);
        if (!go) throw new Exception("Missing button: " + name);
        go.GetComponent<Image>().color = new Color(0.06f, 0.19f, 0.14f, 1f);
        var existing = go.transform.Find("PortfolioLabel");
        var text = existing ? existing.GetComponent<TextMeshProUGUI>() : new GameObject("PortfolioLabel", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(go.transform, false);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        text.text = label;
        text.font = font;
        text.fontSize = 26;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12;
        text.fontSizeMax = 26;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }
}
