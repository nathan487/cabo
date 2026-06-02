using UnityEngine;

/// <summary>
/// Main game manager for Cabo card game.
/// Handles game state, turn management, and coordinates between players.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    public int cardCount = 4;
    public int maxPlayers = 2;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Debug.Log("[GameManager] Cabo Game Manager initialized.");
    }
}
