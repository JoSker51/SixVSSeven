using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    // Orden = sentido de los turnos ("hacia la derecha"). Ordena la lista en el Inspector
    // siguiendo ese sentido alrededor de la cuadrícula.
    [SerializeField] private List<CatapultController> catapults = new List<CatapultController>();

    private int current = -1;

    // Para el HUD y la cámara
    public IReadOnlyList<CatapultController> Catapults => catapults;
    public CatapultController Current => current >= 0 && current < catapults.Count ? catapults[current] : null;
    public Rock CurrentRock { get; private set; }

    private void Start()
    {
        foreach (var c in catapults)
        {
            c.SetActive(false);
            c.Fired += OnFired;
        }
        if (GameManager.Instance != null) GameManager.Instance.PlayerWon += OnPlayerWon;
        NextTurn();
    }

    private void OnPlayerWon(int player)
    {
        foreach (var c in catapults) c.SetActive(false);
    }

    private void OnDestroy()
    {
        foreach (var c in catapults) if (c) c.Fired -= OnFired;
        if (GameManager.Instance != null) GameManager.Instance.PlayerWon -= OnPlayerWon;
    }

    private void OnFired(CatapultController catapult, Rock rock)
    {
        CurrentRock = rock;
        // El turno termina cuando la roca desaparece (a los 2 s de puntuar/tocar el suelo)
        rock.Finished += _ => { CurrentRock = null; NextTurn(); };
    }

    private void NextTurn()
    {
        if (catapults.Count == 0) return;
        if (GameManager.Instance != null && GameManager.Instance.GameOver) return;
        current = (current + 1) % catapults.Count;
        catapults[current].SetActive(true);
        Debug.Log($"Turno del jugador {catapults[current].playerId}");
    }
}
