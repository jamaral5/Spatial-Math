using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Gives the app an environment instead of Unity's default empty-project sky.
///
/// Two pieces, both generated at runtime so there is nothing to author by hand:
///   1. A dark vertical gradient skybox (Shaders/GradientSkybox.shader).
///   2. A ground grid of lines that fades out with distance (Shaders/GridLines.shader),
///      sitting below the axes so the graph always reads as floating in a space.
///
/// Drop this on any GameObject in the scene. It touches RenderSettings, which Unity
/// treats as scene state, so anything it changes is reverted when play mode ends.
/// </summary>
[AddComponentMenu("Spatial Math/Scene Backdrop")]
public class SceneBackdrop : MonoBehaviour
{
    [Header("Shaders")]
    [Tooltip("Leave empty and they are found by name. Assign them before making a BUILD, " +
             "because Unity strips shaders that no scene object references.")]
    public Shader skyboxShader;
    public Shader gridShader;

    [Header("Sky Gradient")]
    public Color skyTopColor     = new Color(0.020f, 0.028f, 0.070f);
    public Color skyHorizonColor = new Color(0.070f, 0.120f, 0.210f);
    public Color skyBottomColor  = new Color(0.010f, 0.012f, 0.022f);
    [Range(0.2f, 5f)] public float skyFalloff = 1.4f;

    [Header("Ambient Light")]
    [Tooltip("A dark sky also means dark ambient light. This keeps the graph readable.")]
    public bool overrideAmbientLight = true;
    public Color ambientColor = new Color(0.28f, 0.30f, 0.36f);

    [Header("Ground Grid")]
    [Tooltip("Off by default — the floor grid competes with the graph for attention. " +
             "The gradient sky alone gives depth without the visual noise.")]
    public bool showGrid = false;

    [Tooltip("How far the grid reaches from the origin, in world units.")]
    public float gridExtent = 26f;

    [Tooltip("Spacing between the thin lines.")]
    public float cellSize = 1f;

    [Tooltip("Every Nth line is drawn brighter. Set to 0 for a uniform grid.")]
    public int majorLineEvery = 5;

    [Tooltip("Height of the grid plane. Kept below the axes so the two never overlap.")]
    public float gridHeight = -5f;

    public Color minorLineColor = new Color(0.38f, 0.48f, 0.62f, 0.16f);
    public Color majorLineColor = new Color(0.45f, 0.66f, 0.92f, 0.40f);

    // ─── Runtime-created objects ───────────────────────────────────────
    private Material skyboxMaterial;
    private Material gridMaterial;
    private GameObject gridObject;

    // Saved so the scene is left exactly as we found it.
    private bool skyApplied;
    private bool ambientApplied;
    private Material previousSkybox;
    private AmbientMode previousAmbientMode;
    private Color previousAmbientColor;

    void Start()
    {
        BuildSky();
        if (showGrid) BuildGrid();
    }

    void OnDestroy()
    {
        // Restore the lighting settings first, then throw away the materials that were
        // pointed at — the other order would leave RenderSettings holding a dead object.
        if (skyApplied) RenderSettings.skybox = previousSkybox;

        if (ambientApplied)
        {
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientLight = previousAmbientColor;
        }

        if (skyboxMaterial != null) Destroy(skyboxMaterial);
        if (gridMaterial != null) Destroy(gridMaterial);
    }

    // ───────────────────────────────────────────────────────────────────
    // Sky
    // ───────────────────────────────────────────────────────────────────

    private void BuildSky()
    {
        Shader shader = ResolveShader(skyboxShader, "Spatial Math/Gradient Skybox");
        if (shader == null) return;

        skyboxMaterial = new Material(shader) { name = "Backdrop Sky (runtime)" };
        skyboxMaterial.SetColor("_TopColor", skyTopColor);
        skyboxMaterial.SetColor("_HorizonColor", skyHorizonColor);
        skyboxMaterial.SetColor("_BottomColor", skyBottomColor);
        skyboxMaterial.SetFloat("_Falloff", skyFalloff);

        previousSkybox = RenderSettings.skybox;
        RenderSettings.skybox = skyboxMaterial;
        skyApplied = true;

        if (overrideAmbientLight)
        {
            previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientColor = RenderSettings.ambientLight;

            // Flat ambient, rather than ambient sampled from the (now very dark) sky.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            ambientApplied = true;
        }
    }

