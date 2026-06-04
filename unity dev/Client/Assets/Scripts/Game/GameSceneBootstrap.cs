using Game.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Static bridge between scenes. Holds pending game data before scene load.
/// </summary>
public class GameSceneBootstrap : MonoBehaviour
{
    /// <summary>Hot-seat mode: player count set by MainMenuUI.</summary>
    public static int PendingPlayerCount { get; set; }

    /// <summary>Network mode: GameStartNotify data set before loading GameScene.</summary>
    public static GameStartNotify PendingGameStart { get; set; }

    /// <summary>Load the game scene (from network mode).</summary>
    public static void LoadGameScene()
    {
        SceneManager.LoadScene("GameScene");
    }

    /// <summary>Load the game scene (from hot-seat mode).</summary>
    public static void LoadGameScene(int playerCount)
    {
        PendingPlayerCount = playerCount;
        SceneManager.LoadScene("GameScene"); // old hot-seat uses SampleScene
    }
}
