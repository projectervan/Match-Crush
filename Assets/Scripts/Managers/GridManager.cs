using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton yang menangani pembuatan, pengelolaan, dan logika grid 8x8.
/// Mendeteksi match-3+, menghancurkan objek, menerapkan gravity, dan spawn piece baru.
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

    [Header("Timing")]
    [SerializeField] private float destroyDelay = 0.25f;
    [SerializeField] private float gravityStepDelay = 0.05f;
    [SerializeField] private float refillSpawnDelay = 0.03f;
    [SerializeField] private float postRefillDelay = 0.15f;

    [Header("Scoring")]
    [SerializeField] private int baseScorePerPiece = 10;
    [SerializeField] private int comboMultiplierStep = 5;

    /// <summary>
    /// Array 2D yang menyimpan referensi setiap piece di grid.
    /// </summary>
    public GameObject[,] Board { get; private set; }

    /// <summary>
    /// Apakah grid sedang dalam proses (destroy/gravity/refill). Input harus di-block.
    /// </summary>
    public bool IsProcessing { get; private set; }

    /// <summary>
    /// Offset posisi supaya grid berada di tengah layar.
    /// </summary>
    private Vector2 gridOffset;

    /// <summary>
    /// Chain/combo counter untuk skor multiplier.
    /// </summary>
    private int currentComboCount;

    // Events
    public event Action OnProcessingStarted;
    public event Action OnProcessingFinished;
    public event Action<int> OnScoreAwarded; // skor yang diberikan per batch destroy

    private void Start()
    {
        InitializeGrid();
    }

    #region Initialization

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
    /// Piece langsung muncul di posisi grid (tanpa animasi jatuh).
    /// </summary>
    private void SpawnPieceAt(int x, int y)
    {
        int prefabIndex = GetSafePrefabIndex(x, y);
        Vector2 worldPos = GridToWorldPosition(x, y);

        GameObject piece = Instantiate(piecePrefabs[prefabIndex], worldPos, Quaternion.identity, gridParent);
        piece.name = $"Piece ({x},{y})";

        Candy candy = piece.GetComponent<Candy>();
        if (candy != null)
        {
            candy.Init(x, y, prefabIndex);
        }

        Board[x, y] = piece;
    }

    /// <summary>
    /// Spawn piece baru di atas layar dan animasikan jatuh ke posisi grid.
    /// Digunakan saat refill setelah gravity.
    /// </summary>
    private void SpawnPieceWithDropAnimation(int x, int y)
    {
        int prefabIndex = GetSafePrefabIndex(x, y);
        Vector2 targetPos = GridToWorldPosition(x, y);

        // Spawn di atas layar (offset ke atas berdasarkan kolom kosong yang tersisa)
        float spawnOffsetY = (height - y) * (cellSize + spacing);
        Vector2 spawnPos = new Vector2(targetPos.x, gridOffset.y + height * (cellSize + spacing) + spawnOffsetY);

        GameObject piece = Instantiate(piecePrefabs[prefabIndex], spawnPos, Quaternion.identity, gridParent);
        piece.name = $"Piece ({x},{y})";

        Candy candy = piece.GetComponent<Candy>();
        if (candy != null)
        {
            candy.Init(x, y, prefabIndex);
            candy.MoveToPosition(targetPos);
        }
        else
        {
            // Fallback tanpa Candy component — langsung set posisi
            piece.transform.position = (Vector3)targetPos;
        }

        Board[x, y] = piece;
    }

    /// <summary>
    /// Memilih index prefab secara acak yang TIDAK membentuk match-3 horizontal atau vertikal.
    /// </summary>
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
            return Random.Range(0, piecePrefabs.Length);
        }

        return availableIndices[Random.Range(0, availableIndices.Count)];
    }

    #endregion

    #region Match Detection

    /// <summary>
    /// Mendapatkan tipe piece (prefab index) pada koordinat grid tertentu.
    /// </summary>
    /// <returns>Index tipe piece, atau -1 jika kosong/invalid.</returns>
    public int GetPieceType(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return -1;

        if (Board[x, y] == null)
            return -1;

        Candy candy = Board[x, y].GetComponent<Candy>();
        return candy != null ? candy.PieceType : -1;
    }

    /// <summary>
    /// Deteksi semua match (horizontal dan vertikal, minimal 3) di seluruh board.
    /// Juga mendeteksi match lebih dari 3 (4, 5, dst.) sebagai satu grup.
    /// </summary>
    /// <returns>HashSet berisi koordinat semua piece yang termasuk match.</returns>
    public HashSet<Vector2Int> FindAllMatches()
    {
        HashSet<Vector2Int> matchedPositions = new HashSet<Vector2Int>();

        // --- Cek horizontal ---
        for (int y = 0; y < height; y++)
        {
            int matchStart = 0;

            while (matchStart < width)
            {
                int type = GetPieceType(matchStart, y);

                if (type == -1)
                {
                    matchStart++;
                    continue;
                }

                // Hitung panjang berurutan dengan tipe sama
                int matchEnd = matchStart + 1;
                while (matchEnd < width && GetPieceType(matchEnd, y) == type)
                {
                    matchEnd++;
                }

                int matchLength = matchEnd - matchStart;

                // Jika 3 atau lebih, tambahkan semua ke set
                if (matchLength >= 3)
                {
                    for (int i = matchStart; i < matchEnd; i++)
                    {
                        matchedPositions.Add(new Vector2Int(i, y));
                    }
                }

                matchStart = matchEnd;
            }
        }

        // --- Cek vertikal ---
        for (int x = 0; x < width; x++)
        {
            int matchStart = 0;

            while (matchStart < height)
            {
                int type = GetPieceType(x, matchStart);

                if (type == -1)
                {
                    matchStart++;
                    continue;
                }

                int matchEnd = matchStart + 1;
                while (matchEnd < height && GetPieceType(x, matchEnd) == type)
                {
                    matchEnd++;
                }

                int matchLength = matchEnd - matchStart;

                if (matchLength >= 3)
                {
                    for (int i = matchStart; i < matchEnd; i++)
                    {
                        matchedPositions.Add(new Vector2Int(x, i));
                    }
                }

                matchStart = matchEnd;
            }
        }

        return matchedPositions;
    }

    /// <summary>
    /// Cek match hanya di sekitar posisi tertentu (optimisasi setelah swap).
    /// </summary>
    /// <param name="positions">Posisi-posisi yang perlu dicek.</param>
    /// <returns>HashSet berisi koordinat piece yang match.</returns>
    public HashSet<Vector2Int> FindMatchesAt(params Vector2Int[] positions)
    {
        HashSet<Vector2Int> matchedPositions = new HashSet<Vector2Int>();

        foreach (Vector2Int pos in positions)
        {
            // Cek horizontal dari posisi ini
            FindLineMatch(pos.x, pos.y, 1, 0, matchedPositions);
            // Cek vertikal dari posisi ini
            FindLineMatch(pos.x, pos.y, 0, 1, matchedPositions);
        }

        return matchedPositions;
    }

    /// <summary>
    /// Mencari match di satu garis (horizontal atau vertikal) dari posisi awal.
    /// </summary>
    private void FindLineMatch(int startX, int startY, int dirX, int dirY, HashSet<Vector2Int> results)
    {
        int type = GetPieceType(startX, startY);
        if (type == -1) return;

        List<Vector2Int> line = new List<Vector2Int>();
        line.Add(new Vector2Int(startX, startY));

        // Cari ke arah positif
        int x = startX + dirX;
        int y = startY + dirY;
        while (IsValidPosition(x, y) && GetPieceType(x, y) == type)
        {
            line.Add(new Vector2Int(x, y));
            x += dirX;
            y += dirY;
        }

        // Cari ke arah negatif
        x = startX - dirX;
        y = startY - dirY;
        while (IsValidPosition(x, y) && GetPieceType(x, y) == type)
        {
            line.Add(new Vector2Int(x, y));
            x -= dirX;
            y -= dirY;
        }

        // Jika total 3 atau lebih, tambahkan ke hasil
        if (line.Count >= 3)
        {
            foreach (Vector2Int pos in line)
            {
                results.Add(pos);
            }
        }
    }

    #endregion

    #region Destroy, Gravity & Refill

    /// <summary>
    /// Entry point utama: menghancurkan match, apply gravity, refill, dan cek chain.
    /// Dipanggil oleh Candy.cs setelah swap yang valid.
    /// </summary>
    /// <param name="matches">Set posisi yang harus dihancurkan.</param>
    public IEnumerator DestroyMatchesAndRefill(HashSet<Vector2Int> matches)
    {
        IsProcessing = true;
        OnProcessingStarted?.Invoke();
        currentComboCount = 0;

        yield return StartCoroutine(ProcessMatchCycle(matches));

        IsProcessing = false;
        OnProcessingFinished?.Invoke();
    }

    /// <summary>
    /// Siklus rekursif: destroy → gravity → refill → cek ulang.
    /// </summary>
    private IEnumerator ProcessMatchCycle(HashSet<Vector2Int> matches)
    {
        currentComboCount++;

        // --- 1. Hitung dan berikan skor ---
        int scoreGained = CalculateScore(matches.Count, currentComboCount);
        OnScoreAwarded?.Invoke(scoreGained);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreGained);
        }

        // --- 2. Hancurkan semua piece yang match ---
        DestroyMatches(matches);

        yield return new WaitForSeconds(destroyDelay);

        // --- 3. Apply gravity — jatuhkan piece ke bawah ---
        yield return StartCoroutine(ApplyGravity());

        yield return new WaitForSeconds(postRefillDelay);

        // --- 4. Spawn piece baru di slot kosong (baris atas) ---
        yield return StartCoroutine(RefillEmptySlots());

        yield return new WaitForSeconds(postRefillDelay);

        // --- 5. Cek match baru (chain/combo) ---
        HashSet<Vector2Int> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
        {
            yield return StartCoroutine(ProcessMatchCycle(newMatches));
        }
    }

    /// <summary>
    /// Menghitung skor berdasarkan jumlah piece dan combo multiplier.
    /// Match 3 = base, Match 4 = base + bonus, Match 5+ = base + bonus lebih besar.
    /// Combo meningkatkan skor setiap chain berturut-turut.
    /// </summary>
    private int CalculateScore(int pieceCount, int comboLevel)
    {
        // Base score per piece
        int baseScore = pieceCount * baseScorePerPiece;

        // Bonus untuk match lebih dari 3
        int matchBonus = 0;
        if (pieceCount == 4)
            matchBonus = 20;
        else if (pieceCount == 5)
            matchBonus = 50;
        else if (pieceCount > 5)
            matchBonus = 50 + (pieceCount - 5) * 30;

        // Combo multiplier (chain ke-2 = +5, chain ke-3 = +10, dst.)
        int comboBonus = (comboLevel - 1) * comboMultiplierStep * pieceCount;

        return baseScore + matchBonus + comboBonus;
    }

    /// <summary>
    /// Menghancurkan semua piece pada posisi yang diberikan.
    /// </summary>
    private void DestroyMatches(HashSet<Vector2Int> matches)
    {
        foreach (Vector2Int pos in matches)
        {
            DestroyPieceAt(pos.x, pos.y);
        }
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
    /// Gravity Drop — menurunkan semua piece ke posisi kosong di bawahnya.
    /// Iterasi dari bawah ke atas per kolom agar piece jatuh secara berurutan.
    /// </summary>
    public IEnumerator ApplyGravity()
    {
        bool hasMoved = true;

        while (hasMoved)
        {
            hasMoved = false;

            for (int x = 0; x < width; x++)
            {
                // Iterasi dari baris kedua dari bawah ke atas
                for (int y = 1; y < height; y++)
                {
                    if (Board[x, y] != null && Board[x, y - 1] == null)
                    {
                        // Cari posisi paling bawah yang kosong di kolom ini
                        int targetY = y - 1;
                        while (targetY > 0 && Board[x, targetY - 1] == null)
                        {
                            targetY--;
                        }

                        // Pindahkan piece
                        Board[x, targetY] = Board[x, y];
                        Board[x, y] = null;

                        // Update posisi visual dengan animasi
                        Candy candy = Board[x, targetY].GetComponent<Candy>();
                        if (candy != null)
                        {
                            candy.SetGridPosition(x, targetY);
                            candy.MoveToPosition(GridToWorldPosition(x, targetY));
                        }
                        else
                        {
                            Board[x, targetY].transform.position = (Vector3)GridToWorldPosition(x, targetY);
                        }

                        hasMoved = true;
                    }
                }
            }

            yield return new WaitForSeconds(gravityStepDelay);
        }
    }

    /// <summary>
    /// Mengisi ulang slot kosong dengan piece baru yang jatuh dari atas.
    /// Scan dari bawah ke atas agar piece di bawah spawn duluan.
    /// </summary>
    private IEnumerator RefillEmptySlots()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (Board[x, y] == null)
                {
                    SpawnPieceWithDropAnimation(x, y);
                    yield return new WaitForSeconds(refillSpawnDelay);
                }
            }
        }
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Konversi koordinat grid (x, y) ke posisi dunia (world position).
    /// </summary>
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

    #endregion

    #region Public Properties
    public int Width => width;
    public int Height => height;
    public int CurrentCombo => currentComboCount;
    #endregion
}
