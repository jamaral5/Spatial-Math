using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The bottom-left "Skins" button and the panel it opens.
///
/// Each preset restyles both the surface and the sky together, so the two never clash.
/// The last entry loads an image from disk and wraps it over the surface using the UVs
/// the mesh already carries.
/// </summary>
[AddComponentMenu("Spatial Math/Skin Panel UI")]
public class SkinPanelUI : MonoBehaviour
{
    [Header("References (found automatically if left empty)")]
    public GraphManager graphManager;
    public SceneBackdrop sceneBackdrop;
    public Canvas targetCanvas;

    [Header("Layout")]
    [Tooltip("Which corner of the screen the button hangs off. (0,1) is top-left, " +
             "(0,0) bottom-left. The list opens away from that edge.")]
    public Vector2 anchor = new Vector2(0f, 1f);

    public Vector2 buttonSize = new Vector2(170f, 44f);
    public Vector2 buttonOffset = new Vector2(16f, -212f);
    public Vector2 panelSize = new Vector2(240f, 372f);

    [Header("Type Sizes")]
    public float buttonFontSize = 19f;
    public float entryFontSize = 16f;

    [Tooltip("How many times a pattern repeats across a custom uploaded image.")]
    public float customImageTiling = 1f;

    private RectTransform panel;
    private RectTransform button;
    private Texture2D customTexture;

    void Start()
    {
        if (graphManager == null) graphManager = FindFirstObjectByType<GraphManager>();
        if (sceneBackdrop == null) sceneBackdrop = FindFirstObjectByType<SceneBackdrop>();

        if (targetCanvas == null) targetCanvas = UIKit.FindSceneCanvas();
        if (targetCanvas == null || graphManager == null)
        {
            Debug.LogWarning("[SkinPanelUI] Needs a Canvas and a GraphManager — disabling.");
            enabled = false;
            return;
        }

        Build();

        // Hidden until the user leaves the title screen, like the rest of the controls.
        button.gameObject.SetActive(false);
        StartScreen.WhenDismissed(() =>
        {
            if (button != null) button.gameObject.SetActive(true);
        });
    }

    /// <summary>Live layout tweaking during Play mode. See GraphOpacityUI.OnValidate.</summary>
    void OnValidate()
    {
        if (!Application.isPlaying || button == null || panel == null) return;

        PlaceWidgets();
    }

    /// <summary>
    /// Positions the button and its list. Anchored to the top of the screen the list
    /// drops downward; anchored to the bottom it rises, so it never runs off-screen.
    /// </summary>
    private void PlaceWidgets()
    {
        UIKit.Corner(button, anchor, buttonSize, buttonOffset);

        bool opensDownward = anchor.y > 0.5f;
        float gap = buttonSize.y + 8f;

        UIKit.Corner(panel, anchor, panelSize,
                     new Vector2(buttonOffset.x,
                                 opensDownward ? buttonOffset.y - gap : buttonOffset.y + gap));
    }

    void OnDestroy()
    {
        if (customTexture != null) Destroy(customTexture);
    }

    // ───────────────────────────────────────────────────────────────────

    private void Build()
    {
        // ── The bottom-left button ────────────────────────────────────
        Button toggle = UIKit.TextButton("SkinsButton", targetCanvas.transform, "Skins",
                                         buttonFontSize, UIKit.NeonSoft);
        button = toggle.GetComponent<RectTransform>();
        toggle.onClick.AddListener(TogglePanel);

        // ── The list it opens, stacked clear of the button ────────────
        panel = UIKit.NeonPanel("SkinPanel", targetCanvas.transform, UIKit.PanelDark, UIKit.NeonSoft);

        PlaceWidgets();

        RectTransform column = UIKit.Stretch(UIKit.NewRect("Column", panel), 12f);
        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 5f;

        var heading = UIKit.Label("Heading", column, "GRAPH SKIN", 11f, UIKit.Neon,
                                  TMPro.TextAlignmentOptions.Left);
        heading.characterSpacing = 6f;
        heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;

        foreach (GraphSkin skin in SkinLibrary.Presets())
        {
            GraphSkin captured = skin;   // capture per iteration, not the loop variable
            AddEntry(column, skin.name, () => Apply(captured));
        }

        AddEntry(column, "Upload image…", LoadCustomImage);

        panel.gameObject.SetActive(false);
    }

    private void AddEntry(Transform parent, string label, System.Action action)
    {
        Button entry = UIKit.TextButton(label, parent, label, entryFontSize, new Color(1f, 1f, 1f, 0.10f));
        entry.gameObject.AddComponent<LayoutElement>().preferredHeight = entryFontSize + 16f;
        entry.onClick.AddListener(() => action?.Invoke());
    }

    private void TogglePanel()
    {
        if (panel != null) panel.gameObject.SetActive(!panel.gameObject.activeSelf);
    }

    // ───────────────────────────────────────────────────────────────────

    private void Apply(GraphSkin skin)
    {
        graphManager.SetSkin(skin.texture, skin.tint, skin.tiling);

        if (sceneBackdrop != null)
            sceneBackdrop.SetSkyColors(skin.skyTop, skin.skyHorizon, skin.skyBottom);
    }

    /// <summary>
    /// Loads a picture off disk and wraps it over the surface.
    ///
    /// In the editor this opens a real file dialog. A built player has no such dialog
    /// without a native plugin, so there it reads the newest image out of a Skins folder
    /// beside the save data and tells the user where that is.
    /// </summary>
    private void LoadCustomImage()
    {
        string path = PickImagePath();
        if (string.IsNullOrEmpty(path)) return;

        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SkinPanelUI] Could not read '{path}': {e.Message}");
            return;
        }

        // Replace rather than accumulate — otherwise every upload leaks a texture.
        if (customTexture != null) Destroy(customTexture);

        customTexture = new Texture2D(2, 2, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        if (!customTexture.LoadImage(data))
        {
            Debug.LogWarning($"[SkinPanelUI] '{path}' is not a PNG or JPG this build can decode.");
            Destroy(customTexture);
            customTexture = null;
            return;
        }

        customTexture.name = "Skin_Custom";
        graphManager.SetSkin(customTexture, Color.white, Vector2.one * customImageTiling);
    }

    private string PickImagePath()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel("Choose a graph skin", "", "png,jpg,jpeg");
#else
        string folder = Path.Combine(Application.persistentDataPath, "Skins");
        Directory.CreateDirectory(folder);

        var images = new List<string>();
        foreach (string pattern in new[] { "*.png", "*.jpg", "*.jpeg" })
            images.AddRange(Directory.GetFiles(folder, pattern));

        if (images.Count == 0)
        {
            Debug.Log($"[SkinPanelUI] Drop a PNG or JPG into {folder} and pick Upload image again.");
            return null;
        }

        // Newest file wins, so dropping a picture in and clicking again just works.
        images.Sort((a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
        return images[0];
#endif
    }
}
