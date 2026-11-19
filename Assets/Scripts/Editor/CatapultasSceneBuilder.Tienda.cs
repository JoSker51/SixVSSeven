using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Parte del constructor de escenas para la tienda y los objetos colocables:
//  - 31 prefabs: cubo, pared, rampa, cilindro, esfera y pirámide en goma/turbo/bumper (los normales) y en
//    positivo/negativo (lisos), y el aro multiplicador
//  - sus íconos (se renderizan los modelos reales a PNG)
//  - el catálogo con precios en GameBalance (si un precio ya existe en el asset, se respeta)
//  - la interfaz: tienda, inventario, botones de fase y confirmación de retirada
public static partial class CatapultasSceneBuilder
{
    private const string ItemFolder = PrefabFolder + "/Objetos";
    private const string IconFolder = "Assets/Arte/Iconos";

    private static Material normalMat, positiveMat, negativeMat, ringMat, plusMat, minusMat, ghostValidMat, ghostInvalidMat, haloMat;
    private static PhysicsMaterial bouncyMaterial;
    private static readonly Dictionary<ItemMaterial, (Material look, Material marker, PhysicsMaterial physics)> materialKit =
        new Dictionary<ItemMaterial, (Material, Material, PhysicsMaterial)>();
    private static Mesh bumperMarkerMesh;

    // Los normales vienen en un material: (material, nombre, cuánto más cuestan comprar y retirar que el liso, qué hace)
    private static readonly (ItemMaterial material, string suffix, int extraPrice, int extraRemoval, string effect)[] Materials =
    {
        (ItemMaterial.Rubber, " de goma", 1, 1, "La pelota rebota mucho más fuerte."),
        (ItemMaterial.Turbo, " turbo", 2, 2, "La pelota sale acelerada."),
        (ItemMaterial.Bumper, " bumper", 2, 2, "La empuja lejos, como un pinball."),
    };
    private static Mesh rampMesh;

    // Formas: nombre, descripción y precios iniciales (normal, positivo, negativo) y costo de retirada (normal, pos, neg)
    private struct Shape
    {
        public ItemKind kind; public string name; public string description;
        public int normal, positive, negative, removeNormal, removePositive, removeNegative;
    }

    private static readonly Shape[] Shapes =
    {
        new Shape { kind = ItemKind.Cube, name = "Cubo", description = "Bloque sólido que desvía la pelota.", normal = 3, positive = 8, negative = 6, removeNormal = 8, removePositive = 12, removeNegative = 10 },
        new Shape { kind = ItemKind.Wall, name = "Pared", description = "Rectángulo largo para bloquear o hacer rebotar tiros.", normal = 4, positive = 9, negative = 7, removeNormal = 9, removePositive = 13, removeNegative = 11 },
        new Shape { kind = ItemKind.Ramp, name = "Rampa", description = "Cuña inclinada que lanza la pelota hacia arriba.", normal = 3, positive = 8, negative = 6, removeNormal = 8, removePositive = 12, removeNegative = 10 },
        new Shape { kind = ItemKind.Cylinder, name = "Cilindro", description = "Columna larga; acostada hace de rodillo.", normal = 3, positive = 8, negative = 6, removeNormal = 8, removePositive = 12, removeNegative = 10 },
        new Shape { kind = ItemKind.Sphere, name = "Esfera", description = "Bola brillante con rebotes impredecibles.", normal = 3, positive = 8, negative = 6, removeNormal = 8, removePositive = 12, removeNegative = 10 },
        new Shape { kind = ItemKind.Pyramid, name = "Pirámide", description = "Sus caras inclinadas mandan la pelota de lado.", normal = 3, positive = 8, negative = 6, removeNormal = 8, removePositive = 12, removeNegative = 10 },
    };

    private const int RingPrice = 15, RingRemoval = 18;
    private const float RingMajor = 2.0f, RingMinor = 0.2f;
    private const float RampWidth = 2.6f, RampLength = 4.4f, RampHeight = 2.0f;   // unos 24° de pendiente: rodando rápido, la pelota salta a ~3 m

