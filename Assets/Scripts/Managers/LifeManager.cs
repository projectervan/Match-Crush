using System;
using UnityEngine;

/// <summary>
/// Singleton yang mengelola sistem nyawa (Lives/Hearts).
/// - Maksimal 5 nyawa.
/// - Nyawa berkurang saat pemain kalah (game over).
/// - Regenerasi 1 nyawa setiap 30 menit (termasuk offline).
/// Tempatkan pada GameObject persistent (DontDestroyOnLoad).
/// </summary>
public class LifeManager : MonoBehaviour
{
    #region Singleton
    public static LifeManager Instance { get; private set; }

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
    #endregion

    [Header("Settings")]
    [SerializeField] private int maxLives = 5;
    [SerializeField] private float regenIntervalMinutes = 30f;

    /// <summary>
    /// Jumlah nyawa saat ini.
    /// </summary>
    public int CurrentLives { get; private set; }

    /// <summary>
    /// Waktu tersisa (detik) sampai nyawa berikutnya di-regenerasi.
    /// </summary>
    public float TimeUntilNextLife { get; private set; }

    /// <summary>
    /// Apakah nyawa sudah penuh.
    /// </summary>
    public bool IsFullLives => CurrentLives >= maxLives;

    /// <summary>
    /// Apakah pemain memiliki nyawa untuk bermain.
    /// </summary>
    public bool HasLives => CurrentLives > 0;

    // Events
    public event Action<int> OnLivesChanged;          // lives baru
    public event Action<float> OnRegenTimerTick;      // detik tersisa
    public event Action OnLivesEmpty;                 // nyawa habis

    // Internal
    private DateTime lastLifeLostTime;
    private float regenIntervalSeconds;
    private bool isRegenerating = false;
    private bool isInitialized = false;

    private void Start()
    {
        regenIntervalSeconds = regenIntervalMinutes * 60f;
    }

    /// <summary>
    /// Inisialisasi dari data Firestore. Dipanggil setelah user data loaded.
    /// Menghitung nyawa yang harus ditambah berdasarkan waktu offline.
    /// </summary>
    public void Initialize()
    {
        if (DatabaseManager.Instance == null || DatabaseManager.Instance.CachedUserData == null)
        {
            // Default jika belum ada data
            CurrentLives = maxLives;
            isInitialized = true;
            OnLivesChanged?.Invoke(CurrentLives);
            return;
        }

        var userData = DatabaseManager.Instance.CachedUserData;
        CurrentLives = userData.Lives;
        lastLifeLostTime = DatabaseManager.Instance.GetLastLifeLostTime();

        // Hitung nyawa yang di-regenerasi saat offline
        CalculateOfflineRegen();

        isInitialized = true;
        OnLivesChanged?.Invoke(CurrentLives);

        Debug.Log($"LifeManager: Initialized — Lives: {CurrentLives}/{maxLives}");

        // Mulai timer regen jika belum penuh
        if (!IsFullLives)
        {
            StartRegenTimer();
        }
    }

    /// <summary>
    /// Menghitung berapa nyawa yang harus di-regenerasi berdasarkan selisih waktu offline.
    /// </summary>
    private void CalculateOfflineRegen()
    {
        if (CurrentLives >= maxLives) return;

        TimeSpan elapsed = DateTime.UtcNow - lastLifeLostTime;
        double totalMinutesElapsed = elapsed.TotalMinutes;

        // Berapa banyak nyawa yang bisa di-regen dalam waktu tersebut
        int livesRestored = Mathf.FloorToInt((float)(totalMinutesElapsed / regenIntervalMinutes));

        if (livesRestored > 0)
        {
            int previousLives = CurrentLives;
            CurrentLives = Mathf.Min(CurrentLives + livesRestored, maxLives);

            Debug.Log($"LifeManager: Offline regen — +{CurrentLives - previousLives} lives (elapsed: {totalMinutesElapsed:F1} min)");

            // Hitung sisa waktu untuk nyawa berikutnya
            double remainderMinutes = totalMinutesElapsed - (livesRestored * regenIntervalMinutes);
            TimeUntilNextLife = (float)((regenIntervalMinutes - remainderMinutes) * 60f);

            // Simpan ke Firestore
            SaveLivesToFirestore();
        }
        else
        {
            // Belum cukup waktu untuk 1 nyawa, hitung sisa waktu
            TimeUntilNextLife = (float)((regenIntervalMinutes - totalMinutesElapsed) * 60f);
            TimeUntilNextLife = Mathf.Max(0f, TimeUntilNextLife);
        }
    }

