using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Firestore;
using Firebase.Auth;

/// <summary>
/// Data model untuk dokumen user di Firestore.
/// </summary>
[FirestoreData]
public class UserData
{
    [FirestoreProperty]
    public string DisplayName { get; set; }

    [FirestoreProperty]
    public int CurrentLevel { get; set; } = 1;

    [FirestoreProperty]
    public int HighScore { get; set; } = 0;
}

/// <summary>
/// Singleton yang menangani semua operasi baca/tulis ke Firebase Firestore.
/// Tempatkan pada GameObject persistent (DontDestroyOnLoad).
/// </summary>
public class DatabaseManager : MonoBehaviour
{
    #region Singleton
    public static DatabaseManager Instance { get; private set; }

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

    private FirebaseFirestore db;
    private const string USERS_COLLECTION = "users";

    /// <summary>
    /// Data user yang sedang login (cache lokal).
    /// </summary>
    public UserData CachedUserData { get; private set; }

    // Events untuk UI binding
    public event Action<UserData> OnUserDataLoaded;
    public event Action<string> OnDatabaseError;

    private void Start()
    {
        db = FirebaseFirestore.DefaultInstance;
    }

    /// <summary>
    /// Mendapatkan referensi dokumen user berdasarkan UserId.
    /// </summary>
    private DocumentReference GetUserDocument(string userId)
    {
        return db.Collection(USERS_COLLECTION).Document(userId);
    }

    /// <summary>
    /// Membuat atau memperbarui dokumen user di Firestore setelah login pertama kali.
    /// Jika dokumen sudah ada, data tidak ditimpa (merge).
    /// </summary>
    /// <param name="user">FirebaseUser dari AuthManager.</param>
    public async void CreateUserIfNotExists(FirebaseUser user)
    {
        if (user == null)
        {
            Debug.LogError("DatabaseManager: User is null, cannot create document.");
            return;
        }

        try
        {
            DocumentReference docRef = GetUserDocument(user.UserId);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                // User baru — buat dokumen dengan nilai default
                UserData newUser = new UserData
                {
                    DisplayName = user.DisplayName ?? "Player",
                    CurrentLevel = 1,
                    HighScore = 0
                };

                await docRef.SetAsync(newUser);
                CachedUserData = newUser;
                Debug.Log($"DatabaseManager: New user document created for {user.DisplayName}");
            }
            else
            {
                // User sudah ada — load data
                CachedUserData = snapshot.ConvertTo<UserData>();
                Debug.Log($"DatabaseManager: Existing user loaded — Level {CachedUserData.CurrentLevel}");
            }

            OnUserDataLoaded?.Invoke(CachedUserData);
        }
        catch (Exception e)
        {
            Debug.LogError($"DatabaseManager: Error creating/checking user — {e.Message}");
            OnDatabaseError?.Invoke(e.Message);
        }
    }

    /// <summary>
    /// Mengambil data user dari Firestore.
    /// </summary>
    /// <param name="userId">UserId dari Firebase Auth.</param>
    public async void LoadUserData(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("DatabaseManager: UserId kosong, tidak bisa load data.");
            return;
        }

        try
        {
            DocumentReference docRef = GetUserDocument(userId);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                CachedUserData = snapshot.ConvertTo<UserData>();
                Debug.Log($"DatabaseManager: Data loaded — {CachedUserData.DisplayName}, Level {CachedUserData.CurrentLevel}, HighScore {CachedUserData.HighScore}");
                OnUserDataLoaded?.Invoke(CachedUserData);
            }
            else
            {
                Debug.LogWarning("DatabaseManager: Dokumen user tidak ditemukan.");
                OnDatabaseError?.Invoke("User data not found.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"DatabaseManager: Error loading data — {e.Message}");
            OnDatabaseError?.Invoke(e.Message);
        }
    }

    /// <summary>
    /// Menyimpan CurrentLevel ke Firestore (dipanggil saat level cleared).
    /// </summary>
    /// <param name="newLevel">Level baru setelah increment.</param>
    public async void SaveCurrentLevel(int newLevel)
    {
        FirebaseUser user = AuthManager.Instance?.CurrentUser;
        if (user == null)
        {
            Debug.LogError("DatabaseManager: Tidak ada user yang login.");
            return;
        }

        try
        {
            DocumentReference docRef = GetUserDocument(user.UserId);
            await docRef.UpdateAsync("CurrentLevel", newLevel);

            if (CachedUserData != null)
                CachedUserData.CurrentLevel = newLevel;

            Debug.Log($"DatabaseManager: CurrentLevel updated to {newLevel}");
        }
        catch (Exception e)
        {
            Debug.LogError($"DatabaseManager: Error saving level — {e.Message}");
            OnDatabaseError?.Invoke(e.Message);
        }
    }

    /// <summary>
    /// Menyimpan HighScore ke Firestore (hanya jika skor baru lebih tinggi).
    /// </summary>
    /// <param name="score">Skor yang dicapai pemain.</param>
    public async void SaveHighScore(int score)
    {
        FirebaseUser user = AuthManager.Instance?.CurrentUser;
        if (user == null)
        {
            Debug.LogError("DatabaseManager: Tidak ada user yang login.");
            return;
        }

        // Hanya update jika skor lebih tinggi dari yang tersimpan
        if (CachedUserData != null && score <= CachedUserData.HighScore)
        {
            Debug.Log("DatabaseManager: Skor tidak lebih tinggi dari HighScore, skip update.");
            return;
        }

        try
        {
            DocumentReference docRef = GetUserDocument(user.UserId);
            await docRef.UpdateAsync("HighScore", score);

            if (CachedUserData != null)
                CachedUserData.HighScore = score;

            Debug.Log($"DatabaseManager: HighScore updated to {score}");
        }
        catch (Exception e)
        {
            Debug.LogError($"DatabaseManager: Error saving high score — {e.Message}");
            OnDatabaseError?.Invoke(e.Message);
        }
    }

    /// <summary>
    /// Increment level dan simpan ke Firestore.
    /// Dipanggil oleh VictoryPanel saat pemain menyelesaikan level.
    /// </summary>
    public void IncrementLevel()
    {
        if (CachedUserData == null) return;

        int newLevel = CachedUserData.CurrentLevel + 1;
        SaveCurrentLevel(newLevel);
    }

    /// <summary>
    /// Menyimpan beberapa field sekaligus ke Firestore.
    /// </summary>
    /// <param name="updates">Dictionary berisi field dan nilai yang akan diupdate.</param>
    public async void SaveMultipleFields(Dictionary<string, object> updates)
    {
        FirebaseUser user = AuthManager.Instance?.CurrentUser;
        if (user == null)
        {
            Debug.LogError("DatabaseManager: Tidak ada user yang login.");
            return;
        }

        try
        {
            DocumentReference docRef = GetUserDocument(user.UserId);
            await docRef.UpdateAsync(updates);

            // Update local cache
            if (CachedUserData != null)
            {
                if (updates.ContainsKey("CurrentLevel"))
                    CachedUserData.CurrentLevel = (int)updates["CurrentLevel"];
                if (updates.ContainsKey("HighScore"))
                    CachedUserData.HighScore = (int)updates["HighScore"];
                if (updates.ContainsKey("DisplayName"))
                    CachedUserData.DisplayName = (string)updates["DisplayName"];
            }

            Debug.Log("DatabaseManager: Multiple fields updated successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"DatabaseManager: Error saving multiple fields — {e.Message}");
            OnDatabaseError?.Invoke(e.Message);
        }
    }
}
