using System;
using System.Collections.Generic;

// Objetos comprados que el jugador todavía no colocó en la arena.
public class Inventory
{
    private readonly List<ItemDefinition> items = new List<ItemDefinition>();

    public event Action Changed;

    public IReadOnlyList<ItemDefinition> Items => items;
    public int Count => items.Count;

    public bool Add(ItemDefinition item)
    {
        if (item == null || item.prefab == null) return false;
        items.Add(item);
        Changed?.Invoke();
        return true;
    }

    public bool Remove(ItemDefinition item)
    {
        if (!items.Remove(item)) return false;
        Changed?.Invoke();
        return true;
    }

    public bool Contains(ItemDefinition item) => items.Contains(item);

    public int CountOf(ItemDefinition item)
    {
        int n = 0;
        foreach (var i in items) if (i == item) n++;
        return n;
    }
}

// Estado de cada participante de la partida (monedas: GameManager)
public class PlayerState
{
    public int Id;
    public UnityEngine.Color Color;
    public float Angle;
    public CatapultController Catapult;
    public UnityEngine.Transform Castle;
    public Bubble CastleBubble;
    public readonly Inventory Inventory = new Inventory();

    public string Name => $"Jugador {Id + 1}";
}
