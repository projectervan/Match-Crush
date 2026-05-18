using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// State permainan.
/// </summary>
public enum GameState
{
    Playing,
    Won,
    Lost,
    Paused
}

/// <summary>
/// Singleton yang mengatur game state, move limit, skor, dan logika procedural level.
/// Tempatkan pada GameObject persistent di GameplayScene.
/// </summary>
public class GameManager : MonoBehaviour
{
    #region Singleton
    public static GameManager Instance { get; private set; }

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

    [Header("Level Settings (Override)")]
    [Tooltip("Jika > 0, override level dari Firestore untuk testing.")]
    [SerializeField] private int debugLevel = 0;

    [Header("Coins Reward")]
    [Tooltip("Koin per sisa move saat menang.")]
    [SerializeField] private int coinsPerMoveLeft = 10;

    // State
    public GameState CurrentState { get; private set; } = GameState.Playing;
    public int CurrentLevel { get; private set; }
    public int TargetScore { get; private set; }
    public int MoveLimit { get; private set; }
    public int MovesRemaining { get; private set; }
    public int CurrentScore { get; private set; }
    public int Coins { get; private set; }

    // Events untuk UI binding
    public event Action<int> OnScoreChanged;          // skor baru
    public event Action<int> OnMovesChanged;          // moves remaining
    public event Action<int> OnCoinsChanged;          // koin berubah
    public event Action<GameState> OnStateChanged;    // state berubah
    public event Action OnVictory;
    public event Action OnGameOver;

    private void Start()
    {
        InitializeLevel();
    }

    /// <summary>
    /// Inisialisasi level berdasarkan data user dari Firestore atau debug override.
    /// </summary>
    private void InitializeLevel()
    {
        // Ambil level dari DatabaseManager (cache) atau gunakan debug value
        if (debugLevel > 0)
        {
            CurrentLevel = debugLevel;
        }
        else if (DatabaseManager.Instance != null && DatabaseManager.Instance.CachedUserData != null)
        {
            CurrentLevel = DatabaseManager.Instance.CachedUserData.CurrentLevel;
        }
        else
        {
            CurrentLevel = 1;
        }

        // Hitung target dan move limit berdasarkan PRD formula
        TargetScore = CurrentLevel * 1000;
        MoveLimit = Mathf.Max(10, 30 - (CurrentLevel / 5));
        MovesRemaining = MoveLimit;
        CurrentScore = 0;
        CurrentState = GameState.Playing;

        // Load coins dari Firestore
        if (DatabaseManager.Instance != null && DatabaseManager.Instance.CachedUserData != null)
        {
            Coins = DatabaseManager.Instance.CachedUserData.Coins;
        }
        else
        {
            Coins = 100; // Default untuk testing
        }

        // Notify UI
        OnScoreChanged?.Invoke(CurrentScore);
        OnMovesChanged?.Invoke(MovesRemaining);
        OnCoinsChanged?.Invoke(Coins);
        OnStateChanged?.Invoke(CurrentState);

        Debug.Log($"GameManager: Level {CurrentLevel} | Target: {TargetScore} | Moves: {MoveLimit} | Coins: {Coins}");
    }

    /// <summary>
    /// Dipanggil oleh Candy.cs setiap kali pemain melakukan swipe yang valid.
    /// Mengurangi 1 move.
    /// </summary>
    public void UseMove()
    {
        if (CurrentState != GameState.Playing) return;

        MovesRemaining--;
        OnMovesChanged?.Invoke(MovesRemaining);

        Debug.Log($"GameManager: Move used. Remaining: {MovesRemaining}");
    }

    /// <summary>
    /// Mengembalikan 1 move (dipanggil jika swap tidak menghasilkan match / revert).
    /// </summary>
    public void UndoMove()
    {
        if (CurrentState != GameState.Playing) return;

        MovesRemaining++;
        OnMovesChanged?.Invoke(MovesRemaining);

        Debug.Log($"GameManager: Move reverted. Remaining: {MovesRemaining}");
    }

    /// <summary>
    /// Menambah skor. Dipanggil oleh GridManager setelah match dihancurkan.
    /// Setelah skor ditambah, cek kondisi menang.
    /// </summary>
    /// <param name="amount">Jumlah skor yang ditambahkan.</param>
    public void AddScore(int amount)
    {
        if (CurrentState != GameState.Playing) return;

        CurrentScore += amount;
        OnScoreChanged?.Invoke(CurrentScore);

        Debug.Log($"GameManager: Score +{amount} = {CurrentScore}/{TargetScore}");

        // Cek kondisi menang
        CheckWinCondition();
    }

