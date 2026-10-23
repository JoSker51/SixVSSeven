using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TurnPhase { Setup, Shop, Placement, Aiming, Resolving, Finishing, GameOver }

// Turnos y fases. Cada turno: Compra → Colocación → Lanzamiento → Resolución → Finalización.
// Solo recorre a los jugadores que participan (los registra MatchSetup en orden, "hacia la derecha").
// Las monedas del tiro se calculan aquí, una sola vez, con RewardCalculator.
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [SerializeField] private List<CatapultController> catapults = new List<CatapultController>();
    [Tooltip("Compra y Colocación. Sin ellas el turno empieza directo en el lanzamiento.")]
    [SerializeField] private bool preparationPhases = true;
    [SerializeField] private float resolveTime = 2.4f;   // segundos mostrando el resultado del tiro

    private int current = -1;

    public IReadOnlyList<CatapultController> Catapults => catapults;
    public CatapultController Current => current >= 0 && current < catapults.Count ? catapults[current] : null;
    public int CurrentPlayer => Current != null ? Current.playerId : -1;
    public Rock CurrentRock { get; private set; }
    public TurnPhase Phase { get; private set; } = TurnPhase.Setup;
    public int Round { get; private set; } = 1;
    public bool PreparationPhases => preparationPhases;
    public ShotReward LastReward { get; private set; }
    public ShotRecord LastShot { get; private set; }

    public event Action<int> TurnStarted;                      // jugador
    public event Action<TurnPhase> PhaseChanged;
    public event Action<ShotReward, ShotRecord> ShotResolved;

    private static bool IsGameOver => GameManager.Instance != null && GameManager.Instance.GameOver;

    private void Awake() => Instance = this;

    public void Register(CatapultController catapult)
    {
        if (catapult && !catapults.Contains(catapult)) catapults.Add(catapult);
    }

    private void Start()
    {
        foreach (var c in catapults)
        {
            c.SetActive(false);
            c.Fired += OnFired;
        }
        if (GameManager.Instance != null) GameManager.Instance.PlayerWon += OnPlayerWon;
        if (BubbleField.Instance != null) BubbleField.Instance.Generate(MatchConfig.ResolveSeed());
        if (catapults.Count > 0) BeginTurn(0);
    }

    private void OnDestroy()
    {
        foreach (var c in catapults) if (c) c.Fired -= OnFired;
        if (GameManager.Instance != null) GameManager.Instance.PlayerWon -= OnPlayerWon;
        if (Instance == this) Instance = null;
        ShotTracker.End();
    }

    // ¿Puede 'player' hacer algo de la fase 'phase' ahora mismo?
    public bool IsPhase(TurnPhase phase, int player) => !IsGameOver && Phase == phase && player == CurrentPlayer;

    // Botones "Continuar" y "¡A lanzar!" del HUD (o Enter): Compra → Colocación → Lanzamiento
    public bool Advance()
    {
        if (IsGameOver) return false;
        if (Phase == TurnPhase.Shop) { SetPhase(TurnPhase.Placement); return true; }
        if (Phase == TurnPhase.Placement) { SetPhase(TurnPhase.Aiming); return true; }
        return false;
    }

    private void BeginTurn(int index)
    {
        current = index;
        Debug.Log($"Turno del jugador {CurrentPlayer} (ronda {Round})");
        TurnStarted?.Invoke(CurrentPlayer);
        SetPhase(preparationPhases ? TurnPhase.Shop : TurnPhase.Aiming);
    }

    private void SetPhase(TurnPhase phase)
    {
        Phase = phase;
        // La catapulta solo responde en la fase de lanzamiento
        foreach (var c in catapults) if (c != Current || phase != TurnPhase.Aiming) c.SetActive(false);
        if (phase == TurnPhase.Aiming && Current != null) Current.SetActive(true);
        PhaseChanged?.Invoke(phase);
    }

    private void OnFired(CatapultController catapult, Rock rock)
    {
        CurrentRock = rock;
        ShotTracker.Begin(catapult.playerId, rock);
        rock.Finished += OnRockFinished;
    }

    private void OnRockFinished(Rock rock)
    {
        CurrentRock = null;
        var record = ShotTracker.End();
        if (record == null) return;

        SetPhase(TurnPhase.Resolving);
        LastShot = record;
        LastReward = Resolve(record);
        ShotResolved?.Invoke(LastReward, record);
        StartCoroutine(FinishTurn());
    }

    private ShotReward Resolve(ShotRecord record)
    {
        var gm = GameManager.Instance;
        var bal = GameBalance.Current;
        var reward = RewardCalculator.Compute(record, bal);
        if (gm == null) return reward;

        gm.AddCoins(record.ThrowerId, reward.Final);

        // Burbujas de castillo de rivales: robo con tope por tiro (el dueño nunca baja de 0)
        int cap = bal.maxStealPerShot;
        foreach (int owner in record.CastleOwners)
        {
            if (owner == record.ThrowerId || cap <= 0) continue;
            int stolen = gm.Transfer(owner, record.ThrowerId, Mathf.Min(bal.stealAmount, cap));
            reward.Stolen += stolen;
            cap -= stolen;
        }
        Debug.Log($"Tiro J{record.ThrowerId}: burbujas {reward.BubbleCount} (B={reward.B}) P={reward.P} N={reward.N} aro={reward.Ring} → +{reward.Final}, robo {reward.Stolen}");
        return reward;
    }

    private IEnumerator FinishTurn()
    {
        yield return new WaitForSeconds(resolveTime);
        if (IsGameOver) { SetPhase(TurnPhase.GameOver); yield break; }

        // Finalización: las burbujas reventadas vuelven a aparecer y se prepara al siguiente jugador
        SetPhase(TurnPhase.Finishing);
        if (BubbleField.Instance != null) BubbleField.Instance.RespawnPopped();
        int next = (current + 1) % catapults.Count;
        if (next == 0) Round++;
        BeginTurn(next);
    }

    private void OnPlayerWon(int player)
    {
        foreach (var c in catapults) c.SetActive(false);
        Phase = TurnPhase.GameOver;
        PhaseChanged?.Invoke(Phase);
    }
}
