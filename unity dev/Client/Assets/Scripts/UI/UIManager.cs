using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages all UI elements for the Cabo card game.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI References")]
    public Text statusText;
    public Text playerScoreText;
    public Text opponentScoreText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    public void UpdateScores(int playerScore, int opponentScore)
    {
        if (playerScoreText != null)
            playerScoreText.text = $"You: {playerScore}";
        if (opponentScoreText != null)
            opponentScoreText.text = $"Opponent: {opponentScore}";
    }
}