    static partial void BuildShopAssets()
    {
        EnsureFolder(PrefabFolder, "Objetos");
        EnsureFolder("Assets/Arte", "Iconos");
        SetupItemMaterials();
        var torus = SaveMesh(ProceduralMesh.Torus(RingMajor, RingMinor, 64, 16, "AroObjeto"), "AroObjeto");
        bumperMarkerMesh = SaveMesh(ProceduralMesh.Torus(0.34f, 0.09f, 32, 10, "AroBumper"), "AroBumper");
        var pyramid = SaveMesh(ProceduralMesh.Pyramid(2.4f, 2.1f), "Piramide");
        rampMesh = SaveMesh(ProceduralMesh.Ramp(RampWidth, RampLength, RampHeight), "Rampa");
        RemoveOldLidAssets();
        RemoveOldPlainNormals();

        var bal = balance;
        var b = GameBalance.Current;   // valores de efectos (por si alguien cambió +2/−2/×2)
        var oldItems = new Dictionary<string, ItemDefinition>();
        foreach (var i in bal.items) if (i != null && !string.IsNullOrEmpty(i.id)) oldItems[i.id] = i;
        var items = new List<ItemDefinition>();

        foreach (var s in Shapes)
        {
            // Normales: uno por material
            foreach (var m in Materials)
            {
                string id = $"{s.kind}_{m.material}".ToLowerInvariant();
                var prefab = BuildItemPrefab(id, s.kind, ItemVariant.Normal, m.material, torus, pyramid);
                var item = MakeItem(oldItems, id, s.kind, ItemVariant.Normal, s.name + m.suffix, $"{s.description} {m.effect}",
                                    s.normal + m.extraPrice, s.removeNormal + m.extraRemoval, prefab);
                item.material = m.material;
                if (!oldItems.ContainsKey(id)) item.shopWeight = 0.5f;
                items.Add(item);
            }
            // Positivos y negativos: lisos
            foreach (var v in new[] { ItemVariant.Positive, ItemVariant.Negative })
            {
                string id = $"{s.kind}_{v}".ToLowerInvariant();
                var prefab = BuildItemPrefab(id, s.kind, v, ItemMaterial.Plain, torus, pyramid);
                string effect = v == ItemVariant.Positive
                    ? $" +{b.positiveBonus} si lo toca y revientas burbujas."   // corto: cabe en la tarjeta
                    : $" −{b.negativePenalty} si lo toca y revientas burbujas.";
                int price = v == ItemVariant.Positive ? s.positive : s.negative;
                int removal = v == ItemVariant.Positive ? s.removePositive : s.removeNegative;
                items.Add(MakeItem(oldItems, id, s.kind, v, s.name, s.description + effect, price, removal, prefab));
            }
        }

        var ring = BuildItemPrefab("ring_special", ItemKind.Ring, ItemVariant.Special, ItemMaterial.Plain, torus, pyramid);
        items.Add(MakeItem(oldItems, "ring_special", ItemKind.Ring, ItemVariant.Special, "Aro multiplicador",
            $"Cada pasada por un aro multiplica las monedas de burbujas ×{b.ringMultiplier}: se apilan (×4, ×8...).",
            RingPrice, RingRemoval, ring));

        RenderIcons(items);
        bal.items = items;
        EditorUtility.SetDirty(bal);
    }

    // Si el asset ya tenía el objeto, se conservan su precio, costo de retirada y si está en la tienda
    private static ItemDefinition MakeItem(Dictionary<string, ItemDefinition> old, string id, ItemKind kind, ItemVariant variant,
                                           string name, string description, int price, int removal, GameObject prefab)
    {
        var item = new ItemDefinition { id = id, kind = kind, variant = variant, displayName = name, description = description, price = price, removalCost = removal, inShop = true };
        if (old.TryGetValue(id, out var o))
        {
            item.price = o.price;
            item.removalCost = o.removalCost;
            item.inShop = o.inShop;
            item.shopWeight = o.shopWeight;
        }
        item.prefab = prefab;
        return item;
    }

