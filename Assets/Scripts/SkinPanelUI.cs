using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// The "Customize Graph" button and the skin list it opens.
///
/// Each preset restyles both the surface and the sky together, so the two never clash.
/// The last entry loads an image from disk and wraps it over the surface using the UVs
/// the mesh already carries.
/// </summary>
[AddComponentMenu("Spatial Math/Customize Graph Panel")]
public class SkinPanelUI : ToolPanelUI
{
    [Header("References (found automatically if left empty)")]
    public GraphManager graphManager;
    public SceneBackdrop sceneBackdrop;

    [Header("Label")]
    public string buttonLabel = "Customize Graph";

    [Tooltip("How many times a pattern repeats across a custom uploaded image.")]
    public float customImageTiling = 1f;

    private Texture2D customTexture;

    protected override string ButtonLabel => buttonLabel;

    /// <summary>Default placement when the component is first added: the upper slot.</summary>
    private void Reset()
    {
        stackOrder = 0;
    }

    protected override bool Initialise()
    {
        if (graphManager == null) graphManager = FindFirstObjectByType<GraphManager>();
        if (sceneBackdrop == null) sceneBackdrop = FindFirstObjectByType<SceneBackdrop>();

        if (graphManager == null)
        {
            Debug.LogWarning("[SkinPanelUI] No GraphManager in the scene — disabling.");
            return false;
        }

        return true;
    }

    protected override void Populate(RectTransform column)
    {
        AddHeading(column, "Graph skin");

        foreach (GraphSkin skin in SkinLibrary.Presets())
        {
            GraphSkin captured = skin;   // capture per iteration, not the loop variable
            AddEntry(column, skin.name, () => Apply(captured));
        }

        AddEntry(column, "Upload image…", LoadCustomImage);
    }

    private void OnDestroy()
    {
        if (customTexture != null) Destroy(customTexture);
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
