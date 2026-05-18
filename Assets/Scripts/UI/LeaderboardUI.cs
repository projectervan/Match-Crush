using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI Controller untuk menampilkan leaderboard (Top 10 players).
/// Tempatkan pada Panel/Canvas di MainMenuScene.
///
/// Hierarchy yang direkomendasikan:
/// - LeaderboardPanel (GameObject dengan script ini)
///   - Title (TextMeshProUGUI: "Leaderboard")
///   - ScrollView
///     - Viewport
///       - Content (assign ke 'contentParent', VerticalLayoutGroup)
///   - CloseButton (Button)
///   - RefreshButton (Button)
///   - LoadingIndicator (GameObject, aktif saat loading)
///   - EmptyStateText (TextMeshProUGUI: "No data available")
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Parent Transform (Content) di ScrollView untuk menampung item entry.")]
    [SerializeField] private Transform contentParent;

    [Tooltip("Prefab untuk satu baris entry leaderboard.")]
    [SerializeField] private GameObject entryPrefab;

    [Header("UI Elements")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button refreshButton;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private TextMeshProUGUI emptyStateText;

    [Header("Panel")]
    [SerializeField] private GameObject leaderboardPanel;

    private void Start()
    {
        // Setup button listeners
        if (closeButton != null)
            closeButton.onClick.AddListener(HideLeaderboard);

        if (refreshButton != null)
            refreshButton.onClick.AddListener(RefreshLeaderboard);

        // Subscribe ke event DatabaseManager
        if (DatabaseManager.Instance != null)
        {
            DatabaseManager.Instance.OnLeaderboardLoaded += PopulateLeaderboard;
            DatabaseManager.Instance.OnDatabaseError += HandleError;
        }

        // Sembunyikan panel saat start
        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (DatabaseManager.Instance != null)
        {
            DatabaseManager.Instance.OnLeaderboardLoaded -= PopulateLeaderboard;
            DatabaseManager.Instance.OnDatabaseError -= HandleError;
        }
    }

    /// <summary>
    /// Menampilkan panel leaderboard dan fetch data dari Firestore.
    /// Dipanggil oleh tombol "Leaderboard" di Main Menu.
    /// </summary>
    public void ShowLeaderboard()
    {
        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(true);

        SetLoadingState(true);

        if (DatabaseManager.Instance != null)
        {
            DatabaseManager.Instance.GetTopPlayers(10);
        }
        else
        {
            SetLoadingState(false);
            ShowEmptyState("Database not available.");
        }
    }

    /// <summary>
    /// Menyembunyikan panel leaderboard.
    /// </summary>
    public void HideLeaderboard()
    {
        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(false);
    }

    /// <summary>
    /// Refresh data leaderboard.
    /// </summary>
    public void RefreshLeaderboard()
    {
        SetLoadingState(true);
        ClearEntries();

        if (DatabaseManager.Instance != null)
        {
            DatabaseManager.Instance.GetTopPlayers(10);
        }
    }

    /// <summary>
    /// Callback saat data leaderboard berhasil diambil.
    /// Mengisi UI list dengan entry.
    /// </summary>
    /// <param name="entries">List data leaderboard dari Firestore.</param>
    private void PopulateLeaderboard(List<LeaderboardEntry> entries)
    {
        SetLoadingState(false);
        ClearEntries();

        if (entries == null || entries.Count == 0)
        {
            ShowEmptyState("No players found.");
            return;
        }

        HideEmptyState();

        foreach (LeaderboardEntry entry in entries)
        {
            SpawnEntryItem(entry);
        }

        Debug.Log($"LeaderboardUI: Displayed {entries.Count} entries.");
    }

    /// <summary>
    /// Spawn satu item entry di dalam ScrollView content.
    /// </summary>
    private void SpawnEntryItem(LeaderboardEntry entry)
    {
        if (entryPrefab == null || contentParent == null)
        {
            Debug.LogWarning("LeaderboardUI: entryPrefab atau contentParent belum di-assign.");
            return;
        }

        GameObject item = Instantiate(entryPrefab, contentParent);

        // Cari dan set komponen UI pada prefab entry
        LeaderboardEntryUI entryUI = item.GetComponent<LeaderboardEntryUI>();
        if (entryUI != null)
        {
            entryUI.SetData(entry);
        }
        else
        {
            // Fallback: cari TextMeshProUGUI children secara manual
            TextMeshProUGUI[] texts = item.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 3)
            {
                texts[0].text = $"#{entry.Rank}";
                texts[1].text = entry.DisplayName;
                texts[2].text = entry.HighScore.ToString("N0");
            }
            else if (texts.Length >= 1)
            {
                texts[0].text = $"#{entry.Rank}  {entry.DisplayName}  —  {entry.HighScore:N0}";
            }
        }
    }

    /// <summary>
    /// Hapus semua entry item dari content parent.
    /// </summary>
    private void ClearEntries()
    {
        if (contentParent == null) return;

        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// Set loading state (tampilkan/sembunyikan indicator).
    /// </summary>
    private void SetLoadingState(bool isLoading)
    {
        if (loadingIndicator != null)
            loadingIndicator.SetActive(isLoading);

        if (refreshButton != null)
            refreshButton.interactable = !isLoading;
    }

    /// <summary>
    /// Tampilkan pesan kosong.
    /// </summary>
    private void ShowEmptyState(string message)
    {
        if (emptyStateText != null)
        {
            emptyStateText.gameObject.SetActive(true);
            emptyStateText.text = message;
        }
    }

    /// <summary>
    /// Sembunyikan pesan kosong.
    /// </summary>
    private void HideEmptyState()
    {
        if (emptyStateText != null)
            emptyStateText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Handle error dari DatabaseManager.
    /// </summary>
    private void HandleError(string errorMessage)
    {
        SetLoadingState(false);
        ShowEmptyState($"Error: {errorMessage}");
    }
}
