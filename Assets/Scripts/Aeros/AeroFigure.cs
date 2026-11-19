using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// El Aero en 3D, armado con piezas brillantes a partir de un AeroLook. Mismas proporciones que el dibujo
// de la presentación (1 unidad = 100 px del SVG): mide unas 3 unidades con la cabeza; se escala desde afuera.
// Mira hacia +Z. No tiene colisiones: la pelota lo atraviesa. Flota y se mueve solo, como en el canal Aeros.
public class AeroFigure : MonoBehaviour
{
    private Transform pivot, torso, head, handL, handR, footL, footR, wingL, wingR;
    private Vector3 headBase, handLBase, handRBase;
    private float phase, hopTime = -1f;
    private readonly List<Object> owned = new List<Object>();   // materiales de esta figura (se liberan al destruirla)

    public AeroLook Look { get; private set; }

    public static AeroFigure Create(AeroLook look, Transform parent, string name = "Aero")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var f = go.AddComponent<AeroFigure>();
        f.Build(look);
        return f;
    }

    // Saltito de alegría (al empezar su turno o al ganar monedas)
    public void Hop() => hopTime = 0f;

    public void Build(AeroLook look)
    {
        Look = look.Clone();
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
        foreach (var o in owned) if (o) Destroy(o);
        owned.Clear();
        phase = Random.value * 10f;

        float sx = Mathf.Clamp(look.w, AeroLook.MinW, AeroLook.MaxW), sy = Mathf.Clamp(look.h, AeroLook.MinH, AeroLook.MaxH);
        Color body = AeroLook.ParseColor(look.body, new Color(0.18f, 0.66f, 1f));
        Color headC = AeroLook.ParseColor(look.head, body);
        Color hatC = AeroLook.ParseColor(look.hatColor, Color.red);
        Color scarfC = AeroLook.ParseColor(look.scarfColor, Color.cyan);
        var bodyMat = Glossy(body);
        var headMat = Glossy(headC);

        pivot = Group("Pivote", transform, Vector3.zero);

        // Alas y capa van detrás del cuerpo
        if (look.Has("wings"))
        {
            var iri = Iridescent();
            float wy = 0.2f + 1.05f * sy;
            wingL = Group("Ala izq", pivot, new Vector3(-0.35f * sx, wy, -0.45f));
            wingR = Group("Ala der", pivot, new Vector3(0.35f * sx, wy, -0.45f));
            Piece(PrimitiveType.Sphere, wingL, new Vector3(-0.6f, 0.25f, 0f), new Vector3(1.3f, 0.75f, 0.08f), iri, Quaternion.Euler(0f, 0f, 30f));
            Piece(PrimitiveType.Sphere, wingR, new Vector3(0.6f, 0.25f, 0f), new Vector3(1.3f, 0.75f, 0.08f), iri, Quaternion.Euler(0f, 0f, -30f));
        }

        // Cuerpo de gota: se estira desde 0,2 (cerca de los pies)
        torso = Group("Torso", pivot, new Vector3(0f, 0.2f, 0f));
        var bodyGo = MeshPiece("Cuerpo", torso, DropMesh(), bodyMat);
        bodyGo.transform.localScale = new Vector3(sx, sy, sx * 0.92f);
        if (look.Has("cape"))
        {
            var cape = Glossy(new Color(0.83f, 0.17f, 0.19f), 0.7f);
            Piece(PrimitiveType.Sphere, torso, new Vector3(0f, 0.55f * sy, -0.42f * sx), new Vector3(1.75f * sx, 1.6f * sy, 0.5f), cape);
            Piece(PrimitiveType.Sphere, torso, new Vector3(0f, 1.18f * sy, 0.38f * sx), Vector3.one * 0.16f, Gold());
        }
        float neck = 1.3f * sy;   // altura del cuello dentro del torso
        BuildScarf(look.scarf, scarfC, Group("Bufanda", torso, new Vector3(0f, neck, 0f)), sx);
        if (look.Has("pin"))
        {
            var pin = Group("Pin", torso, new Vector3(-0.42f * sx, 0.95f * sy, 0.62f * sx));
            pin.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            Piece(PrimitiveType.Cylinder, pin, Vector3.zero, new Vector3(0.3f, 0.02f, 0.3f), Glossy(Color.white, 0.9f));
            Piece(PrimitiveType.Cylinder, pin, new Vector3(0.04f, 0.02f, -0.02f), new Vector3(0.15f, 0.02f, 0.15f), Glossy(new Color(1f, 0.36f, 0.56f)));
            Piece(PrimitiveType.Cylinder, pin, new Vector3(-0.06f, 0.03f, 0.04f), new Vector3(0.12f, 0.02f, 0.12f), Glossy(new Color(1f, 0.89f, 0.54f)));
        }

        // Manos y pies flotantes
        float handY = 0.2f + 0.92f * sy, handX = 1.04f * sx + 0.14f;
        handL = Group("Mano izq", pivot, new Vector3(-handX, handY, 0.05f));
        handR = Group("Mano der", pivot, new Vector3(handX, handY, 0.05f));
        Piece(PrimitiveType.Sphere, handL, Vector3.zero, Vector3.one * 0.42f, bodyMat);
        Piece(PrimitiveType.Sphere, handR, Vector3.zero, Vector3.one * 0.42f, bodyMat);
        if (look.Has("wand")) BuildWand(handR);
        handLBase = handL.localPosition; handRBase = handR.localPosition;
        footL = Group("Pie izq", pivot, new Vector3(-0.32f * sx, 0.13f, 0.08f));
        footR = Group("Pie der", pivot, new Vector3(0.32f * sx, 0.13f, 0.08f));
        Piece(PrimitiveType.Sphere, footL, Vector3.zero, new Vector3(0.52f, 0.26f, 0.62f), bodyMat);
        Piece(PrimitiveType.Sphere, footR, Vector3.zero, new Vector3(0.52f, 0.26f, 0.62f), bodyMat);

        // Cabeza de burbuja, sin cara
        head = Group("Cabeza", pivot, new Vector3(0f, 0.94f + 1.4f * sy, 0f));
        headBase = head.localPosition;
        Piece(PrimitiveType.Sphere, head, Vector3.zero, Vector3.one * 1.32f, headMat);
        if (look.Has("phones")) BuildPhones(head, hatC);
        BuildGlasses(look.glasses, head);
        BuildHat(look.hat, hatC, head);
        if (look.Has("flower")) BuildFlower(head);
    }

    // ---------------- Animación ----------------

    private void Update()
    {
        if (!pivot) return;
        float t = Time.time * 2.4f + phase;
        head.localPosition = headBase + Vector3.up * (Mathf.Sin(t) * 0.05f);
        handL.localPosition = handLBase + Vector3.up * (Mathf.Sin(t + 1.3f) * 0.08f);
        handR.localPosition = handRBase + Vector3.up * (Mathf.Sin(t + 2.6f) * 0.08f);
        torso.localScale = new Vector3(1f - Mathf.Sin(t * 2f) * 0.012f, 1f + Mathf.Sin(t * 2f) * 0.02f, 1f);
        if (wingL)
        {
            float flap = Mathf.Sin(t * 3f) * 14f;
            wingL.localRotation = Quaternion.Euler(0f, -18f, flap);
            wingR.localRotation = Quaternion.Euler(0f, 18f, -flap);
        }

        float hop = 0f;
        if (hopTime >= 0f)
        {
            hopTime += Time.deltaTime;
            const float duration = 0.55f;
            float k = hopTime / duration;
            hop = k < 1f ? Mathf.Sin(k * Mathf.PI) * 0.9f : 0f;
            if (k >= 1f) hopTime = -1f;
        }
        pivot.localPosition = Vector3.up * hop;
    }

    private void OnDestroy()
    {
        foreach (var o in owned) if (o) Destroy(o);
    }

    // ---------------- Ropa y accesorios ----------------

    private void BuildHat(string id, Color c, Transform h)
    {
        var mat = Glossy(c);
        var dark = Glossy(Color.Lerp(c, Color.black, 0.2f));
        switch (id)
        {
            case "beanie":
                MeshPiece("Gorro", h, DomeMesh(), mat).transform.localPosition = new Vector3(0f, 0.08f, 0f);
                var band = MeshPiece("Borde", h, TorusMesh(0.68f, 0.1f, 360f), dark);
                band.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                Piece(PrimitiveType.Sphere, h, new Vector3(0f, 0.92f, 0f), Vector3.one * 0.34f, Glossy(Color.Lerp(c, Color.white, 0.35f)));
                break;
            case "cap":
                MeshPiece("Gorra", h, DomeMesh(), mat).transform.localPosition = new Vector3(0f, 0.06f, 0f);
                Piece(PrimitiveType.Cylinder, h, new Vector3(0f, 0.12f, 0.62f), new Vector3(0.95f, 0.025f, 0.75f), dark);
                Piece(PrimitiveType.Sphere, h, new Vector3(0f, 0.82f, 0f), Vector3.one * 0.12f, dark);
                break;
            case "tophat":
                Piece(PrimitiveType.Cylinder, h, new Vector3(0f, 0.92f, 0f), new Vector3(0.88f, 0.4f, 0.88f), mat);
                Piece(PrimitiveType.Cylinder, h, new Vector3(0f, 0.7f, 0f), new Vector3(0.9f, 0.08f, 0.9f), Glossy(c.grayscale > 0.9f ? new Color(0.91f, 0.2f, 0.23f) : Color.white));
                Piece(PrimitiveType.Cylinder, h, new Vector3(0f, 0.53f, 0f), new Vector3(1.64f, 0.025f, 1.64f), dark);
                break;
            case "crown":
                var gold = Gold();
                MeshPiece("Corona", h, TubeMesh(0.55f, 0.3f), gold).transform.localPosition = new Vector3(0f, 0.36f, 0f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI * 2f / 6f;
                    var spike = MeshPiece("Punta", h, ConeMesh(), gold);
                    spike.transform.localPosition = new Vector3(Mathf.Sin(a) * 0.52f, 0.66f, Mathf.Cos(a) * 0.52f);
                    spike.transform.localScale = new Vector3(0.2f, 0.3f, 0.2f);
                }
                Piece(PrimitiveType.Sphere, h, new Vector3(0f, 0.5f, 0.56f), Vector3.one * 0.13f, Glossy(new Color(0.91f, 0.2f, 0.23f)));
                Piece(PrimitiveType.Sphere, h, new Vector3(-0.3f, 0.5f, 0.47f), Vector3.one * 0.1f, Glossy(new Color(0.1f, 0.65f, 0.85f)));
                Piece(PrimitiveType.Sphere, h, new Vector3(0.3f, 0.5f, 0.47f), Vector3.one * 0.1f, Glossy(new Color(0.37f, 0.82f, 0.24f)));
                break;
            case "party":
                var cone = MeshPiece("Gorro de fiesta", h, ConeMesh(), mat);
                cone.transform.localPosition = new Vector3(0.08f, 0.5f, 0f);
                cone.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
                cone.transform.localScale = new Vector3(0.66f, 0.78f, 0.66f);
                Piece(PrimitiveType.Sphere, h, new Vector3(-0.03f, 1.28f, 0f), Vector3.one * 0.22f, Glossy(new Color(1f, 0.82f, 0.25f)));
                break;
            case "halo":
                var iri = Iridescent();
                MeshPiece("Aro", h, TorusMesh(0.58f, 0.07f, 360f), iri).transform.localPosition = new Vector3(0f, 0.9f, 0f);
                Piece(PrimitiveType.Sphere, h, new Vector3(-0.5f, 1.04f, 0.1f), Vector3.one * 0.16f, iri);
                Piece(PrimitiveType.Sphere, h, new Vector3(0.5f, 1.08f, -0.1f), Vector3.one * 0.12f, iri);
                break;
        }
    }

    private void BuildScarf(string id, Color c, Transform neck, float sx)
    {
        Material mat = id == "stripes" ? Striped(c) : Glossy(c, 0.75f);
        switch (id)
        {
            case "plain":
            case "stripes":
                var ring = MeshPiece("Vuelta", neck, TorusMesh(0.5f, 0.16f, 360f), mat);
                ring.transform.localScale = new Vector3(sx, 1f, sx * 0.92f);
                var tail = Piece(PrimitiveType.Cube, neck, new Vector3(0.3f * sx, -0.38f, 0.48f * sx), new Vector3(0.26f, 0.7f, 0.08f), mat, Quaternion.Euler(-12f, 0f, 10f));
                tail.name = "Punta";
                break;
            case "bow":
                Transform bow = Group("Moño", neck, new Vector3(0f, -0.05f, 0.5f * sx));
                var l = MeshPiece("Lazo", bow, ConeMesh(), mat);
                l.transform.localPosition = new Vector3(-0.2f, 0f, 0f); l.transform.localRotation = Quaternion.Euler(0f, 0f, -90f); l.transform.localScale = new Vector3(0.36f, 0.38f, 0.2f);
                var r = MeshPiece("Lazo", bow, ConeMesh(), mat);
                r.transform.localPosition = new Vector3(0.2f, 0f, 0f); r.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); r.transform.localScale = new Vector3(0.36f, 0.38f, 0.2f);
                Piece(PrimitiveType.Sphere, bow, Vector3.zero, Vector3.one * 0.2f, Glossy(Color.Lerp(c, Color.black, 0.15f)));
                break;
            case "tie":
                Transform tie = Group("Corbata", neck, new Vector3(0f, -0.05f, 0.5f * sx));
                tie.localRotation = Quaternion.Euler(-14f, 0f, 0f);
                Piece(PrimitiveType.Cube, tie, Vector3.zero, new Vector3(0.18f, 0.14f, 0.1f), Glossy(Color.Lerp(c, Color.black, 0.15f)));
                Piece(PrimitiveType.Cube, tie, new Vector3(0f, -0.4f, 0.02f), new Vector3(0.2f, 0.55f, 0.05f), mat);
                Piece(PrimitiveType.Cube, tie, new Vector3(0f, -0.7f, 0.02f), new Vector3(0.2f, 0.2f, 0.05f), mat, Quaternion.Euler(0f, 0f, 45f));
                break;
            case "beads":
                var alt = Glossy(Color.Lerp(c, Color.white, 0.4f));
                for (int i = 0; i < 9; i++)
                {
                    float a = Mathf.Lerp(-80f, 80f, i / 8f) * Mathf.Deg2Rad;
                    Piece(PrimitiveType.Sphere, neck, new Vector3(Mathf.Sin(a) * 0.6f * sx, -0.12f - Mathf.Cos(a) * 0.08f, Mathf.Cos(a) * 0.55f * sx), Vector3.one * 0.2f, i % 2 == 0 ? mat : alt);
                }
                break;
        }
    }

    private void BuildGlasses(string id, Transform h)
    {
        var frame = Glossy(new Color(0.17f, 0.18f, 0.23f), 0.8f);
        switch (id)
        {
            case "round":
                var lens = Clear(new Color(0.82f, 0.94f, 1f, 0.45f));
                foreach (float x in new[] { -0.25f, 0.25f })
                {
                    var ring = MeshPiece("Aro", h, TorusMesh(0.2f, 0.035f, 360f), frame);
                    ring.transform.localPosition = new Vector3(x, 0.04f, 0.62f); ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Piece(PrimitiveType.Cylinder, h, new Vector3(x, 0.04f, 0.61f), new Vector3(0.38f, 0.005f, 0.38f), lens, Quaternion.Euler(90f, 0f, 0f));
                }
                Piece(PrimitiveType.Cube, h, new Vector3(0f, 0.08f, 0.66f), new Vector3(0.12f, 0.03f, 0.03f), frame);
                break;
            case "sun":
                var black = Glossy(new Color(0.08f, 0.1f, 0.15f), 0.97f);
                Piece(PrimitiveType.Cube, h, new Vector3(-0.27f, 0.06f, 0.6f), new Vector3(0.44f, 0.28f, 0.06f), black, Quaternion.Euler(0f, -12f, 0f));
                Piece(PrimitiveType.Cube, h, new Vector3(0.27f, 0.06f, 0.6f), new Vector3(0.44f, 0.28f, 0.06f), black, Quaternion.Euler(0f, 12f, 0f));
                Piece(PrimitiveType.Cube, h, new Vector3(0f, 0.12f, 0.66f), new Vector3(0.14f, 0.04f, 0.04f), black);
                break;
            case "visor":
                var visor = Clear(new Color(0.31f, 0.86f, 1f, 0.6f));
                Piece(PrimitiveType.Sphere, h, new Vector3(0f, 0.04f, 0.12f), new Vector3(1.42f, 0.36f, 1.2f), visor);
                break;
            case "3d":
                var white = Glossy(Color.white);
                Piece(PrimitiveType.Cube, h, new Vector3(-0.25f, 0.07f, 0.62f), new Vector3(0.44f, 0.3f, 0.04f), white, Quaternion.Euler(0f, -10f, 0f));
                Piece(PrimitiveType.Cube, h, new Vector3(0.25f, 0.07f, 0.62f), new Vector3(0.44f, 0.3f, 0.04f), white, Quaternion.Euler(0f, 10f, 0f));
                Piece(PrimitiveType.Cube, h, new Vector3(-0.25f, 0.07f, 0.645f), new Vector3(0.36f, 0.22f, 0.02f), Clear(new Color(0.91f, 0.2f, 0.23f, 0.85f)), Quaternion.Euler(0f, -10f, 0f));
                Piece(PrimitiveType.Cube, h, new Vector3(0.25f, 0.07f, 0.645f), new Vector3(0.36f, 0.22f, 0.02f), Clear(new Color(0.1f, 0.65f, 0.85f, 0.85f)), Quaternion.Euler(0f, 10f, 0f));
                break;
            case "heart":
                var pink = Glossy(new Color(1f, 0.37f, 0.64f));
                foreach (float x in new[] { -0.26f, 0.26f })
                {
                    var heart = Group("Corazón", h, new Vector3(x, 0.04f, 0.62f));
                    heart.localRotation = Quaternion.Euler(0f, x * 30f, 0f);
                    Piece(PrimitiveType.Sphere, heart, new Vector3(-0.08f, 0.06f, 0f), new Vector3(0.2f, 0.2f, 0.07f), pink);
                    Piece(PrimitiveType.Sphere, heart, new Vector3(0.08f, 0.06f, 0f), new Vector3(0.2f, 0.2f, 0.07f), pink);
                    Piece(PrimitiveType.Cube, heart, new Vector3(0f, -0.04f, 0f), new Vector3(0.19f, 0.19f, 0.06f), pink, Quaternion.Euler(0f, 0f, 45f));
                }
                Piece(PrimitiveType.Cube, h, new Vector3(0f, 0.1f, 0.67f), new Vector3(0.2f, 0.025f, 0.025f), Glossy(new Color(0.69f, 0.09f, 0.42f)));
                break;
            case "mono":
                var g = Gold();
                var m = MeshPiece("Monóculo", h, TorusMesh(0.19f, 0.04f, 360f), g);
                m.transform.localPosition = new Vector3(0.26f, 0.04f, 0.6f); m.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Piece(PrimitiveType.Cylinder, h, new Vector3(0.26f, 0.04f, 0.6f), new Vector3(0.34f, 0.005f, 0.34f), Clear(new Color(0.82f, 0.94f, 1f, 0.4f)), Quaternion.Euler(90f, 0f, 0f));
                Piece(PrimitiveType.Cylinder, h, new Vector3(0.43f, -0.3f, 0.5f), new Vector3(0.015f, 0.32f, 0.015f), g, Quaternion.Euler(0f, 0f, 12f));
                break;
        }
    }

    private void BuildPhones(Transform h, Color c)
    {
        var dark = Glossy(new Color(0.17f, 0.18f, 0.23f), 0.85f);
        var band = MeshPiece("Diadema", h, TorusMesh(0.72f, 0.06f, 180f), dark);
        band.transform.localRotation = Quaternion.Euler(0f, 0f, 0f) * Quaternion.Euler(-90f, 0f, 0f);   // arco por encima de la cabeza
        var cup = Glossy(c);
        Piece(PrimitiveType.Capsule, h, new Vector3(-0.7f, 0f, 0f), new Vector3(0.28f, 0.24f, 0.28f), cup, Quaternion.Euler(0f, 0f, 90f));
        Piece(PrimitiveType.Capsule, h, new Vector3(0.7f, 0f, 0f), new Vector3(0.28f, 0.24f, 0.28f), cup, Quaternion.Euler(0f, 0f, 90f));
    }

    private void BuildFlower(Transform h)
    {
        var f = Group("Flor", h, new Vector3(0.38f, 0.45f, 0.32f));
        f.localRotation = Quaternion.Euler(-30f, 30f, 0f);
        var petal = Glossy(new Color(1f, 0.62f, 0.77f));
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2f / 5f;
            Piece(PrimitiveType.Sphere, f, new Vector3(Mathf.Cos(a) * 0.11f, Mathf.Sin(a) * 0.11f, 0f), new Vector3(0.17f, 0.17f, 0.07f), petal);
        }
        Piece(PrimitiveType.Sphere, f, new Vector3(0f, 0f, 0.03f), Vector3.one * 0.12f, Glossy(new Color(1f, 0.82f, 0.25f)));
    }

    private void BuildWand(Transform hand)
    {
        var g = Gold();
        Piece(PrimitiveType.Cylinder, hand, new Vector3(0.13f, 0.35f, 0f), new Vector3(0.06f, 0.36f, 0.06f), g, Quaternion.Euler(0f, 0f, -20f));
        var ring = MeshPiece("Aro", hand, TorusMesh(0.15f, 0.035f, 360f), g);
        ring.transform.localPosition = new Vector3(0.28f, 0.82f, 0f); ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var iri = Iridescent();
        Piece(PrimitiveType.Sphere, hand, new Vector3(0.12f, 1.25f, 0f), Vector3.one * 0.24f, iri);
        Piece(PrimitiveType.Sphere, hand, new Vector3(0.42f, 1.42f, 0.05f), Vector3.one * 0.15f, iri);
    }

    // ---------------- Piezas y materiales ----------------

    private static Transform Group(string name, Transform parent, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    private static GameObject Piece(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
    {
        var go = GameObject.CreatePrimitive(type);
        DestroyImmediate(go.GetComponent<Collider>());   // al instante: así no molesta a la física ni un solo paso
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    private static GameObject MeshPiece(string name, Transform parent, Mesh mesh, Material mat)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    private static Shader litShader;

    private Material NewLit(Color c, float smoothness, float metallic)
    {
        if (!litShader) litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(litShader) { color = c };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        owned.Add(m);
        return m;
    }

    private Material Glossy(Color c, float smoothness = 0.93f) => NewLit(c, smoothness, 0f);

    private Material Gold()
    {
        var m = NewLit(new Color(1f, 0.78f, 0.2f), 0.9f, 0.85f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.25f, 0.15f, 0f));
        return m;
    }

    private Material Clear(Color c)
    {
        var m = NewLit(c, 0.97f, 0f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        return m;
    }

    // Tornasol de burbuja: para alas, aro y burbujas de la varita
    private Material Iridescent()
    {
        var m = Clear(new Color(0.8f, 0.92f, 1f, 0.7f));
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.18f, 0.12f, 0.22f));
        return m;
    }

    private Material Striped(Color c)
    {
        var m = Glossy(Color.white, 0.75f);
        m.SetTexture("_BaseMap", StripeTexture(c));
        m.SetTextureScale("_BaseMap", new Vector2(6f, 1f));
        return m;
    }

    private Texture2D StripeTexture(Color c)
    {
        var tex = new Texture2D(16, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        var light = Color.Lerp(c, Color.white, 0.75f);
        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 4; y++) tex.SetPixel(x, y, x < 8 ? c : light);
        tex.Apply();
        owned.Add(tex);
        return tex;
    }

    // ---------------- Mallas (se arman una vez y se comparten) ----------------

    private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

    // Gota: punta redondeada arriba y base ancha y plana abajo. Medidas del cuerpo del SVG (y = 0 a la altura 330 del dibujo)
    private static Mesh DropMesh() => Cached("gota", () => Lathe(new[]
    {
        new Vector2(0f, -0.14f), new Vector2(0.5f, -0.13f), new Vector2(0.72f, -0.08f), new Vector2(0.84f, 0f),
        new Vector2(0.88f, 0.12f), new Vector2(0.84f, 0.32f), new Vector2(0.75f, 0.52f), new Vector2(0.62f, 0.72f),
        new Vector2(0.46f, 0.92f), new Vector2(0.3f, 1.12f), new Vector2(0.16f, 1.3f), new Vector2(0f, 1.4f),
    }));

    // Media esfera un poco más grande que la cabeza: gorro y gorra
    private static Mesh DomeMesh() => Cached("cupula", () =>
    {
        var p = new List<Vector2>();
        for (int i = 0; i <= 12; i++)
        {
            float a = i / 12f * Mathf.PI * 0.5f;
            p.Add(new Vector2(Mathf.Cos(a) * 0.72f, Mathf.Sin(a) * 0.74f));
        }
        return Lathe(p.ToArray());
    });

    private static Mesh ConeMesh() => Cached("cono", () => Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f) }));

    // Aro con grosor (la corona): pared de afuera, borde de arriba y pared de adentro
    private static Mesh TubeMesh(float r, float h) => Cached($"tubo{r}{h}", () => Lathe(new[]
    {
        new Vector2(r, 0f), new Vector2(r, h), new Vector2(r - 0.05f, h), new Vector2(r - 0.05f, 0f),
    }));

    private static Mesh TorusMesh(float major, float minor, float arc) => Cached($"toro{major}{minor}{arc}", () =>
    {
        int segs = Mathf.Max(8, Mathf.RoundToInt(48 * arc / 360f)), rings = 12;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i <= segs; i++)
        {
            float a = Mathf.Deg2Rad * arc * i / segs;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int j = 0; j <= rings; j++)
            {
                float b = j * Mathf.PI * 2f / rings;
                var normal = d * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                v.Add(d * major + normal * minor); n.Add(normal);
            }
        }
        int ring = rings + 1;
        for (int i = 0; i < segs; i++)
            for (int j = 0; j < rings; j++)
            {
                int a0 = i * ring + j, b0 = (i + 1) * ring + j;
                t.Add(a0); t.Add(a0 + 1); t.Add(b0); t.Add(b0); t.Add(a0 + 1); t.Add(b0 + 1);
            }
        var mesh = new Mesh { name = "Toro" };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
        return mesh;
    });

    // Gira un perfil (x = radio, y = altura) alrededor del eje Y
    private static Mesh Lathe(Vector2[] profile)
    {
        const int segs = 40;
        var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i <= segs; i++)
        {
            float a = i * Mathf.PI * 2f / segs;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            foreach (var p in profile) v.Add(new Vector3(p.x * c, p.y, p.x * s));
        }
        int k = profile.Length;
        for (int i = 0; i < segs; i++)
            for (int j = 0; j < k - 1; j++)
            {
                int a0 = i * k + j, b0 = (i + 1) * k + j;
                t.Add(a0); t.Add(a0 + 1); t.Add(b0); t.Add(b0); t.Add(a0 + 1); t.Add(b0 + 1);
            }
        var mesh = new Mesh { name = "Torno" };
        mesh.SetVertices(v); mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh Cached(string key, System.Func<Mesh> make)
    {
        if (!meshes.TryGetValue(key, out var m) || !m) meshes[key] = m = make();
        return m;
    }
}
