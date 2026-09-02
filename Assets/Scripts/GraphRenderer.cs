using UnityEngine;
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

    private Func<float, float, float> equationFunc;
    private Mesh mesh;
    private string currentEquation = "";

    private float minY, maxY;
    
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;

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

    private void ApplyMaterial()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        if (graphMaterial != null)
            meshRenderer.sharedMaterial = graphMaterial;
    }

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