using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Component yang menempel pada prefab entry leaderboard.
/// Menampilkan rank, nama, dan skor satu pemain.
///
/// Prefab hierarchy yang direkomendasikan:
/// - EntryItem (HorizontalLayoutGroup)
///   - RankText (TextMeshProUGUI)
///   - NameText (TextMeshProUGUI)
///   - ScoreText (TextMeshProUGUI)
///   - LevelText (TextMeshProUGUI) [opsional]
///   - HighlightBG (Image) [opsional, untuk top 3]
/// </summary>
public class LeaderboardEntryUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("Visual")]
    [Tooltip("Background Image untuk highlight top 3 (opsional).")]
    [SerializeField] private Image backgroundImage;

    [SerializeField] private Color goldColor = new Color(1f, 0.84f, 0f, 0.3f);
    [SerializeField] private Color silverColor = new Color(0.75f, 0.75f, 0.75f, 0.3f);
    [SerializeField] private Color bronzeColor = new Color(0.8f, 0.5f, 0.2f, 0.3f);
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.05f);

    /// <summary>
    /// Set data untuk entry ini.
    /// </summary>
    /// <param name="entry">Data leaderboard entry.</param>
    public void SetData(LeaderboardEntry entry)
    {
        if (rankText != null)
            rankText.text = $"#{entry.Rank}";

        if (nameText != null)
            nameText.text = entry.DisplayName;

        if (scoreText != null)
            scoreText.text = entry.HighScore.ToString("N0");

        if (levelText != null)
            levelText.text = $"Lv.{entry.CurrentLevel}";

        // Highlight top 3
        ApplyRankHighlight(entry.Rank);
    }

    /// <summary>
    /// Terapkan warna highlight berdasarkan rank.
    /// </summary>
    private void ApplyRankHighlight(int rank)
    {
        if (backgroundImage == null) return;

        switch (rank)
        {
            case 1:
                backgroundImage.color = goldColor;
                break;
            case 2:
                backgroundImage.color = silverColor;
                break;
            case 3:
                backgroundImage.color = bronzeColor;
                break;
            default:
                backgroundImage.color = normalColor;
                break;
        }
    }
}
