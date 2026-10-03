using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private DifficultyManager difficultyManager;

    [Header("Game State UI")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Final Results")]
    [SerializeField] private TextMeshProUGUI enemiesKilledText;
    [SerializeField] private TextMeshProUGUI totalScoreText;

    private bool gameOver;
    private bool gameStarted;

    private void Awake()
    {
        Time.timeScale = 0f;
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    private void Start()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerMovement>();
        if (difficultyManager == null)
            difficultyManager = FindFirstObjectByType<DifficultyManager>();

        if (player != null)
            player.Died += HandlePlayerDied;
        else
            Debug.LogError("GameManager requires a PlayerMovement reference.", this);

        if (difficultyManager == null)
            Debug.LogError("GameManager requires a DifficultyManager reference to show final results.", this);

        if (startPanel != null)
            startPanel.SetActive(true);
    }

    private void Update()
    {
        if (gameStarted || gameOver || startPanel != null)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            StartGame();
    }

    /// <summary>Connect this method to the Start button's OnClick event.</summary>
    public void StartGame()
    {
        if (gameOver)
            return;

        gameStarted = true;
        if (startPanel != null)
            startPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void ResetGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void HandlePlayerDied()
    {
        if (gameOver)
            return;

        gameOver = true;
        Time.timeScale = 0f;

        if (difficultyManager != null)
        {
            if (enemiesKilledText != null)
                enemiesKilledText.text = $"Enemies Killed: {difficultyManager.KillCount}";
            if (totalScoreText != null)
                totalScoreText.text = $"Total Score: {difficultyManager.Score}";
        }

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
    }

    private void OnDisable()
    {
        if (player != null)
            player.Died -= HandlePlayerDied;
    }
}