    private static GameObject BuildItemPrefab(string id, ItemKind kind, ItemVariant variant, ItemMaterial material, Mesh torus, Mesh pyramid)
    {
        var root = new GameObject(id);
        bool hasKit = materialKit.TryGetValue(material, out var kit);
        Material mat = hasKit ? kit.look : variant switch
        {
            ItemVariant.Positive => positiveMat,
            ItemVariant.Negative => negativeMat,
            ItemVariant.Special => ringMat,
            _ => normalMat,
        };

        GameObject shape;
        float top;
        switch (kind)
        {
            case ItemKind.Wall:
                shape = Prim(PrimitiveType.Cube, "Forma", root.transform, new Vector3(0f, 1f, 0f), new Vector3(5f, 2f, 0.5f), mat); top = 2f; break;
            case ItemKind.Ramp:
                shape = MeshObject("Forma", root.transform, rampMesh, mat, false);
                ConvexCollider(shape); top = RampHeight; break;
            case ItemKind.Cylinder:
                shape = Prim(PrimitiveType.Cylinder, "Forma", root.transform, new Vector3(0f, 2.1f, 0f), new Vector3(1.1f, 2.1f, 1.1f), mat, false);
                ConvexCollider(shape); top = 4.2f; break;
            case ItemKind.Sphere:
                shape = Prim(PrimitiveType.Sphere, "Forma", root.transform, new Vector3(0f, 1.05f, 0f), Vector3.one * 2.1f, mat); top = 2.1f; break;
            case ItemKind.Pyramid:
                shape = MeshObject("Forma", root.transform, pyramid, mat, false);
                ConvexCollider(shape); top = 2.1f; break;
            case ItemKind.Ring:
                shape = MeshObject("Forma", root.transform, torus, mat, true);   // toro cóncavo: colisión exacta con el borde
                top = RingMinor; break;
            default:
                shape = Prim(PrimitiveType.Cube, "Forma", root.transform, new Vector3(0f, 0.95f, 0f), Vector3.one * 1.9f, mat); top = 1.9f; break;
        }
        foreach (var c in shape.GetComponents<Collider>()) c.sharedMaterial = hasKit ? kit.physics : bouncyMaterial;
        if (hasKit) AddMaterialMarker(root.transform, material, kit.marker, top);

        // Símbolo que flota encima: "+" en los positivos, "−" en los negativos (sin colisión)
        if (variant == ItemVariant.Positive || variant == ItemVariant.Negative)
        {
            var symbol = new GameObject("Simbolo").transform;
            symbol.SetParent(root.transform, false);
            symbol.localPosition = Vector3.up * (top + 0.6f);
            var smat = variant == ItemVariant.Positive ? plusMat : minusMat;
            Prim(PrimitiveType.Cube, "Barra", symbol, Vector3.zero, new Vector3(0.6f, 0.15f, 0.15f), smat, false);
            if (variant == ItemVariant.Positive) Prim(PrimitiveType.Cube, "Barra", symbol, Vector3.zero, new Vector3(0.15f, 0.6f, 0.15f), smat, false);
            symbol.gameObject.AddComponent<MarkerSpin>();
        }

        var obj = root.AddComponent<ArenaObject>();
        Set(obj, ("itemId", id), ("ringInnerRadius", RingMajor - RingMinor));
        string path = $"{ItemFolder}/{id}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return Load<GameObject>(path);
    }

    // Símbolo del material que flota encima (sin colisión): pelotita de goma que salta, doble flecha turbo, aro bumper
    private static void AddMaterialMarker(Transform root, ItemMaterial material, Material mat, float top)
    {
        var symbol = new GameObject("Simbolo").transform;
        symbol.SetParent(root, false);
        symbol.localPosition = Vector3.up * (top + 0.7f);
        var spin = symbol.gameObject.AddComponent<MarkerSpin>();
        switch (material)
        {
            case ItemMaterial.Rubber:
                Prim(PrimitiveType.Sphere, "Pelotita", symbol, Vector3.zero, Vector3.one * 0.42f, mat, false);
                Set(spin, ("bobHeight", 0.35f), ("bobSpeed", 7f));
                break;
            case ItemMaterial.Turbo:
                for (int k = 0; k < 2; k++)   // dos flechas seguidas, como »
                {
                    float x = -0.16f + k * 0.3f;
                    Prim(PrimitiveType.Cube, "Barra", symbol, new Vector3(x, 0.11f, 0f), new Vector3(0.34f, 0.1f, 0.1f), mat, false, Quaternion.Euler(0f, 0f, -40f));
                    Prim(PrimitiveType.Cube, "Barra", symbol, new Vector3(x, -0.11f, 0f), new Vector3(0.34f, 0.1f, 0.1f), mat, false, Quaternion.Euler(0f, 0f, 40f));
                }
                Set(spin, ("spinSpeed", 220f));
                break;
            case ItemMaterial.Bumper:
                var ringGo = MeshObject("Aro", symbol, bumperMarkerMesh, mat, false);
                ringGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Set(spin, ("spinSpeed", 160f));
                break;
        }
    }

