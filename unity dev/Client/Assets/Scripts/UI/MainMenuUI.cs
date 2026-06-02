using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Main menu for Glucose Cabo. Allows selecting player count and starting a game.
/// Loads the GameScene (SampleScene) with the selected player count.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    public Text titleText;
    public Button btn2Players;
    public Button btn3Players;
    public Button btn4Players;
    public Text infoText;

    [Header("Scene Loading")]
    public string gameSceneName = "SampleScene";

    private void Start()
    {
        if (titleText != null)
            titleText.text = "Glucose Cabo\n血糖卡波";
        if (infoText != null)
            infoText.text = "Hot-Seat Mode\nPlayers take turns on the same device";

        if (btn2Players != null) btn2Players.onClick.AddListener(() => StartGame(2));
        if (btn3Players != null) btn3Players.onClick.AddListener(() => StartGame(3));
        if (btn4Players != null) btn4Players.onClick.AddListener(() => StartGame(4));
    }

    private void StartGame(int numPlayers)
    {
        // Pass player count to the game scene via static field
        GameSceneBootstrap.PendingPlayerCount = numPlayers;

        Debug.Log($"[MainMenuUI] Loading {gameSceneName} with {numPlayers} players...");
        SceneManager.LoadScene(gameSceneName);
    }
}
