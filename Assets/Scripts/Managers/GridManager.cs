using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton yang menangani pembuatan, pengelolaan, dan logika grid 8x8.
/// Tempatkan pada GameObject persistent di GameplayScene.
/// </summary>
public class GridManager : MonoBehaviour
{
    #region Singleton
    public static GridManager Instance { get; private set; }

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

    [Header("Grid Settings")]
    [SerializeField] private int width = 8;
    [SerializeField] private int height = 8;
    [SerializeField] private float cellSize = 1.0f;
    [SerializeField] private float spacing = 0.1f;

    [Header("Piece Prefabs")]
    [Tooltip("Array prefab candy/gem dengan warna berbeda (minimal 5 warna).")]
    [SerializeField] private GameObject[] piecePrefabs;

    [Header("Grid Parent")]
    [Tooltip("Transform parent untuk menampung semua piece di hierarchy.")]
    [SerializeField] private Transform gridParent;

    /// <summary>
    /// Array 2D yang menyimpan referensi setiap piece di grid.
    /// </summary>
    public GameObject[,] Board { get; private set; }

    /// <summary>
    /// Offset posisi supaya grid berada di tengah layar.
    /// </summary>
    private Vector2 gridOffset;

    private void Start()
    {
        InitializeGrid();
    }

    /// <summary>
    /// Menginisialisasi grid 8x8, menghitung offset agar center, lalu mengisi piece.
    /// </summary>
    public void InitializeGrid()
    {
        Board = new GameObject[width, height];
        CalculateGridOffset();
        FillBoard();
    }

    /// <summary>
    /// Menghitung offset supaya grid ter-center di world space (0,0).
    /// </summary>
    private void CalculateGridOffset()
    {
        float totalWidth = (width - 1) * (cellSize + spacing);
        float totalHeight = (height - 1) * (cellSize + spacing);

        gridOffset = new Vector2(
            -totalWidth / 2f,
            -totalHeight / 2f
        );
    }

