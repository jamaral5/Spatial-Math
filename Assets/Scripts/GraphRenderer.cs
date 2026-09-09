using UnityEngine;
using UnityEngine.Rendering;
using System;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class GraphRenderer : MonoBehaviour
{
    [Header("Graph Settings")]
    [Tooltip("Number of subdivisions along each axis. Higher = smoother but more expensive.")]
    public int resolution = 60;

    [Tooltip("The range along X and Z axes: [-graphRange, graphRange]")]
    public float graphRange = 5f;

    [Tooltip("Maximum Y value clamp to prevent extreme spikes.")]
    public float maxYClamp = 10f;

    [Header("Visual")]
    public Material graphMaterial;
    public Gradient colorGradient;

    [Tooltip("How solid the surface looks. 0 = invisible, 1 = fully opaque.")]
    [Range(0f, 1f)]
    public float opacity = 0.65f;

    private Func<float, float, float> equationFunc;
    private Mesh mesh;
    private string currentEquation = "";

    private float minY, maxY;

    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;

    // Shader property IDs, looked up once. Cheaper than passing strings every frame
    // while the user drags the opacity slider.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId     = Shader.PropertyToID("_Color");
    private static readonly int SurfaceId   = Shader.PropertyToID("_Surface");
    private static readonly int BlendId     = Shader.PropertyToID("_Blend");
    private static readonly int SrcBlendId  = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId  = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteId    = Shader.PropertyToID("_ZWrite");
    private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
    private static readonly int BaseMapId   = Shader.PropertyToID("_BaseMap");

    // Our private copy of graphMaterial, plus the asset it was copied from so we only
    // re-copy when this slot is actually handed a different material.
    private Material runtimeMaterial;
    private Material materialSource;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        mesh = new Mesh();
        mesh.name = "GraphMesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        meshFilter.mesh = mesh;

        ApplyMaterial();

        if (colorGradient == null || colorGradient.colorKeys.Length == 0)
        {
            colorGradient = new Gradient();
            colorGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.1f, 0.3f, 1f), 0.0f),
                    new GradientColorKey(new Color(0.1f, 0.9f, 0.4f), 0.33f),
                    new GradientColorKey(new Color(1f, 0.9f, 0.1f), 0.66f),
                    new GradientColorKey(new Color(1f, 0.2f, 0.1f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
        }
    }

    void OnDestroy()
    {
        // runtimeMaterial is created with 'new', so nothing else will clean it up.
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
    }

    /// <summary>
    /// Gives this graph its OWN copy of the assigned material. Opacity is stored on the
    /// material, so without a copy, dragging the slider would permanently edit the
    /// shared .mat asset on disk and fade every graph slot at the same time.
    /// </summary>
    private void ApplyMaterial()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        if (graphMaterial == null) return;

        if (runtimeMaterial == null || materialSource != graphMaterial)
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);

            runtimeMaterial = new Material(graphMaterial);
            runtimeMaterial.name = graphMaterial.name + " (runtime copy)";
            materialSource = graphMaterial;

            MakeTransparent(runtimeMaterial);
        }

        meshRenderer.sharedMaterial = runtimeMaterial;
        ApplyOpacity();
    }

    /// <summary>
    /// Switches a URP material into alpha-blended mode. An opaque material throws the
    /// alpha channel away, so without this the opacity slider would silently do nothing
    /// whenever the assigned material was not already set to Transparent.
    /// </summary>
    private static void MakeTransparent(Material m)
    {
        SetFloatIfPresent(m, SurfaceId,   1f);                                 // 0 = Opaque, 1 = Transparent
        SetFloatIfPresent(m, BlendId,     0f);                                 // 0 = Alpha blend
        SetFloatIfPresent(m, SrcBlendId,  (float)BlendMode.SrcAlpha);
        SetFloatIfPresent(m, DstBlendId,  (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(m, AlphaClipId, 0f);

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");   // we write straight alpha, not premultiplied
        m.DisableKeyword("_ALPHATEST_ON");

        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    /// <summary>Writes the current opacity into the material alpha channel.</summary>
    private void ApplyOpacity()
    {
        if (runtimeMaterial == null) return;

        // Only alpha changes here. RGB stays whatever the material was authored with.
        if (runtimeMaterial.HasProperty(BaseColorId))
        {
            Color c = runtimeMaterial.GetColor(BaseColorId);
            c.a = opacity;
            runtimeMaterial.SetColor(BaseColorId, c);
        }
        if (runtimeMaterial.HasProperty(ColorId))
        {
            Color c = runtimeMaterial.GetColor(ColorId);
            c.a = opacity;
            runtimeMaterial.SetColor(ColorId, c);
        }

        // At full opacity there is nothing to see through, so let the surface write
        // depth again. That stops a folded graph from showing its own far side through
        // its near side, which is the usual artifact of a transparent mesh.
        SetFloatIfPresent(runtimeMaterial, ZWriteId, opacity >= 0.99f ? 1f : 0f);
    }

    private static void SetFloatIfPresent(Material m, int propertyId, float value)
    {
        if (m.HasProperty(propertyId)) m.SetFloat(propertyId, value);
    }

    /// <summary>
    /// Applies a skin: a repeating pattern (or none), a tint, and how many times the
    /// pattern repeats across the surface.
    ///
    /// BuildMesh already lays UVs 0..1 over the whole grid, so the texture stretches
    /// across the shape and follows its folds rather than sitting flat.
    /// </summary>
    public void SetSkin(Texture texture, Color tint, Vector2 tiling)
    {
        ApplyMaterial();
        if (runtimeMaterial == null) return;

        if (runtimeMaterial.HasProperty(BaseMapId))
        {
            runtimeMaterial.SetTexture(BaseMapId, texture);
            runtimeMaterial.SetTextureScale(BaseMapId, tiling);
        }

        // Tint multiplies the texture, so a patterned skin passes white through here and
        // lets its own colours show. Alpha is left to ApplyOpacity.
        if (runtimeMaterial.HasProperty(BaseColorId))
        {
            tint.a = opacity;
            runtimeMaterial.SetColor(BaseColorId, tint);
        }
        if (runtimeMaterial.HasProperty(ColorId))
        {
            tint.a = opacity;
            runtimeMaterial.SetColor(ColorId, tint);
        }

        ApplyOpacity();
    }

    /// <summary>Fades this graph. 0 = invisible, 1 = solid. Cheap enough to call every frame.</summary>
    public void SetOpacity(float value)
    {
        opacity = Mathf.Clamp01(value);
        ApplyOpacity();
    }

    public float GetOpacity() => opacity;

    public bool SetEquation(string equation)
    {
        try
        {
            var parser = new EquationParser();
            equationFunc = parser.Parse(equation);
            currentEquation = equation;

            ApplyMaterial();
            BuildMesh();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GraphRenderer] Failed to parse equation '{equation}': {e.Message}");
            return false;
        }
    }

    public string GetCurrentEquation() => currentEquation;

    /// <summary>
    /// The lowest and highest values the last build actually produced, and whether a
    /// build has happened at all. BuildMesh already replaces NaN and infinity with zero
    /// and clamps to maxYClamp, so these are always finite and bounded — which is what
    /// lets the axes size themselves off a divergent function without blowing up.
    /// </summary>
    public bool HasBounds { get; private set; }
    public float MinValue => minY;
    public float MaxValue => maxY;

    public void RebuildMesh()
    {
        ApplyMaterial();
        if (equationFunc != null) BuildMesh();
    }
    public float Evaluate(float x, float z)
    {
        if (equationFunc == null) return 0f;
        return equationFunc(x, z);
    }
    private void BuildMesh()
    {
        int verts = resolution + 1;
        int totalVerts = verts * verts;

        Vector3[] vertices = new Vector3[totalVerts];
        Vector2[] uvs = new Vector2[totalVerts];
        Color[] colors = new Color[totalVerts];

        float step = (graphRange * 2f) / resolution;

        float[] yValues = new float[totalVerts];
        minY = float.MaxValue;
        maxY = float.MinValue;

        for (int zi = 0; zi <= resolution; zi++)
        {
            for (int xi = 0; xi <= resolution; xi++)
            {
                float x = -graphRange + xi * step;
                float z = -graphRange + zi * step;
                float y = 0f;

                try
                {
                    y = equationFunc(x, z);
                    if (float.IsNaN(y) || float.IsInfinity(y)) y = 0f;
                    y = Mathf.Clamp(y, -maxYClamp, maxYClamp);
                }
                catch
                {
                    y = 0f;
                }

                int idx = zi * verts + xi;
                yValues[idx] = y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        HasBounds = true;

        float yRange = Mathf.Max(maxY - minY, 0.001f);

        for (int zi = 0; zi <= resolution; zi++)
        {
            for (int xi = 0; xi <= resolution; xi++)
            {
                float x = -graphRange + xi * step;
                float z = -graphRange + zi * step;
                int idx = zi * verts + xi;
                float y = yValues[idx];

                vertices[idx] = new Vector3(x, y, z);
                uvs[idx] = new Vector2((float)xi / resolution, (float)zi / resolution);

                float t = (y - minY) / yRange;
                colors[idx] = colorGradient.Evaluate(t);
            }
        }

        int[] triangles = new int[resolution * resolution * 6];
        int triIdx = 0;
        for (int zi = 0; zi < resolution; zi++)
        {
            for (int xi = 0; xi < resolution; xi++)
            {
                int bl = zi * verts + xi;
                int br = bl + 1;
                int tl = bl + verts;
                int tr = tl + 1;

                triangles[triIdx++] = bl;
                triangles[triIdx++] = tl;
                triangles[triIdx++] = br;

                triangles[triIdx++] = br;
                triangles[triIdx++] = tl;
                triangles[triIdx++] = tr;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }
}