    /// <summary>
    /// Cek apakah skor sudah mencapai target.
    /// </summary>
    private void CheckWinCondition()
    {
        if (CurrentScore >= TargetScore)
        {
            TriggerVictory();
        }
    }

    /// <summary>
    /// Dipanggil setelah seluruh proses match selesai (gravity + refill).
    /// Cek apakah move sudah habis dan belum menang.
    /// </summary>
    public void CheckGameOverCondition()
    {
        if (CurrentState != GameState.Playing) return;

        if (MovesRemaining <= 0 && CurrentScore < TargetScore)
        {
            TriggerGameOver();
        }
    }

    /// <summary>
    /// Trigger state menang.
    /// </summary>
    private void TriggerVictory()
    {
        if (CurrentState != GameState.Playing) return;

        CurrentState = GameState.Won;
        OnStateChanged?.Invoke(CurrentState);
        OnVictory?.Invoke();

        // Hitung Moves Left Bonus: 10 koin x sisa moves
        int movesLeftBonus = MovesRemaining * coinsPerMoveLeft;
        if (movesLeftBonus > 0)
        {
            AddCoins(movesLeftBonus);
            Debug.Log($"GameManager: Moves Left Bonus — +{movesLeftBonus} coins ({MovesRemaining} moves x {coinsPerMoveLeft})");
        }

        Debug.Log($"GameManager: VICTORY! Level {CurrentLevel} cleared. Total Coins: {Coins}");

        // Update level dan koin di Firestore
        if (DatabaseManager.Instance != null)
        {
            DatabaseManager.Instance.IncrementLevel();
            DatabaseManager.Instance.SaveHighScore(CurrentScore);
            DatabaseManager.Instance.SaveCoins(Coins);
        }
    }

    /// <summary>
    /// Trigger state kalah.
    /// </summary>
    private void TriggerGameOver()
    {
        if (CurrentState != GameState.Playing) return;

        CurrentState = GameState.Lost;
        OnStateChanged?.Invoke(CurrentState);
        OnGameOver?.Invoke();

        Debug.Log($"GameManager: GAME OVER. Score {CurrentScore}/{TargetScore}");

        // Simpan high score jika lebih tinggi
        if (DatabaseManager.Instance != null)
        {
            DatabaseManager.Instance.SaveHighScore(CurrentScore);
        }
    }

    #region Public Actions (Button Callbacks)

    /// <summary>
    /// Retry level saat ini (reload scene).
    /// </summary>
    public void RetryLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Lanjut ke level berikutnya (reload scene, level sudah di-increment di Firestore).
    /// </summary>
    public void NextLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Pause game.
    /// </summary>
    public void PauseGame()
    {
        if (CurrentState != GameState.Playing) return;

        CurrentState = GameState.Paused;
        Time.timeScale = 0f;
        OnStateChanged?.Invoke(CurrentState);
    }

    /// <summary>
    /// Resume game dari pause.
    /// </summary>
    public void ResumeGame()
    {
        if (CurrentState != GameState.Paused) return;

        CurrentState = GameState.Playing;
        Time.timeScale = 1f;
        OnStateChanged?.Invoke(CurrentState);
    }

    /// <summary>
    /// Kembali ke Main Menu.
    /// </summary>
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenuScene");
    }

    #endregion

    #region Coins

    /// <summary>
    /// Menambah koin dan notify UI.
    /// </summary>
    /// <param name="amount">Jumlah koin yang ditambahkan.</param>
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;

        Coins += amount;
        OnCoinsChanged?.Invoke(Coins);
        Debug.Log($"GameManager: +{amount} coins. Total: {Coins}");
    }

    /// <summary>
    /// Mengurangi koin. Mengembalikan true jika berhasil (cukup saldo).
    /// </summary>
    /// <param name="amount">Jumlah koin yang dikurangi.</param>
    public bool SpendCoins(int amount)
    {
        if (amount <= 0 || Coins < amount) return false;

        Coins -= amount;
        OnCoinsChanged?.Invoke(Coins);

        // Simpan ke Firestore
        if (DatabaseManager.Instance != null)
        {
            DatabaseManager.Instance.SaveCoins(Coins);
        }

        Debug.Log($"GameManager: -{amount} coins. Total: {Coins}");
        return true;
    }

    #endregion

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