    /// <summary>
    /// Mengisi seluruh grid dengan piece acak tanpa membentuk match-3 awal.
    /// </summary>
    private void FillBoard()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                SpawnPieceAt(x, y);
            }
        }
    }

    /// <summary>
    /// Spawn satu piece pada posisi grid (x, y) dengan memastikan tidak terjadi match awal.
    /// </summary>
    /// <param name="x">Kolom grid.</param>
    /// <param name="y">Baris grid.</param>
    private void SpawnPieceAt(int x, int y)
    {
        int prefabIndex = GetSafePrefabIndex(x, y);
        Vector2 worldPos = GridToWorldPosition(x, y);

        GameObject piece = Instantiate(piecePrefabs[prefabIndex], worldPos, Quaternion.identity, gridParent);
        piece.name = $"Piece ({x},{y})";

        // Simpan koordinat grid ke component Candy/Piece jika ada
        Candy candy = piece.GetComponent<Candy>();
        if (candy != null)
        {
            candy.Init(x, y, prefabIndex);
        }

        Board[x, y] = piece;
    }

    /// <summary>
    /// Memilih index prefab secara acak yang TIDAK membentuk match-3 horizontal atau vertikal.
    /// </summary>
    /// <param name="x">Kolom saat ini.</param>
    /// <param name="y">Baris saat ini.</param>
    /// <returns>Index prefab yang aman.</returns>
    private int GetSafePrefabIndex(int x, int y)
    {
        List<int> availableIndices = new List<int>();

        for (int i = 0; i < piecePrefabs.Length; i++)
        {
            availableIndices.Add(i);
        }

        // Cek horizontal — jika 2 piece di kiri memiliki tipe yang sama, exclude tipe tersebut
        if (x >= 2)
        {
            int leftType1 = GetPieceType(x - 1, y);
            int leftType2 = GetPieceType(x - 2, y);

            if (leftType1 == leftType2 && leftType1 != -1)
            {
                availableIndices.Remove(leftType1);
            }
        }

        // Cek vertikal — jika 2 piece di bawah memiliki tipe yang sama, exclude tipe tersebut
        if (y >= 2)
        {
            int belowType1 = GetPieceType(x, y - 1);
            int belowType2 = GetPieceType(x, y - 2);

            if (belowType1 == belowType2 && belowType1 != -1)
            {
                availableIndices.Remove(belowType1);
            }
        }

        // Pilih secara acak dari index yang tersisa
        if (availableIndices.Count == 0)
        {
            // Fallback: sangat jarang terjadi, pilih random biasa
            return Random.Range(0, piecePrefabs.Length);
        }

        return availableIndices[Random.Range(0, availableIndices.Count)];
    }

    /// <summary>
    /// Mendapatkan tipe piece (prefab index) pada koordinat grid tertentu.
    /// </summary>
    /// <param name="x">Kolom.</param>
    /// <param name="y">Baris.</param>
    /// <returns>Index tipe piece, atau -1 jika kosong/invalid.</returns>
    private int GetPieceType(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return -1;

        if (Board[x, y] == null)
            return -1;

        Candy candy = Board[x, y].GetComponent<Candy>();
        return candy != null ? candy.PieceType : -1;
    }

    /// <summary>
    /// Konversi koordinat grid (x, y) ke posisi dunia (world position).
    /// </summary>
    /// <param name="x">Kolom grid.</param>
    /// <param name="y">Baris grid.</param>
    /// <returns>Posisi world 2D.</returns>
    public Vector2 GridToWorldPosition(int x, int y)
    {
        float posX = gridOffset.x + x * (cellSize + spacing);
        float posY = gridOffset.y + y * (cellSize + spacing);
        return new Vector2(posX, posY);
    }

    /// <summary>
    /// Cek apakah koordinat valid di dalam batas grid.
    /// </summary>
    public bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    /// <summary>
    /// Menghapus piece pada posisi grid tertentu.
    /// </summary>
    public void DestroyPieceAt(int x, int y)
    {
        if (!IsValidPosition(x, y) || Board[x, y] == null) return;

        Destroy(Board[x, y]);
        Board[x, y] = null;
    }

    /// <summary>
    /// Deteksi semua match (horizontal dan vertikal, minimal 3) di seluruh board.
    /// </summary>
    /// <returns>HashSet berisi koordinat semua piece yang termasuk match.</returns>
    public HashSet<Vector2Int> FindAllMatches()
    {
        HashSet<Vector2Int> matchedPositions = new HashSet<Vector2Int>();

        // Cek horizontal
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2; x++)
            {
                int type = GetPieceType(x, y);
                if (type == -1) continue;

                if (GetPieceType(x + 1, y) == type && GetPieceType(x + 2, y) == type)
                {
                    // Temukan semua piece berurutan dengan tipe sama
                    int matchLength = 2;
                    while (x + matchLength < width && GetPieceType(x + matchLength, y) == type)
                    {
                        matchLength++;
                    }

                    for (int i = 0; i < matchLength; i++)
                    {
                        matchedPositions.Add(new Vector2Int(x + i, y));
                    }

                    x += matchLength - 1; // Skip piece yang sudah dihitung
                }
            }
        }

        // Cek vertikal
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 2; y++)
            {
                int type = GetPieceType(x, y);
                if (type == -1) continue;

                if (GetPieceType(x, y + 1) == type && GetPieceType(x, y + 2) == type)
                {
                    int matchLength = 2;
                    while (y + matchLength < height && GetPieceType(x, y + matchLength) == type)
                    {
                        matchLength++;
                    }

                    for (int i = 0; i < matchLength; i++)
                    {
                        matchedPositions.Add(new Vector2Int(x, y + i));
                    }

                    y += matchLength - 1;
                }
            }
        }

        return matchedPositions;
    }

    /// <summary>
    /// Gravity Drop — menurunkan semua piece ke posisi kosong di bawahnya.
    /// </summary>
    public IEnumerator ApplyGravity()
    {
        bool hasMoved = true;

        while (hasMoved)
        {
            hasMoved = false;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height - 1; y++)
                {
                    // Jika posisi ini kosong dan di atasnya ada piece
                    if (Board[x, y] == null && Board[x, y + 1] != null)
                    {
                        // Pindahkan piece ke bawah
                        Board[x, y] = Board[x, y + 1];
                        Board[x, y + 1] = null;

                        // Update posisi visual
                        Candy candy = Board[x, y].GetComponent<Candy>();
                        if (candy != null)
                        {
                            candy.SetGridPosition(x, y);
                            candy.MoveToPosition(GridToWorldPosition(x, y));
                        }
                        else
                        {
                            Board[x, y].transform.position = (Vector3)GridToWorldPosition(x, y);
                        }

                        hasMoved = true;
                    }
                }
            }

            yield return new WaitForSeconds(0.05f);
        }

        // Isi posisi kosong di baris atas dengan piece baru
        yield return StartCoroutine(RefillEmptySlots());
    }

    /// <summary>
    /// Mengisi ulang slot kosong di bagian atas grid dengan piece baru.
    /// </summary>
    private IEnumerator RefillEmptySlots()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (Board[x, y] == null)
                {
                    SpawnPieceAt(x, y);
                    yield return new WaitForSeconds(0.02f);
                }
            }
        }
    }

    /// <summary>
    /// Menghancurkan semua piece yang ter-match dan menjalankan gravity.
    /// </summary>
    /// <param name="matches">Set posisi yang harus dihancurkan.</param>
    public IEnumerator DestroyMatchesAndRefill(HashSet<Vector2Int> matches)
    {
        // Hitung skor berdasarkan jumlah piece yang dihancurkan
        int scoreGained = matches.Count * 10;

        // Hancurkan piece
        foreach (Vector2Int pos in matches)
        {
            DestroyPieceAt(pos.x, pos.y);
        }

        yield return new WaitForSeconds(0.2f);

        // Apply gravity
        yield return StartCoroutine(ApplyGravity());

        yield return new WaitForSeconds(0.1f);

        // Notify GameManager tentang skor
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreGained);
        }

        // Cek match baru setelah refill (chain/combo)
        HashSet<Vector2Int> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
        {
            yield return StartCoroutine(DestroyMatchesAndRefill(newMatches));
        }
    }

    #region Public Properties
    public int Width => width;
    public int Height => height;
    #endregion
}
