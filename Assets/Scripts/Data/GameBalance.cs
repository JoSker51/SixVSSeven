using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemKind { Cube, Wall, Ramp, Ring, Cylinder, Sphere, Pyramid }
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
    [Tooltip("Meta elegida la primera vez en «Nueva partida»; después se recuerda la última")]
    public int defaultWinCoins = 100;
    [Tooltip("Metas que se pueden elegir al empezar una partida (de menor a mayor; la última es el tope)")]
    public int[] winCoinChoices = { 50, 100, 150, 200, 300, 400, 500, 750, 1000, 1500, 2000, 2500, 3000, 4000, 5000 };
    [Tooltip("0 = una distribución distinta en cada partida")]
    public int randomSeed = 0;

    [Header("Pelota")]
    [Tooltip("Multiplica la velocidad de la pelota. La gravedad del tiro sube con su cuadrado, así llega a las mismas distancias pero vuela más rápido (1 = como antes).")]
    public float projectileSpeed = 1.35f;
    public float ProjectileGravityScale => projectileSpeed * projectileSpeed;

    [Header("Arena circular")]
    public float arenaRadius = 27f;
    public float castleRingRadius = 21f;     // distancia del centro a cada castillo
    public float catapultRingRadius = 14.5f; // distancia del centro a cada catapulta
    public float castleKeepOut = 5.4f;       // nada se genera ni coloca tan cerca de un castillo
    public float catapultKeepOut = 4.5f;

    [Header("Burbujas")]
    public int bubblesPerSection = 16;
    public Vector2 bubbleRadius = new Vector2(0.55f, 1.1f);
    public Vector2 bubbleHeight = new Vector2(1.3f, 5.5f);
    public float bubbleMinGap = 0.8f;         // espacio libre mínimo entre dos burbujas
    public float bubbleWallMargin = 1.5f;
    public float bubbleCenterMargin = 2f;
    public float bubbleObjectClearance = 0.6f;
    public int bubbleMaxAttempts = 250;
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
    [Tooltip("Cada pasada por un aro multiplica las monedas de burbujas por esto, y se apila: 2 pasadas ×4, 3 ×8...")]
    public int ringMultiplier = 2;

    [Header("Tienda")]
    public int shopOffers = 3;
    public int refreshCost = 1;
    public int refreshesPerTurn = 1;

    [Header("Colocación")]
    [Tooltip("Cuánto se puede subir un objeto (Shift + ruedita) por encima de lo que tiene debajo")]
    public float placementMaxHeight = 6f;
    [Tooltip("Altura máxima de lo construido: apilando objetos no se puede pasar de aquí")]
    public float stackMaxHeight = 16f;
    public float placementClearance = 0.15f;
    public float rotationStep = 15f;
    public float heightStep = 0.25f;

    [Header("Física de los objetos")]
    public float objectBounciness = 0.8f;
    public float objectFriction = 0.15f;
    [Tooltip("Al chocar con un objeto la pelota sale con esta velocidad × la que traía (1 = sin empujón)")]
    public float bounceBoost = 1.15f;
    [Tooltip("Cuánto se desvía al azar al rebotar en un objeto: 0 = rebote normal, 1 = cualquier dirección")]
    [Range(0f, 1f)] public float bounceScatter = 0.3f;
    [Tooltip("Tope de velocidad tras un rebote, para que no se acelere sin fin")]
    public float maxBounceSpeed = 26f;

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
