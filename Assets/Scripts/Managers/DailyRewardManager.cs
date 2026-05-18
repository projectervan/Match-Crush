using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;
using Firebase.Auth;

/// <summary>
/// Data model untuk daily login reward yang disimpan di Firestore (sub-field di users collection).
/// </summary>
[FirestoreData]
public class DailyRewardData
{
    [FirestoreProperty]
    public Timestamp LastLoginDate { get; set; }

    [FirestoreProperty]
    public int LoginStreak { get; set; } = 0;
}

/// <summary>
/// Singleton yang menangani sistem Daily Login Reward.
/// Cek apakah hari ini berbeda dari LastLoginDate:
/// - Selisih tepat 1 hari → LoginStreak++ dan berikan koin.
/// - Selisih lebih dari 1 hari → Reset LoginStreak ke 1.
/// - Hari yang sama → Tidak ada reward (sudah klaim hari ini).
///
/// Tempatkan pada GameObject persistent (DontDestroyOnLoad).
/// </summary>
public class DailyRewardManager : MonoBehaviour
{
    #region Singleton
    public static DailyRewardManager Instance { get; private set; }

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

    [Header("Reward Settings")]
    [Tooltip("Koin reward per hari streak (index 0 = hari 1, dst). Jika streak melebihi array, gunakan nilai terakhir.")]
    [SerializeField] private int[] streakRewards = new int[]
    {
        50,   // Hari 1
        100,  // Hari 2
        150,  // Hari 3
        200,  // Hari 4
        250,  // Hari 5
        300,  // Hari 6
        500   // Hari 7+
    };

    // State
    public int CurrentStreak { get; private set; }
    public int TodayReward { get; private set; }
    public bool HasClaimedToday { get; private set; }

    // Events
    public event Action<int, int> OnDailyRewardAvailable; // (streak, coinReward)
    public event Action OnAlreadyClaimed;

    private FirebaseFirestore db;
    private const string USERS_COLLECTION = "users";

    /// <summary>
    /// Dipanggil setelah user berhasil login dan data loaded.
    /// Cek daily reward eligibility.
    /// </summary>
    public void CheckDailyReward()
    {
        db = FirebaseFirestore.DefaultInstance;

        FirebaseUser user = AuthManager.Instance?.CurrentUser;
        if (user == null)
        {
            Debug.LogWarning("DailyRewardManager: No user logged in.");
            return;
        }

        LoadAndProcessDailyReward(user.UserId);
    }

    /// <summary>
    /// Load data daily reward dari Firestore dan proses logika streak.
    /// </summary>
    private async void LoadAndProcessDailyReward(string userId)
    {
        try
        {
            DocumentReference docRef = db.Collection(USERS_COLLECTION).Document(userId);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                Debug.LogWarning("DailyRewardManager: User document not found.");
                return;
            }

            // Ambil field LastLoginDate dan LoginStreak
            DateTime lastLoginDate = DateTime.MinValue;
            int loginStreak = 0;

            if (snapshot.ContainsField("LastLoginDate"))
            {
                Timestamp lastLoginTimestamp = snapshot.GetValue<Timestamp>("LastLoginDate");
                lastLoginDate = lastLoginTimestamp.ToDateTime().Date;
            }

            if (snapshot.ContainsField("LoginStreak"))
            {
                loginStreak = snapshot.GetValue<int>("LoginStreak");
            }

            // Hitung selisih hari
            DateTime today = DateTime.UtcNow.Date;
            int daysDifference = (today - lastLoginDate).Days;

            if (daysDifference == 0)
            {
                // Sudah login hari ini — tidak ada reward
                CurrentStreak = loginStreak;
                HasClaimedToday = true;
                TodayReward = 0;

                OnAlreadyClaimed?.Invoke();
                Debug.Log($"DailyRewardManager: Already claimed today. Streak: {CurrentStreak}");
            }
            else if (daysDifference == 1)
            {
                // Tepat 1 hari selisih — streak berlanjut
                loginStreak++;
                CurrentStreak = loginStreak;
                HasClaimedToday = false;

                int reward = GetRewardForStreak(CurrentStreak);
                TodayReward = reward;

                // Simpan dan berikan reward
                await SaveDailyRewardData(docRef, today, CurrentStreak);
                AwardCoins(reward);

                OnDailyRewardAvailable?.Invoke(CurrentStreak, reward);
                Debug.Log($"DailyRewardManager: Streak continued! Day {CurrentStreak}, Reward: {reward} coins");
            }
            else
            {
                // Selisih lebih dari 1 hari — reset streak
                CurrentStreak = 1;
                HasClaimedToday = false;

                int reward = GetRewardForStreak(CurrentStreak);
                TodayReward = reward;

                // Simpan dan berikan reward
                await SaveDailyRewardData(docRef, today, CurrentStreak);
                AwardCoins(reward);

                OnDailyRewardAvailable?.Invoke(CurrentStreak, reward);
                Debug.Log($"DailyRewardManager: Streak reset! Day 1, Reward: {reward} coins (missed {daysDifference - 1} day(s))");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"DailyRewardManager: Error processing daily reward — {e.Message}");
        }
    }

    /// <summary>
    /// Simpan LastLoginDate dan LoginStreak ke Firestore.
    /// </summary>
    private async System.Threading.Tasks.Task SaveDailyRewardData(DocumentReference docRef, DateTime today, int streak)
    {
        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "LastLoginDate", Timestamp.FromDateTime(today.ToUniversalTime()) },
            { "LoginStreak", streak }
        };

        await docRef.UpdateAsync(updates);
        Debug.Log($"DailyRewardManager: Saved — LastLoginDate: {today:yyyy-MM-dd}, Streak: {streak}");
    }

    /// <summary>
    /// Berikan koin reward ke pemain melalui GameManager atau DatabaseManager.
    /// </summary>
    private void AwardCoins(int amount)
    {
        if (amount <= 0) return;

        // Jika GameManager aktif (di gameplay scene), gunakan itu
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddCoins(amount);
        }
        else if (DatabaseManager.Instance != null && DatabaseManager.Instance.CachedUserData != null)
        {
            // Jika di main menu / login scene, update langsung via DatabaseManager
            int newCoins = DatabaseManager.Instance.CachedUserData.Coins + amount;
            DatabaseManager.Instance.SaveCoins(newCoins);
        }

        Debug.Log($"DailyRewardManager: Awarded {amount} coins.");
    }

    /// <summary>
    /// Mendapatkan jumlah koin reward berdasarkan streak saat ini.
    /// </summary>
    /// <param name="streak">Hari ke-berapa streak.</param>
    /// <returns>Jumlah koin reward.</returns>
    public int GetRewardForStreak(int streak)
    {
        if (streakRewards == null || streakRewards.Length == 0)
            return 50; // fallback

        // Index 0 = hari 1, index 1 = hari 2, dst.
        int index = Mathf.Clamp(streak - 1, 0, streakRewards.Length - 1);
        return streakRewards[index];
    }

    /// <summary>
    /// Mendapatkan preview reward untuk N hari ke depan (untuk UI).
    /// </summary>
    /// <param name="days">Jumlah hari yang ingin di-preview.</param>
    /// <returns>Array reward per hari.</returns>
    public int[] GetRewardPreview(int days = 7)
    {
        int[] preview = new int[days];
        for (int i = 0; i < days; i++)
        {
            preview[i] = GetRewardForStreak(i + 1);
        }
        return preview;
    }

    #region Properties
    public int MaxStreakDays => streakRewards != null ? streakRewards.Length : 7;
    #endregion
}
