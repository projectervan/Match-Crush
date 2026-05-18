using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Singleton yang mengelola semua UI di GameplayScene.
/// Mendengarkan events dari GameManager dan GridManager untuk update tampilan.
/// Tempatkan pada Canvas GameObject di GameplayScene.
/// </summary>
public class UIManager : MonoBehaviour
{
    #region Singleton
    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    [Header("HUD Elements")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private TextMeshProUGUI coinsText;

    [Header("Victory Panel")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI victoryTitleText;
    [SerializeField] private TextMeshProUGUI victoryScoreText;
    [SerializeField] private Button nextLevelButton;

    [Header("Game Over Panel")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverTitleText;
    [SerializeField] private TextMeshProUGUI gameOverScoreText;
    [SerializeField] private Button retryButton;

    [Header("Out of Moves Panel")]
    [SerializeField] private GameObject outOfMovesPanel;
    [SerializeField] private TextMeshProUGUI outOfMovesMessageText;
    [SerializeField] private TextMeshProUGUI outOfMovesCostText;
    [SerializeField] private Button buyMovesButton;
    [SerializeField] private TextMeshProUGUI buyMovesButtonText;
    [SerializeField] private Button declineMovesButton;

    [Header("Pause Panel")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;

    private void Start()
    {
        // Sembunyikan semua panel saat mulai
        HideAllPanels();

        // Setup button listeners
        SetupButtons();

        // Subscribe ke GameManager events
        SubscribeToEvents();

        // Initial HUD update
        UpdateHUD();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    #region Event Subscription

    private void SubscribeToEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged += HandleScoreChanged;
            GameManager.Instance.OnMovesChanged += HandleMovesChanged;
            GameManager.Instance.OnCoinsChanged += HandleCoinsChanged;
            GameManager.Instance.OnVictory += HandleVictory;
            GameManager.Instance.OnGameOver += HandleGameOver;
            GameManager.Instance.OnOutOfMoves += HandleOutOfMoves;
            GameManager.Instance.OnStateChanged += HandleStateChanged;
        }

        if (GridManager.Instance != null)
        {
            GridManager.Instance.OnProcessingFinished += HandleProcessingFinished;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged -= HandleScoreChanged;
            GameManager.Instance.OnMovesChanged -= HandleMovesChanged;
            GameManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
            GameManager.Instance.OnVictory -= HandleVictory;
            GameManager.Instance.OnGameOver -= HandleGameOver;
            GameManager.Instance.OnOutOfMoves -= HandleOutOfMoves;
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        if (GridManager.Instance != null)
        {
            GridManager.Instance.OnProcessingFinished -= HandleProcessingFinished;
        }
    }

    #endregion

    #region Button Setup

    private void SetupButtons()
    {
        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(OnNextLevelClicked);

        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryClicked);

        if (buyMovesButton != null)
            buyMovesButton.onClick.AddListener(OnBuyMovesClicked);

        if (declineMovesButton != null)
            declineMovesButton.onClick.AddListener(OnDeclineMovesClicked);

        if (pauseButton != null)
            pauseButton.onClick.AddListener(OnPauseClicked);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResumeClicked);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    #endregion

    #region HUD Update

    /// <summary>
    /// Update seluruh HUD berdasarkan state GameManager saat ini.
    /// </summary>
    private void UpdateHUD()
    {
        if (GameManager.Instance == null) return;

        UpdateLevelText();
        UpdateScoreText(GameManager.Instance.CurrentScore);
        UpdateMovesText(GameManager.Instance.MovesRemaining);
        UpdateCoinsText(GameManager.Instance.Coins);
    }

    private void UpdateLevelText()
    {
        if (levelText != null && GameManager.Instance != null)
        {
            levelText.text = $"Level {GameManager.Instance.CurrentLevel}";
        }
    }

    private void UpdateScoreText(int score)
    {
        if (scoreText != null && GameManager.Instance != null)
        {
            scoreText.text = $"Score: {score} / {GameManager.Instance.TargetScore}";
        }
    }

    private void UpdateMovesText(int moves)
    {
        if (movesText != null)
        {
            movesText.text = $"Moves Left: {moves}";

            // Warna merah jika moves sedikit
            if (moves <= 3)
                movesText.color = Color.red;
            else if (moves <= 5)
                movesText.color = new Color(1f, 0.5f, 0f); // Orange
            else
                movesText.color = Color.white;
        }
    }

    private void UpdateCoinsText(int coins)
    {
        if (coinsText != null)
        {
            coinsText.text = $"{coins}";
        }
    }

    #endregion

    #region Event Handlers

    private void HandleScoreChanged(int newScore)
    {
        UpdateScoreText(newScore);
    }

    private void HandleMovesChanged(int movesRemaining)
    {
        UpdateMovesText(movesRemaining);
    }

    private void HandleCoinsChanged(int coins)
    {
        UpdateCoinsText(coins);
    }

    private void HandleVictory()
    {
        ShowVictoryPanel();
    }

    private void HandleGameOver()
    {
        HideOutOfMovesPanel();
        ShowGameOverPanel();
    }

    private void HandleOutOfMoves()
    {
        ShowOutOfMovesPanel();
    }

    private void HandleStateChanged(GameState newState)
    {
        Debug.Log($"UIManager: Game state changed to {newState}");
    }

    /// <summary>
    /// Dipanggil saat GridManager selesai memproses (destroy/gravity/refill selesai).
    /// Cek apakah game over setelah semua animasi selesai.
    /// </summary>
    private void HandleProcessingFinished()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CheckGameOverCondition();
        }
    }

    #endregion

    #region Panel Display

    private void HideAllPanels()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (outOfMovesPanel != null) outOfMovesPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    /// <summary>
    /// Menampilkan Victory Panel dengan info skor.
    /// </summary>
    private void ShowVictoryPanel()
    {
        if (victoryPanel == null) return;

        victoryPanel.SetActive(true);

        if (victoryTitleText != null)
            victoryTitleText.text = "Level Cleared!";

        if (victoryScoreText != null && GameManager.Instance != null)
            victoryScoreText.text = $"Score: {GameManager.Instance.CurrentScore}";

        Debug.Log("UIManager: Victory panel shown.");
    }

    /// <summary>
    /// Menampilkan Game Over Panel.
    /// </summary>
    private void ShowGameOverPanel()
    {
        if (gameOverPanel == null) return;

        gameOverPanel.SetActive(true);

        if (gameOverTitleText != null)
            gameOverTitleText.text = "Game Over";

        if (gameOverScoreText != null && GameManager.Instance != null)
            gameOverScoreText.text = $"Score: {GameManager.Instance.CurrentScore} / {GameManager.Instance.TargetScore}";

        Debug.Log("UIManager: Game Over panel shown.");
    }

    /// <summary>
    /// Menampilkan Out of Moves Panel.
    /// Jika koin tidak cukup, disable tombol beli.
    /// </summary>
    private void ShowOutOfMovesPanel()
    {
        if (outOfMovesPanel == null) return;

        outOfMovesPanel.SetActive(true);

        if (outOfMovesMessageText != null)
            outOfMovesMessageText.text = "Out of Moves!";

        if (GameManager.Instance != null)
        {
            int cost = GameManager.Instance.ExtraMovesCost;
            int moves = GameManager.Instance.ExtraMovesAmount;
            bool canAfford = GameManager.Instance.CanAffordExtraMoves();

            if (outOfMovesCostText != null)
                outOfMovesCostText.text = $"Buy +{moves} Moves for {cost} Coins?";

            if (buyMovesButton != null)
            {
                buyMovesButton.interactable = canAfford;

                if (buyMovesButtonText != null)
                {
                    buyMovesButtonText.text = canAfford
                        ? $"Buy ({cost} Coins)"
                        : "Not Enough Coins";
                }
            }
        }

        Debug.Log("UIManager: Out of Moves panel shown.");
    }

    /// <summary>
    /// Menyembunyikan Out of Moves Panel.
    /// </summary>
    private void HideOutOfMovesPanel()
    {
        if (outOfMovesPanel != null)
            outOfMovesPanel.SetActive(false);
    }

    /// <summary>
    /// Menampilkan Pause Panel.
    /// </summary>
    private void ShowPausePanel()
    {
        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    /// <summary>
    /// Menyembunyikan Pause Panel.
    /// </summary>
    private void HidePausePanel()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    #endregion

    #region Button Callbacks

    private void OnNextLevelClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.NextLevel();
    }

    private void OnRetryClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RetryLevel();
    }

    /// <summary>
    /// Pemain menekan tombol "Beli +5 Moves".
    /// </summary>
    private void OnBuyMovesClicked()
    {
        if (GameManager.Instance == null) return;

        bool success = GameManager.Instance.BuyExtraMoves();

        if (success)
        {
            // Berhasil beli — sembunyikan panel, game lanjut
            HideOutOfMovesPanel();
            Debug.Log("UIManager: Extra moves purchased, game continues.");
        }
        else
        {
            // Gagal (seharusnya tidak terjadi karena tombol di-disable)
            Debug.LogWarning("UIManager: Failed to buy extra moves.");
        }
    }

    /// <summary>
    /// Pemain menolak membeli moves — trigger Game Over.
    /// </summary>
    private void OnDeclineMovesClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.DeclineExtraMoves();
        }
    }

    private void OnPauseClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PauseGame();
            ShowPausePanel();
        }
    }

    private void OnResumeClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResumeGame();
            HidePausePanel();
        }
    }

    private void OnMainMenuClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.GoToMainMenu();
    }

    #endregion
}