    // Los normales lisos (cube_normal...) pasaron a tener material: se borran sus prefabs e íconos viejos
    private static void RemoveOldPlainNormals()
    {
        foreach (var s in Shapes)
        {
            string id = $"{s.kind}_normal".ToLowerInvariant();
            AssetDatabase.DeleteAsset($"{ItemFolder}/{id}.prefab");
            AssetDatabase.DeleteAsset($"{IconFolder}/{id}.png");
        }
    }

    // Rayas diagonales blancas y oscuras: el color naranja del material las tiñe
    private static Texture2D StripeTexture()
    {
        const string path = MatFolder + "/RayasTurbo.asset";
        var tex = Load<Texture2D>(path);
        if (tex != null) return tex;
        const int size = 128;
        tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, name = "RayasTurbo" };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = ((x + y) % 64) / 64f;   // dos rayas por cuadro
                float edge = Mathf.Clamp01((u - 0.46f) / 0.06f) * (1f - Mathf.Clamp01((u - 0.94f) / 0.06f));   // borde suave entre rayas
                px[y * size + x] = Color.Lerp(Color.white, new Color(0.22f, 0.12f, 0.1f), edge);
            }
        tex.SetPixels(px);
        tex.Apply(true);
        AssetDatabase.CreateAsset(tex, path);
        return tex;
    }

    private static PhysicsMaterial PhysicsAsset(string name, float bounciness, float friction)
    {
        string path = MatFolder + $"/{name}.asset";
        var pm = Load<PhysicsMaterial>(path);
        if (pm == null)
        {
            pm = new PhysicsMaterial(name);
            AssetDatabase.CreateAsset(pm, path);
        }
        pm.bounciness = bounciness;
        pm.dynamicFriction = friction;
        pm.staticFriction = friction;
        pm.bounceCombine = PhysicsMaterialCombine.Maximum;
        pm.frictionCombine = PhysicsMaterialCombine.Minimum;
        EditorUtility.SetDirty(pm);
        return pm;
    }

    // La rampa reemplazó a la tapa: se borran sus prefabs e íconos si quedaron de antes
    private static void RemoveOldLidAssets()
    {
        foreach (var v in new[] { "normal", "positive", "negative" })
        {
            AssetDatabase.DeleteAsset($"{ItemFolder}/lid_{v}.prefab");
            AssetDatabase.DeleteAsset($"{IconFolder}/lid_{v}.png");
        }
    }

    private static void ConvexCollider(GameObject go)
    {
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        mc.convex = true;
    }

    private static void SetupItemMaterials()
    {
        normalMat = Lit("ObjetoPerla", new Color(0.93f, 0.96f, 1f), 0.92f, 0.05f);
        positiveMat = Lit("ObjetoEsmeralda", new Color(0.22f, 0.84f, 0.52f), 0.93f, 0f, new Color(0.05f, 0.35f, 0.18f));
        negativeMat = Lit("ObjetoObsidiana", new Color(0.13f, 0.15f, 0.24f), 0.95f, 0.1f, new Color(0.06f, 0.01f, 0.04f));   // oscuro, sin confundirse con el magenta del J4
        ringMat = Lit("ObjetoOro", new Color(1f, 0.8f, 0.32f), 0.93f, 0.75f, new Color(0.4f, 0.28f, 0.04f));
        plusMat = Lit("SimboloMas", new Color(0.85f, 1f, 0.9f), 0.9f, 0f, new Color(0.45f, 1.4f, 0.7f));
        minusMat = Lit("SimboloMenos", new Color(1f, 0.8f, 0.86f), 0.9f, 0f, new Color(1.4f, 0.3f, 0.5f));
        ghostValidMat = Transparent(Lit("FantasmaValido", new Color(0.55f, 0.92f, 1f, 0.42f), 0.95f));
        ghostInvalidMat = Transparent(Lit("FantasmaInvalido", new Color(1f, 0.42f, 0.55f, 0.42f), 0.95f));
        haloMat = Transparent(Lit("SeleccionHalo", new Color(1f, 1f, 1f, 0.28f), 0.95f, 0f, new Color(0.35f, 0.55f, 0.75f)));

        string path = MatFolder + "/ObjetoRebote.asset";
        bouncyMaterial = Load<PhysicsMaterial>(path);
        if (bouncyMaterial == null)
        {
            bouncyMaterial = new PhysicsMaterial("ObjetoRebote");
            AssetDatabase.CreateAsset(bouncyMaterial, path);
        }
        bouncyMaterial.bounciness = balance.objectBounciness;
        bouncyMaterial.dynamicFriction = balance.objectFriction;
        bouncyMaterial.staticFriction = balance.objectFriction;
        bouncyMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;
        EditorUtility.SetDirty(bouncyMaterial);

        // Materiales: cómo se ven, su símbolo y su física (el empujón extra lo da BallMaterials en código)
        // Goma: gelatina translúcida (así no se confunde con el castillo magenta)
        materialKit[ItemMaterial.Rubber] = (
            Transparent(Lit("MaterialGoma", new Color(1f, 0.45f, 0.75f, 0.82f), 0.98f, 0f, new Color(0.22f, 0.02f, 0.12f))),
            Lit("SimboloGoma", new Color(1f, 0.6f, 0.82f), 0.95f, 0f, new Color(1.2f, 0.3f, 0.7f)),
            PhysicsAsset("FisicaGoma", 1f, 0.1f));
        // Turbo: rayas de advertencia naranjas y oscuras (así no se confunde con el castillo amarillo)
        var turboLook = Lit("MaterialTurbo", new Color(1f, 0.52f, 0.1f), 0.9f, 0.2f, new Color(0.3f, 0.08f, 0f));
        turboLook.SetTexture("_BaseMap", StripeTexture());
        turboLook.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
        materialKit[ItemMaterial.Turbo] = (
            turboLook,
            Lit("SimboloTurbo", new Color(1f, 0.85f, 0.3f), 0.9f, 0f, new Color(1.6f, 0.8f, 0.1f)),
            PhysicsAsset("FisicaTurbo", 0.6f, 0f));
        materialKit[ItemMaterial.Bumper] = (
            Lit("MaterialBumper", new Color(0.92f, 0.95f, 1f), 0.93f, 1f, new Color(0.03f, 0.16f, 0.24f)),
            Lit("SimboloBumper", new Color(0.55f, 0.95f, 1f), 0.9f, 0f, new Color(0.3f, 1.3f, 1.6f)),
            PhysicsAsset("FisicaBumper", 0.9f, 0.1f));
    }

    // Íconos de la tienda con los modelos reales. Se renderiza sobre negro y sobre blanco
    // para sacar la transparencia exacta (funciona aunque la cámara no escriba el canal alfa).
    private static void RenderIcons(List<ItemDefinition> items)
    {
        const int size = 256;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.45f, 0.55f, 0.7f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.58f, 0.68f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.34f, 0.4f);

        var studio = new GameObject("Estudio de íconos").transform;
        studio.position = new Vector3(0f, -500f, 0f);
        var light = new GameObject("Luz").AddComponent<Light>();
        light.transform.SetParent(studio, false);
        light.type = LightType.Directional;
        light.intensity = 1.05f;
        light.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
        var cam = new GameObject("Camara").AddComponent<Camera>();
        cam.transform.SetParent(studio, false);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        var black = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var white = new Texture2D(size, size, TextureFormat.RGBA32, false);

        foreach (var item in items)
        {
            var go = Object.Instantiate(item.prefab, studio);
            go.transform.localRotation = item.kind switch
            {
                ItemKind.Ring => Quaternion.Euler(70f, 25f, 0f),
                ItemKind.Ramp => Quaternion.Euler(0f, -35f, 0f),   // se ve el lado triangular en sombra: si no, la rampa blanca se pierde
                _ => Quaternion.Euler(0f, 30f, 0f),
            };
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            float radius = bounds.extents.magnitude;
            Vector3 dir = Quaternion.Euler(22f, 0f, 0f) * Vector3.forward;   // desde arriba y adelante, mirando un poco hacia abajo
            cam.transform.position = bounds.center - dir * (radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f);
            cam.transform.rotation = Quaternion.LookRotation(dir);

            Grab(cam, rt, Color.black, black);
            Grab(cam, rt, Color.white, white);
            var px = new Color[size * size];
            var pb = black.GetPixels();
            var pw = white.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                float a = Mathf.Clamp01(1f - ((pw[i].r - pb[i].r) + (pw[i].g - pb[i].g) + (pw[i].b - pb[i].b)) / 3f);
                px[i] = a > 0.004f ? new Color(pb[i].r / a, pb[i].g / a, pb[i].b / a, a) : Color.clear;
            }
            var icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
            icon.SetPixels(px);
            icon.Apply();
            string path = $"{IconFolder}/{item.id}.png";
            File.WriteAllBytes(path, icon.EncodeToPNG());
            Object.DestroyImmediate(icon);
            Object.DestroyImmediate(go);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            item.icon = Load<Sprite>(path);
        }

        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(black);
        Object.DestroyImmediate(white);
        Object.DestroyImmediate(studio.gameObject);
    }

    private static void Grab(Camera cam, RenderTexture rt, Color background, Texture2D into)
    {
        cam.backgroundColor = background;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        into.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        into.Apply();
        RenderTexture.active = prev;
    }

    // =====================================================================
    // Interfaz de la tienda, inventario, fases y retirada
    // =====================================================================

    static partial void BuildPreparationUI(Transform root, GameHUD hud, TurnManager turnManager)
    {
        Set(turnManager, ("preparationPhases", true));
        new GameObject("ShopManager").AddComponent<ShopManager>();
        var placement = new GameObject("PlacementController").AddComponent<PlacementController>();
        Set(placement, ("ghostValid", ghostValidMat), ("ghostInvalid", ghostInvalidMat), ("selectionHalo", haloMat));

        // --- Inventario (Colocación) ---
        var invRoot = AeroUI.Node("Inventario", root).At(new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1000f, 150f));
        var invGroup = invRoot.gameObject.AddComponent<CanvasGroup>();
        invGroup.alpha = 0f;
        AeroUI.Img(invRoot, "Vidrio", skin.glassPanel, Color.white, 1.4f).rectTransform.Stretch();
        AeroUI.Label(invRoot, "Titulo", "Tu inventario", 20, Navy, AeroWeight.Bold, TextAnchor.UpperLeft)
            .rectTransform.At(new Vector2(0f, 1f), new Vector2(22f, -10f), new Vector2(300f, 28f));
        var slots = AeroUI.Node("Objetos", invRoot).At(new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(960f, 112f));
        var empty = AeroUI.Label(invRoot, "Vacio", "No tienes objetos: cómpralos en la tienda durante tu fase de Compra", 22, new Color(Navy.r, Navy.g, Navy.b, 0.7f), AeroWeight.Light);
        empty.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(940f, 40f));
        var infoRoot = AeroUI.Img(root, "Info colocacion", skin.glassPanel, Color.white, 2.4f);
        infoRoot.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1000f, 50f));
        var infoGroup = infoRoot.gameObject.AddComponent<CanvasGroup>();
        infoGroup.alpha = 0f;
        infoGroup.blocksRaycasts = false;
        var info = AeroUI.Label(infoRoot.transform, "Texto", "", 22, Navy);
        info.rectTransform.Stretch(8f);
        Set(invRoot.gameObject.AddComponent<InventoryBar>(),
            ("skin", skin), ("group", invGroup), ("slotsRoot", slots), ("emptyText", empty), ("infoGroup", infoGroup), ("infoText", info));

        // --- Tienda ---
        var shopRoot = AeroUI.Node("Tienda", root).At(new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1260f, 740f));
        shopRoot.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        var shopFade = shopRoot.gameObject.AddComponent<UIFadePanel>();
        Set(shopFade, ("startVisible", false), ("duration", 0.2f));
        AeroUI.Img(shopRoot, "Vidrio", skin.glassPanel, Color.white).rectTransform.Stretch();
        var shopTitle = AeroUI.Label(shopRoot, "Titulo", "Tienda", 56, Navy, AeroWeight.Bold, TextAnchor.MiddleLeft);
        shopTitle.rectTransform.At(new Vector2(0f, 1f), new Vector2(44f, -18f), new Vector2(400f, 72f));
        AeroUI.Gradient(shopTitle, new Color(0.15f, 0.55f, 0.9f), Navy);
        AeroUI.Effect<Shadow>(shopTitle, new Color(1f, 1f, 1f, 0.9f), new Vector2(0f, -2f));
        var coinsPill = AeroUI.Img(shopRoot, "Saldo", skin.buttonGold, Color.white, 160f / 60f);
        coinsPill.rectTransform.At(new Vector2(1f, 1f), new Vector2(-120f, -26f), new Vector2(320f, 60f));
        var coins = AeroUI.Label(coinsPill.transform, "Texto", "Tus monedas: 0", 28, Navy, AeroWeight.Regular);
        coins.rectTransform.Stretch();
        var close = AeroUI.Orb(shopRoot, "Cerrar", skin, new Color(0.85f, 0.9f, 0.95f), 64f);
        close.rectTransform.At(new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(64f, 64f));
        close.raycastTarget = true;
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close;
        close.gameObject.AddComponent<AeroButtonFX>();
        AeroUI.Label(close.transform, "X", "×", 40, Navy, AeroWeight.Bold).rectTransform.Stretch();

        var cards = new ShopPanel.Card[3];
        for (int i = 0; i < cards.Length; i++) cards[i] = BuildCard(shopRoot, i);

        var refresh = AeroUI.Button(shopRoot, "Actualizar", "Actualizar ofertas (1 moneda)", skin.buttonBlue, new Vector2(520f, 78f), Color.white, 30);
        ((RectTransform)refresh.transform).At(new Vector2(0f, 0f), new Vector2(44f, 28f), new Vector2(520f, 78f));
        var refreshText = refresh.GetComponentInChildren<Text>();
        var message = AeroUI.Label(shopRoot, "Mensaje", "", 24, Navy, AeroWeight.Regular, TextAnchor.MiddleRight);
        message.rectTransform.At(new Vector2(1f, 0f), new Vector2(-44f, 46f), new Vector2(620f, 44f));
        var shopPanel = shopRoot.gameObject.AddComponent<ShopPanel>();
        Set(shopPanel, ("skin", skin), ("panel", shopFade), ("coinsText", coins), ("refreshButton", refresh), ("refreshText", refreshText),
                       ("closeButton", closeButton), ("messageText", message));
        var so = new SerializedObject(shopPanel);
        var list = so.FindProperty("cards");
        list.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            var c = cards[i];
            e.FindPropertyRelative("group").objectReferenceValue = c.group;
            e.FindPropertyRelative("icon").objectReferenceValue = c.icon;
            e.FindPropertyRelative("title").objectReferenceValue = c.title;
            e.FindPropertyRelative("badge").objectReferenceValue = c.badge;
            e.FindPropertyRelative("badgeText").objectReferenceValue = c.badgeText;
            e.FindPropertyRelative("description").objectReferenceValue = c.description;
            e.FindPropertyRelative("price").objectReferenceValue = c.price;
            e.FindPropertyRelative("buy").objectReferenceValue = c.buy;
            e.FindPropertyRelative("buyText").objectReferenceValue = c.buyText;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        // --- Botones de fase ---
        var bar = AeroUI.Node("Fases (botones)", root).Stretch();
        var shopActions = AeroUI.Node("Compra", bar).Stretch();
        var shopGroup = shopActions.gameObject.AddComponent<CanvasGroup>();
        shopGroup.alpha = 0f;
        var openShop = AeroUI.Button(shopActions, "Abrir tienda", "Tienda", skin.buttonBlue, new Vector2(320f, 96f), Color.white, 44);
        ((RectTransform)openShop.transform).At(new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(320f, 96f));
        var cont = AeroUI.Button(shopActions, "Continuar", "Continuar  ▸", skin.buttonGreen, new Vector2(330f, 96f), Color.white, 40);
        ((RectTransform)cont.transform).At(new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(330f, 96f));
        var placeActions = AeroUI.Node("Colocacion", bar).Stretch();
        var placeGroup = placeActions.gameObject.AddComponent<CanvasGroup>();
        placeGroup.alpha = 0f;
        var launch = AeroUI.Button(placeActions, "A lanzar", "¡A lanzar!  ▸", skin.buttonGreen, new Vector2(330f, 96f), Color.white, 40);
        ((RectTransform)launch.transform).At(new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(330f, 96f));
        Set(bar.gameObject.AddComponent<PhaseBar>(),
            ("shopActions", shopGroup), ("openShopButton", openShop), ("continueButton", cont),
            ("placeActions", placeGroup), ("launchButton", launch), ("shop", shopPanel));

        // --- Confirmar retirada ---
        var rem = AeroUI.Node("Retirar", root).At(new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(640f, 360f));
        rem.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        var remFade = rem.gameObject.AddComponent<UIFadePanel>();
        Set(remFade, ("startVisible", false), ("duration", 0.15f));
        AeroUI.Img(rem, "Vidrio", skin.glassPanel, Color.white).rectTransform.Stretch();
        var remTitle = AeroUI.Label(rem, "Titulo", "¿Retirar?", 40, Navy, AeroWeight.Bold);
        remTitle.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(600f, 56f));
        var remDetail = AeroUI.Label(rem, "Detalle", "", 26, Navy, AeroWeight.Regular);
        remDetail.lineSpacing = 1.1f;
        remDetail.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(600f, 130f));
        var remConfirm = AeroUI.Button(rem, "Confirmar", "Retirar", skin.buttonBlue, new Vector2(290f, 84f), Color.white, 32);
        ((RectTransform)remConfirm.transform).At(new Vector2(0.5f, 0f), new Vector2(-155f, 30f), new Vector2(290f, 84f));
        var remCancel = AeroUI.Button(rem, "Cancelar", "Cancelar", skin.buttonSilver, new Vector2(250f, 84f), Navy, 32);
        ((RectTransform)remCancel.transform).At(new Vector2(0.5f, 0f), new Vector2(160f, 30f), new Vector2(250f, 84f));
        Set(rem.gameObject.AddComponent<RemovalPrompt>(),
            ("panel", remFade), ("title", remTitle), ("detail", remDetail),
            ("confirm", remConfirm), ("confirmText", remConfirm.GetComponentInChildren<Text>()), ("cancel", remCancel));
    }

    private static ShopPanel.Card BuildCard(RectTransform parent, int index)
    {
        var card = AeroUI.Img(parent, $"Oferta {index + 1}", skin.glassPanel, Color.white, 1.2f);
        card.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2((index - 1) * 390f, 8f), new Vector2(360f, 500f));
        var group = card.gameObject.AddComponent<CanvasGroup>();
        AeroUI.Img(card.transform, "Resplandor", skin.glow, new Color(0.8f, 0.95f, 1f, 0.9f)).rectTransform
            .At(new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(240f, 210f));
        var icon = AeroUI.Img(card.transform, "Icono", null, Color.white);
        icon.preserveAspect = true;
        icon.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(180f, 180f));
        var title = AeroUI.Label(card.transform, "Nombre", "Cubo", 32, Navy, AeroWeight.Bold);
        title.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -198f), new Vector2(330f, 42f));
        var badge = AeroUI.Img(card.transform, "Variante", skin.buttonSilver, Color.white, 160f / 40f);
        badge.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -244f), new Vector2(210f, 40f));
        var badgeText = AeroUI.Label(badge.transform, "Texto", "Normal", 20, Navy, AeroWeight.Bold);
        badgeText.rectTransform.Stretch();
        var desc = AeroUI.Label(card.transform, "Descripcion", "", 19, Navy, AeroWeight.Regular, TextAnchor.UpperCenter);
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        desc.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -292f), new Vector2(320f, 78f));
        var price = AeroUI.Label(card.transform, "Precio", "3 monedas", 28, Navy, AeroWeight.Regular);
        price.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -372f), new Vector2(320f, 36f));
        var buy = AeroUI.Button(card.transform, "Comprar", "Comprar", skin.buttonGreen, new Vector2(270f, 72f), Color.white, 30);
        ((RectTransform)buy.transform).At(new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(270f, 72f));
        return new ShopPanel.Card
        {
            group = group, icon = icon, title = title, badge = badge, badgeText = badgeText,
            description = desc, price = price, buy = buy, buyText = buy.GetComponentInChildren<Text>(),
        };
    }
}
