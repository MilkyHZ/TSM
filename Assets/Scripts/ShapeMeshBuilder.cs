using System.Collections.Generic;
using UnityEngine;

public enum ShapeType { Cube, Pyramid, Sphere }

public static class ShapeMeshBuilder
{
    public static Mesh Build(ShapeType shape, float width = 1f, float height = 1f, float depth = 1f)
    {
        return shape switch
        {
            ShapeType.Pyramid => BuildPyramid(width, height, depth),
            ShapeType.Sphere => BuildSphere(width, height, depth, 12, 12),
            _ => BuildCube(width, height, depth)
        };
    }

    public static Mesh BuildCube(float w = 1f, float h = 1f, float d = 1f)
    {
        float hx = w * 0.5f, hy = h * 0.5f, hz = d * 0.5f;
        Vector3[] v = {
            new(-hx,-hy,-hz), new( hx,-hy,-hz), new( hx, hy,-hz), new(-hx, hy,-hz),
            new(-hx,-hy, hz), new( hx,-hy, hz), new( hx, hy, hz), new(-hx, hy, hz)
        };
        int[][] f = {
            new[]{0,3,2,1}, new[]{5,6,7,4}, new[]{4,7,3,0},
            new[]{1,2,6,5}, new[]{3,7,6,2}, new[]{4,0,1,5}
        };
        return BuildMesh("Cube", v, f);
    }

    public static Mesh BuildPyramid(float w = 1f, float h = 1f, float d = 1f)
    {
        float hx = w * 0.5f, hy = h * 0.5f, hz = d * 0.5f;
        Vector3[] v = {
            new(-hx,-hy,-hz), new( hx,-hy,-hz), new( hx,-hy, hz), new(-hx,-hy, hz), new(0, hy, 0)
        };
        int[][] f = {
            new[]{0,4,1}, new[]{1,4,2}, new[]{2,4,3}, new[]{3,4,0}, new[]{0,1,2,3}
        };
        return BuildMesh("Pyramid", v, f);
    }

    public static Mesh BuildSphere(float w = 1f, float h = 1f, float d = 1f, int rings = 12, int segs = 12)
    {
        float hx = w * 0.5f, hy = h * 0.5f, hz = d * 0.5f;
        var verts = new List<Vector3> { new(0, hy, 0) };
        var faces = new List<int[]>();

        for (int r = 1; r < rings; r++)
        {
            float phi = Mathf.PI * r / rings;
            float y = Mathf.Cos(phi) * hy;
            float rad = Mathf.Sin(phi);
            for (int s = 0; s < segs; s++)
            {
                float theta = -2f * Mathf.PI * s / segs;
                verts.Add(new Vector3(rad * Mathf.Cos(theta) * hx, y, rad * Mathf.Sin(theta) * hz));
            }
        }
        int bottom = verts.Count;
        verts.Add(new(0, -hy, 0));

        for (int s = 0; s < segs; s++)
            faces.Add(new[] { 0, 1 + s, 1 + (s + 1) % segs });

        for (int r = 0; r < rings - 2; r++)
        {
            int a = 1 + r * segs, b = 1 + (r + 1) * segs;
            for (int s = 0; s < segs; s++)
            {
                int next = (s + 1) % segs;
                faces.Add(new[] { a + s, b + s, b + next, a + next });
            }
        }
        int last = 1 + (rings - 2) * segs;
        for (int s = 0; s < segs; s++)
            faces.Add(new[] { bottom, last + (s + 1) % segs, last + s });

        return BuildMesh("Sphere", verts.ToArray(), faces.ToArray());
    }

    private static Mesh BuildMesh(string name, Vector3[] verts, int[][] faces)
    {
        var tris = new List<int>();
        foreach (var f in faces)
        {
            tris.Add(f[0]); tris.Add(f[1]); tris.Add(f[2]);
            if (f.Length == 4) { tris.Add(f[0]); tris.Add(f[2]); tris.Add(f[3]); }
        }
        Mesh m = new Mesh { name = name, vertices = verts, triangles = tris.ToArray() };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}