    private void Update()
    {
        if (!isInitialized) return;
        if (IsFullLives) return;
        if (!isRegenerating) return;

        // Countdown timer
        TimeUntilNextLife -= Time.deltaTime;
        OnRegenTimerTick?.Invoke(TimeUntilNextLife);

        if (TimeUntilNextLife <= 0f)
        {
            // Regenerasi 1 nyawa
            RegenerateOneLife();
        }
    }

    /// <summary>
    /// Mulai timer regenerasi nyawa.
    /// </summary>
    private void StartRegenTimer()
    {
        isRegenerating = true;

        // Jika timer belum di-set (baru kehilangan nyawa), mulai dari interval penuh
        if (TimeUntilNextLife <= 0f)
        {
            TimeUntilNextLife = regenIntervalSeconds;
        }

        Debug.Log($"LifeManager: Regen timer started — next life in {TimeUntilNextLife:F0}s");
    }

    /// <summary>
    /// Regenerasi 1 nyawa dan reset timer atau stop jika sudah penuh.
    /// </summary>
    private void RegenerateOneLife()
    {
        CurrentLives = Mathf.Min(CurrentLives + 1, maxLives);
        OnLivesChanged?.Invoke(CurrentLives);

        Debug.Log($"LifeManager: Life regenerated! Lives: {CurrentLives}/{maxLives}");

        if (IsFullLives)
        {
            // Nyawa penuh, stop timer
            isRegenerating = false;
            TimeUntilNextLife = 0f;
            Debug.Log("LifeManager: Lives full, regen stopped.");
        }
        else
        {
            // Mulai interval berikutnya
            TimeUntilNextLife = regenIntervalSeconds;
        }

        // Simpan ke Firestore
        SaveLivesToFirestore();
    }

    #region Public Methods

    /// <summary>
    /// Mengurangi 1 nyawa. Dipanggil saat pemain kalah (Game Over).
    /// Tidak mengurangi jika nyawa sudah 0.
    /// </summary>
    public void LoseLife()
    {
        if (CurrentLives <= 0)
        {
            Debug.LogWarning("LifeManager: Tidak ada nyawa tersisa.");
            OnLivesEmpty?.Invoke();
            return;
        }

        CurrentLives--;
        lastLifeLostTime = DateTime.UtcNow;

        OnLivesChanged?.Invoke(CurrentLives);

        Debug.Log($"LifeManager: Life lost! Lives: {CurrentLives}/{maxLives}");

        if (CurrentLives <= 0)
        {
            OnLivesEmpty?.Invoke();
        }

        // Mulai regen timer jika belum berjalan
        if (!isRegenerating)
        {
            TimeUntilNextLife = regenIntervalSeconds;
            StartRegenTimer();
        }

        // Simpan ke Firestore
        SaveLivesToFirestore();
    }

    /// <summary>
    /// Menambah nyawa (misal dari reward atau pembelian).
    /// Tidak melebihi maksimum.
    /// </summary>
    /// <param name="amount">Jumlah nyawa yang ditambahkan.</param>
    public void AddLives(int amount)
    {
        if (amount <= 0) return;

        CurrentLives = Mathf.Min(CurrentLives + amount, maxLives);
        OnLivesChanged?.Invoke(CurrentLives);

        Debug.Log($"LifeManager: +{amount} lives! Lives: {CurrentLives}/{maxLives}");

        // Jika penuh, stop regen
        if (IsFullLives)
        {
            isRegenerating = false;
            TimeUntilNextLife = 0f;
        }

        // Simpan ke Firestore
        SaveLivesToFirestore();
    }

    /// <summary>
    /// Isi ulang nyawa ke penuh (misal dari iklan reward atau pembelian premium).
    /// </summary>
    public void RefillLives()
    {
        CurrentLives = maxLives;
        isRegenerating = false;
        TimeUntilNextLife = 0f;

        OnLivesChanged?.Invoke(CurrentLives);
        SaveLivesToFirestore();

        Debug.Log("LifeManager: Lives refilled to max.");
    }

    /// <summary>
    /// Format waktu tersisa menjadi string MM:SS.
    /// </summary>
    public string GetFormattedTimeUntilNextLife()
    {
        if (IsFullLives) return "FULL";

        int minutes = Mathf.FloorToInt(TimeUntilNextLife / 60f);
        int seconds = Mathf.FloorToInt(TimeUntilNextLife % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    #endregion

    #region Persistence

    /// <summary>
    /// Menyimpan data nyawa ke Firestore melalui DatabaseManager.
    /// </summary>
    private void SaveLivesToFirestore()
    {
        if (DatabaseManager.Instance == null) return;

        DatabaseManager.Instance.SaveLivesData(CurrentLives, lastLifeLostTime);
    }

    #endregion

    #region Properties
    public int MaxLives => maxLives;
    public float RegenIntervalMinutes => regenIntervalMinutes;
    #endregion
}
