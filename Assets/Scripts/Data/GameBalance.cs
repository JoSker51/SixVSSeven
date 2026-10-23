using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemKind { Cube, Wall, Lid, Ring, Cylinder, Sphere, Pyramid }
public enum ItemVariant { Normal, Positive, Negative, Special }

// Un artículo de la tienda (una forma + una variante). Los datos viven aquí; la lógica es la misma para todos.
[Serializable]
public class ItemDefinition
{
    public string id;
    public ItemKind kind;
    public ItemVariant variant;
    public string displayName;
    [TextArea] public string description;
    public int price;
    public int removalCost;
    public bool inShop = true;
    public GameObject prefab;
    public Sprite icon;

    public string VariantName => variant switch
    {
        ItemVariant.Positive => "Positivo",
        ItemVariant.Negative => "Negativo",
        ItemVariant.Special => "Especial",
        _ => "Normal",
    };

    public string FullName => variant == ItemVariant.Normal || variant == ItemVariant.Special ? displayName : $"{displayName} {VariantName.ToLowerInvariant()}";
}

// Todos los números del juego en un solo lugar (Assets/Resources/GameBalance.asset).
// Para equilibrar la partida se cambian aquí, sin tocar código.
[CreateAssetMenu(menuName = "Catapultas/Game Balance")]
public class GameBalance : ScriptableObject
{
    [Header("Partida")]
    public int startingCoins = 5;
    public int defaultWinCoins = 100;
    [Tooltip("0 = una distribución distinta en cada partida")]
    public int randomSeed = 0;

    [Header("Arena circular")]
    public float arenaRadius = 27f;
    public float castleRingRadius = 21f;     // distancia del centro a cada castillo
    public float catapultRingRadius = 14.5f; // distancia del centro a cada catapulta
    public float castleKeepOut = 5.4f;       // nada se genera ni coloca tan cerca de un castillo
    public float catapultKeepOut = 4.5f;

    [Header("Burbujas")]
    public int bubblesPerSection = 4;
    public Vector2 bubbleRadius = new Vector2(0.55f, 1.1f);
    public Vector2 bubbleHeight = new Vector2(1.3f, 5.5f);
    public float bubbleMinGap = 1.6f;         // espacio libre mínimo entre dos burbujas
    public float bubbleWallMargin = 1.5f;
    public float bubbleCenterMargin = 2f;
    public float bubbleObjectClearance = 0.6f;
    public int bubbleMaxAttempts = 80;
    [Tooltip("Monedas = máx(1, redondear(base × tamaño × (1 + cercanía al centro)))")]
    public int bubbleBaseValue = 2;
    public float bubbleReferenceRadius = 0.8f;

    [Header("Burbuja de castillo (robo)")]
    public float castleBubbleRadius = 1.3f;
    public float castleBubbleHeight = 6.4f;
    public int stealAmount = 3;
    public int maxStealPerShot = 10;

    [Header("Recompensas de objetos")]
    public int positiveBonus = 2;
    public int negativePenalty = 2;
    public int ringMultiplier = 2;

    [Header("Tienda")]
    public int shopOffers = 3;
    public int refreshCost = 1;
    public int refreshesPerTurn = 1;

    [Header("Colocación")]
    public float placementMaxHeight = 6f;
    public float placementClearance = 0.15f;
    public float rotationStep = 15f;
    public float heightStep = 0.25f;

    [Header("Física de los objetos")]
    public float objectBounciness = 0.45f;
    public float objectFriction = 0.3f;

    [Header("Catálogo (la pool de la tienda)")]
    public List<ItemDefinition> items = new List<ItemDefinition>();

    private static GameBalance current;

    public static GameBalance Current
    {
        get
        {
            if (current == null) current = Resources.Load<GameBalance>("GameBalance");
            if (current == null)
            {
                Debug.LogWarning("[Catapultas] Falta Assets/Resources/GameBalance.asset; se usan valores por defecto.");
                current = CreateInstance<GameBalance>();
            }
            return current;
        }
    }

    public ItemDefinition Find(string id) => items.Find(i => i.id == id);

    public IEnumerable<ItemDefinition> ShopPool
    {
        get { foreach (var i in items) if (i.inShop && i.prefab != null) yield return i; }
    }
}
