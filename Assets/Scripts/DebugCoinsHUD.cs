using UnityEngine;

// HUD temporal para probar. Se reemplaza por la interfaz real en el paso 6.
public class DebugCoinsHUD : MonoBehaviour
{
    private void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        for (int i = 0; i < gm.PlayerCount; i++)
            GUI.Label(new Rect(20, 20 + i * 32, 400, 30), $"Jugador {i}: {gm.GetCoins(i)} / {gm.WinCoins}", style);

        if (gm.GameOver)
            GUI.Label(new Rect(20, 20 + gm.PlayerCount * 32, 400, 30), "¡Partida terminada!", style);
    }
}
