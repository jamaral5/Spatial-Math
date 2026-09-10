using UnityEngine;

/// <summary>The outline the tangent plane is cut to.</summary>
public enum PlaneShape
{
    Square,
    Circle,
    Triangle,
    Hexagon,
    Diamond
}

/// <summary>
/// Builds the flat meshes the tangent plane is drawn with.
///
/// Every shape is a regular polygon, so one generator covers all of them — a triangle is
/// a 3-gon, a square is a 4-gon turned 45 degrees, a circle is a 48-gon. The mesh lies in
/// the local XZ plane with its face pointing along local +Y, which is the orientation
/// TangentPlaneRenderer rotates onto the surface normal.
/// </summary>
public static class PlaneShapes
{
    public static int SidesFor(PlaneShape shape) => shape switch
    {
        PlaneShape.Triangle => 3,
        PlaneShape.Square   => 4,
        PlaneShape.Diamond  => 4,
        PlaneShape.Hexagon  => 6,
        _                   => 48,   // enough segments to read as a circle
    };

    /// <summary>
    /// Rotation applied to the polygon, in degrees. A 4-gon sits as a diamond by default,
    /// so the square is the same polygon turned 45 degrees onto its flats.
    /// </summary>
    public static float AngleOffsetFor(PlaneShape shape) => shape switch
    {
        PlaneShape.Square   => 45f,
        PlaneShape.Triangle => 90f,
        _                   => 0f,
    };

    public static Mesh Build(PlaneShape shape, float radius)
    {
        return RegularPolygon(SidesFor(shape), radius, AngleOffsetFor(shape), shape.ToString());
    }

    /// <summary>
    /// A double-sided triangle fan. Both faces are generated rather than relying on the
    /// material being set to render back faces: a tangent plane gets looked at from below
    /// as often as from above, and a single-sided one vanishes when you orbit under it.
    /// </summary>
    private static Mesh RegularPolygon(int sides, float radius, float angleOffsetDegrees, string name)
    {
        sides = Mathf.Max(3, sides);

        int perFace = sides + 1;                 // centre plus one vertex per corner
        var vertices = new Vector3[perFace * 2];
        var normals = new Vector3[perFace * 2];
        var uvs = new Vector2[perFace * 2];
        var triangles = new int[sides * 3 * 2];

        float offset = angleOffsetDegrees * Mathf.Deg2Rad;
        int back = perFace;

        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;
        uvs[0] = new Vector2(0.5f, 0.5f);

        vertices[back] = Vector3.zero;
        normals[back] = Vector3.down;
        uvs[back] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < sides; i++)
        {
            float angle = offset + (i / (float)sides) * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            var corner = new Vector3(cos * radius, 0f, sin * radius);
            var uv = new Vector2(0.5f + cos * 0.5f, 0.5f + sin * 0.5f);

            vertices[1 + i] = corner;
            normals[1 + i] = Vector3.up;
            uvs[1 + i] = uv;

            vertices[back + 1 + i] = corner;
            normals[back + 1 + i] = Vector3.down;
            uvs[back + 1 + i] = uv;
        }

        int t = 0;
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            triangles[t++] = 0;
            triangles[t++] = 1 + next;
            triangles[t++] = 1 + i;
        }

        // Same fan, wound the other way, so this half faces down.
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            triangles[t++] = back;
            triangles[t++] = back + 1 + i;
            triangles[t++] = back + 1 + next;
        }

        var mesh = new Mesh { name = $"TangentPlane_{name}", hideFlags = HideFlags.DontSave };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }
}