    // ───────────────────────────────────────────────────────────────────
    // Grid
    // ───────────────────────────────────────────────────────────────────

    private void BuildGrid()
    {
        Shader shader = ResolveShader(gridShader, "Spatial Math/Grid Lines");
        if (shader == null) return;

        gridMaterial = new Material(shader) { name = "Backdrop Grid (runtime)" };

        // Draw ahead of the graph surface. Both are transparent, and without this the two
        // would be sorted against each other by object centre, which can flicker as the
        // camera orbits. The grid is always scenery, so it always goes first.
        gridMaterial.renderQueue = (int)RenderQueue.Transparent - 100;

        gridObject = new GameObject("Backdrop Grid");
        gridObject.transform.SetParent(transform, false);

        gridObject.AddComponent<MeshFilter>().mesh = BuildGridMesh();

        var renderer = gridObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = gridMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    /// <summary>
    /// Builds the whole grid as ONE line-topology mesh: one draw call no matter how many
    /// lines. Each line is cut into one segment per cell so the distance fade can be
    /// evaluated at every crossing instead of only at the two far ends.
    /// </summary>
    private Mesh BuildGridMesh()
    {
        float spacing = Mathf.Max(0.05f, cellSize);
        int half = Mathf.Clamp(Mathf.RoundToInt(gridExtent / spacing), 1, 200);
        float extent = half * spacing;

        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var indices = new List<int>();

        // Pass 0 draws the lines running along Z, pass 1 the lines running along X.
        for (int axis = 0; axis < 2; axis++)
        {
            for (int line = -half; line <= half; line++)
            {
                bool isMajor = majorLineEvery > 0 && line % majorLineEvery == 0;
                Color lineColor = isMajor ? majorLineColor : minorLineColor;
                float fixedCoord = line * spacing;

                for (int segment = -half; segment < half; segment++)
                {
                    float from = segment * spacing;
                    float to = (segment + 1) * spacing;

                    Vector3 a = axis == 0
                        ? new Vector3(fixedCoord, gridHeight, from)
                        : new Vector3(from, gridHeight, fixedCoord);
                    Vector3 b = axis == 0
                        ? new Vector3(fixedCoord, gridHeight, to)
                        : new Vector3(to, gridHeight, fixedCoord);

                    indices.Add(vertices.Count);
                    vertices.Add(a);
                    colors.Add(FadeWithDistance(lineColor, a, extent));

                    indices.Add(vertices.Count);
                    vertices.Add(b);
                    colors.Add(FadeWithDistance(lineColor, b, extent));
                }
            }
        }

        var mesh = new Mesh { name = "BackdropGridMesh" };
        mesh.indexFormat = IndexFormat.UInt32;   // a dense grid easily passes 65k vertices
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetIndices(indices, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Dims a vertex the further it sits from the origin, so the grid dissolves into the
    /// sky instead of ending at a hard square border.
    /// </summary>
    private static Color FadeWithDistance(Color color, Vector3 position, float extent)
    {
        float t = Mathf.Clamp01(new Vector2(position.x, position.z).magnitude / extent);
        color.a *= 1f - t * t;
        return color;
    }

    /// <summary>Uses the assigned shader, falling back to a lookup by name.</summary>
    private static Shader ResolveShader(Shader assigned, string shaderName)
    {
        if (assigned != null) return assigned;

        Shader found = Shader.Find(shaderName);
        if (found == null)
            Debug.LogWarning($"[SceneBackdrop] Shader '{shaderName}' not found. " +
                             "Assign it in the Inspector, or check it imported without errors.");
        return found;
    }
}
