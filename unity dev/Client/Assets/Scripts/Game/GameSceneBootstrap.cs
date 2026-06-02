using UnityEngine;

/// <summary>
/// Holds the pending player count set by MainMenuUI before scene load.
/// GameUI reads this and starts the game when ready.
/// </summary>
public class GameSceneBootstrap : MonoBehaviour
{
    /// <summary>
    /// Set by MainMenuUI before loading the GameScene.
    /// </summary>
    public static int PendingPlayerCount { get; set; }
}
