using System.Collections.Generic;
using UnityEngine;

// Mallas generadas por código: suelo en disco, pared curva, secciones, aro (toro) y pirámide.
// Las usa el constructor de escenas (se guardan como assets) y la partida (las secciones dependen de los jugadores).
public static class ProceduralMesh
{
    // Disco en el plano XZ mirando hacia arriba. UV = posición / uvSize (la cuadrícula se repite cada uvSize unidades)
    public static Mesh Disc(float radius, int segments, float uvSize, string name = "Disco")
    {
        var v = new List<Vector3> { Vector3.zero };
        var uv = new List<Vector2> { Vector2.zero };
        var t = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float a = i * 2f * Mathf.PI / segments;
            var p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            v.Add(p);
            uv.Add(new Vector2(p.x, p.z) / uvSize);
        }
        for (int i = 1; i <= segments; i++) { t.Add(0); t.Add(i + 1); t.Add(i); }
        return Build(name, v, uv, t, true);
    }

    // Porción de corona circular (para teñir la sección de cada jugador)
    public static Mesh Wedge(float fromDeg, float toDeg, float innerRadius, float outerRadius, int segments)
    {
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var t = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Lerp(fromDeg, toDeg, i / (float)segments) * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(d * innerRadius);
            v.Add(d * outerRadius);
            uv.Add(new Vector2(i / (float)segments, 0f));
            uv.Add(new Vector2(i / (float)segments, 1f));
            if (i == segments) break;
            int k = i * 2;
            t.Add(k); t.Add(k + 3); t.Add(k + 1);
            t.Add(k); t.Add(k + 2); t.Add(k + 3);
        }
        return Build("Seccion", v, uv, t, true);
    }

    // Pared curva cerrada (cara interior, exterior y de arriba). Sirve también como MeshCollider sin huecos.
    public static Mesh WallBand(float innerRadius, float outerRadius, float height, int segments, string name = "Pared")
    {
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var t = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u0, float u1)
        {
            int s = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(new Vector2(u0, 0)); uv.Add(new Vector2(u1, 0)); uv.Add(new Vector2(u1, 1)); uv.Add(new Vector2(u0, 1));
            t.Add(s); t.Add(s + 1); t.Add(s + 2);
            t.Add(s); t.Add(s + 2); t.Add(s + 3);
        }
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * 2f * Mathf.PI / segments, a1 = (i + 1) * 2f * Mathf.PI / segments;
            var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            Vector3 up = Vector3.up * height;
            float u0 = i / (float)segments * 40f, u1 = (i + 1) / (float)segments * 40f;
            Quad(d0 * innerRadius, d1 * innerRadius, d1 * innerRadius + up, d0 * innerRadius + up, u0, u1);   // interior (mira al centro)
            Quad(d1 * outerRadius, d0 * outerRadius, d0 * outerRadius + up, d1 * outerRadius + up, u0, u1);   // exterior
            Quad(d0 * innerRadius + up, d1 * innerRadius + up, d1 * outerRadius + up, d0 * outerRadius + up, u0, u1); // arriba
        }
        return Build(name, v, uv, t, false);
    }

    // Toro acostado en el plano XZ (el agujero mira hacia +Y)
    public static Mesh Torus(float majorRadius, float minorRadius, int majorSegments, int minorSegments, string name = "Aro")
    {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var uv = new List<Vector2>();
        var t = new List<int>();
        for (int i = 0; i <= majorSegments; i++)
        {
            float a = i * 2f * Mathf.PI / majorSegments;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int j = 0; j <= minorSegments; j++)
            {
                float b = j * 2f * Mathf.PI / minorSegments;
                var normal = d * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                v.Add(d * majorRadius + normal * minorRadius);
                n.Add(normal);
                uv.Add(new Vector2(i / (float)majorSegments, j / (float)minorSegments));
            }
        }
        int ring = minorSegments + 1;
        for (int i = 0; i < majorSegments; i++)
            for (int j = 0; j < minorSegments; j++)
            {
                int a = i * ring + j, b = (i + 1) * ring + j;
                t.Add(a); t.Add(a + 1); t.Add(b);
                t.Add(b); t.Add(a + 1); t.Add(b + 1);
            }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    // Pirámide de base cuadrada, con caras planas (una normal por cara)
    public static Mesh Pyramid(float baseSize, float height)
    {
        float h = baseSize / 2f;
        var apex = new Vector3(0f, height, 0f);
        Vector3[] c = { new Vector3(-h, 0, -h), new Vector3(h, 0, -h), new Vector3(h, 0, h), new Vector3(-h, 0, h) };
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var t = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            int s = v.Count;
            v.Add(c[i]); v.Add(apex); v.Add(c[(i + 1) % 4]);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0.5f, 1)); uv.Add(new Vector2(1, 0));
            t.Add(s); t.Add(s + 1); t.Add(s + 2);
        }
        int b = v.Count;
        v.AddRange(c);
        uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
        t.Add(b); t.Add(b + 1); t.Add(b + 2);
        t.Add(b); t.Add(b + 2); t.Add(b + 3);
        return Build("Piramide", v, uv, t, false);
    }

    // Rampa (cuña): ancho en X, largo en Z; sube desde el suelo en −Z hasta la altura en +Z. Caras planas.
    public static Mesh Ramp(float width, float length, float height)
    {
        float w = width / 2f, l = length / 2f;
        Vector3 b0 = new Vector3(-w, 0, -l), b1 = new Vector3(w, 0, -l), b2 = new Vector3(w, 0, l), b3 = new Vector3(-w, 0, l);
        Vector3 t2 = new Vector3(w, height, l), t3 = new Vector3(-w, height, l);
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var t = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int s = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
            t.Add(s); t.Add(s + 1); t.Add(s + 2);
            t.Add(s); t.Add(s + 2); t.Add(s + 3);
        }
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int s = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1));
            t.Add(s); t.Add(s + 1); t.Add(s + 2);
        }
        Quad(b0, t3, t2, b1);   // la pendiente
        Quad(b2, t2, t3, b3);   // la pared alta de atrás
        Quad(b0, b1, b2, b3);   // la base
        Tri(b0, b3, t3);        // lado izquierdo
        Tri(b1, t2, b2);        // lado derecho
        return Build("Rampa", v, uv, t, false);
    }

    private static Mesh Build(string name, List<Vector3> v, List<Vector2> uv, List<int> t, bool flatUp)
    {
        var mesh = new Mesh { name = name };
        mesh.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        if (flatUp)
        {
            var n = new Vector3[v.Count];
            for (int i = 0; i < n.Length; i++) n[i] = Vector3.up;
            mesh.normals = n;
        }
        else mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }
}
