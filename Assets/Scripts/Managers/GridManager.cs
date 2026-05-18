using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data hasil deteksi match — menyimpan posisi dan panjang match per grup.
/// </summary>
public struct MatchGroup
{
    public List<Vector2Int> Positions;
    public int Length;
    public bool IsHorizontal;

    public MatchGroup(List<Vector2Int> positions, bool isHorizontal)
    {
        Positions = positions;
        Length = positions.Count;
        IsHorizontal = isHorizontal;
    }
}

/// <summary>
/// Singleton yang menangani pembuatan, pengelolaan, dan logika grid 8x8.
/// Mendeteksi match-3+, special piece (LinePiece), menghancurkan objek,
/// menerapkan gravity, dan spawn piece baru.
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
    [SerializeField] private float lineBlastDelay = 0.1f;

    [Header("Scoring")]
    [SerializeField] private int baseScorePerPiece = 10;
    [SerializeField] private int comboMultiplierStep = 5;
    [SerializeField] private int lineBlastBonusScore = 50;

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
    public event Action<int> OnScoreAwarded;

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
    /// Spawn satu piece pada posisi grid (x, y) tanpa match awal.
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
    /// Spawn piece baru di atas layar dengan animasi jatuh.
    /// </summary>
    private void SpawnPieceWithDropAnimation(int x, int y)
    {
        int prefabIndex = GetSafePrefabIndex(x, y);
        Vector2 targetPos = GridToWorldPosition(x, y);

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
            piece.transform.position = (Vector3)targetPos;
        }

        Board[x, y] = piece;
    }

    /// <summary>
    /// Memilih index prefab yang TIDAK membentuk match-3 awal.
    /// </summary>
    private int GetSafePrefabIndex(int x, int y)
    {
        List<int> availableIndices = new List<int>();

        for (int i = 0; i < piecePrefabs.Length; i++)
        {
            availableIndices.Add(i);
        }

        if (x >= 2)
        {
            int leftType1 = GetPieceType(x - 1, y);
            int leftType2 = GetPieceType(x - 2, y);

            if (leftType1 == leftType2 && leftType1 != -1)
            {
                availableIndices.Remove(leftType1);
            }
        }

        if (y >= 2)
        {
            int belowType1 = GetPieceType(x, y - 1);
            int belowType2 = GetPieceType(x, y - 2);

            if (belowType1 == belowType2 && belowType1 != -1)
            {
                availableIndices.Remove(belowType1);
            }
        }

        if (availableIndices.Count == 0)
        {
            return UnityEngine.Random.Range(0, piecePrefabs.Length);
        }

        return availableIndices[UnityEngine.Random.Range(0, availableIndices.Count)];
    }

    #endregion

    #region Match Detection

    /// <summary>
    /// Mendapatkan tipe piece pada koordinat grid.
    /// </summary>
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
    /// Mendapatkan Candy component pada posisi grid.
    /// </summary>
    private Candy GetCandyAt(int x, int y)
    {
        if (!IsValidPosition(x, y) || Board[x, y] == null) return null;
        return Board[x, y].GetComponent<Candy>();
    }

    /// <summary>
    /// Deteksi semua match di board. Mengembalikan posisi dan juga MatchGroup info.
    /// </summary>
    public HashSet<Vector2Int> FindAllMatches()
    {
        HashSet<Vector2Int> matchedPositions = new HashSet<Vector2Int>();

        // Cek horizontal
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

                int matchEnd = matchStart + 1;
                while (matchEnd < width && GetPieceType(matchEnd, y) == type)
                {
                    matchEnd++;
                }

                int matchLength = matchEnd - matchStart;

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

        // Cek vertikal
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
    /// Deteksi match groups dengan informasi panjang dan orientasi.
    /// Digunakan untuk menentukan apakah perlu spawn LinePiece.
    /// </summary>
    public List<MatchGroup> FindAllMatchGroups()
    {
        List<MatchGroup> groups = new List<MatchGroup>();

        // Horizontal groups
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

                int matchEnd = matchStart + 1;
                while (matchEnd < width && GetPieceType(matchEnd, y) == type)
                {
                    matchEnd++;
                }

                int matchLength = matchEnd - matchStart;

                if (matchLength >= 3)
                {
                    List<Vector2Int> positions = new List<Vector2Int>();
                    for (int i = matchStart; i < matchEnd; i++)
                    {
                        positions.Add(new Vector2Int(i, y));
                    }
                    groups.Add(new MatchGroup(positions, true));
                }

                matchStart = matchEnd;
            }
        }

        // Vertical groups
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
                    List<Vector2Int> positions = new List<Vector2Int>();
                    for (int i = matchStart; i < matchEnd; i++)
                    {
                        positions.Add(new Vector2Int(x, i));
                    }
                    groups.Add(new MatchGroup(positions, false));
                }

                matchStart = matchEnd;
            }
        }

        return groups;
    }

    /// <summary>
    /// Cek match hanya di sekitar posisi tertentu.
    /// </summary>
    public HashSet<Vector2Int> FindMatchesAt(params Vector2Int[] positions)
    {
        HashSet<Vector2Int> matchedPositions = new HashSet<Vector2Int>();

        foreach (Vector2Int pos in positions)
        {
            FindLineMatch(pos.x, pos.y, 1, 0, matchedPositions);
            FindLineMatch(pos.x, pos.y, 0, 1, matchedPositions);
        }

        return matchedPositions;
    }

    /// <summary>
    /// Mencari match di satu garis dari posisi awal.
    /// </summary>
    private void FindLineMatch(int startX, int startY, int dirX, int dirY, HashSet<Vector2Int> results)
    {
        int type = GetPieceType(startX, startY);
        if (type == -1) return;

        List<Vector2Int> line = new List<Vector2Int>();
        line.Add(new Vector2Int(startX, startY));

        int x = startX + dirX;
        int y = startY + dirY;
        while (IsValidPosition(x, y) && GetPieceType(x, y) == type)
        {
            line.Add(new Vector2Int(x, y));
            x += dirX;
            y += dirY;
        }

        x = startX - dirX;
        y = startY - dirY;
        while (IsValidPosition(x, y) && GetPieceType(x, y) == type)
        {
            line.Add(new Vector2Int(x, y));
            x -= dirX;
            y -= dirY;
        }

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
    /// Entry point utama: hancurkan match, spawn LinePiece jika match-4, apply gravity, refill, chain.
    /// </summary>
    public IEnumerator DestroyMatchesAndRefill(HashSet<Vector2Int> matches)
    {
        IsProcessing = true;
        OnProcessingStarted?.Invoke();
        currentComboCount = 0;

        yield return StartCoroutine(ProcessMatchCycle());

        IsProcessing = false;
        OnProcessingFinished?.Invoke();
    }

    /// <summary>
    /// Siklus rekursif: detect groups → spawn specials → trigger specials → destroy → gravity → refill → cek ulang.
    /// </summary>
    private IEnumerator ProcessMatchCycle()
    {
        currentComboCount++;

        // --- 1. Deteksi match groups untuk menentukan special pieces ---
        List<MatchGroup> matchGroups = FindAllMatchGroups();

        if (matchGroups.Count == 0) yield break;

        // --- 2. Kumpulkan semua posisi yang akan dihancurkan ---
        HashSet<Vector2Int> allMatched = new HashSet<Vector2Int>();
        List<LinePieceSpawnInfo> linePieceSpawns = new List<LinePieceSpawnInfo>();

        foreach (MatchGroup group in matchGroups)
        {
            foreach (Vector2Int pos in group.Positions)
            {
                allMatched.Add(pos);
            }

            // Jika match-4 (exactly 4), spawn LinePiece
            if (group.Length == 4)
            {
                // LinePiece spawn di posisi tengah dari match
                Vector2Int spawnPos = group.Positions[1]; // posisi ke-2 dari 4
                SpecialType lineType = group.IsHorizontal
                    ? SpecialType.LineVertical   // Match horizontal → blast vertikal (perpendicular)
                    : SpecialType.LineHorizontal; // Match vertikal → blast horizontal

                linePieceSpawns.Add(new LinePieceSpawnInfo
                {
                    Position = spawnPos,
                    Type = lineType,
                    PieceColorType = GetPieceType(spawnPos.x, spawnPos.y)
                });
            }
        }

        // --- 3. Cek dan trigger LinePiece yang ada di posisi match ---
        HashSet<Vector2Int> lineBlastPositions = new HashSet<Vector2Int>();
        CollectLineBlastTargets(allMatched, lineBlastPositions);

        // Tambahkan line blast positions ke set destroy
        foreach (Vector2Int pos in lineBlastPositions)
        {
            allMatched.Add(pos);
        }

        // --- 4. Hitung skor ---
        int scoreGained = CalculateScore(allMatched.Count, currentComboCount);

        // Bonus skor untuk line blast
        if (lineBlastPositions.Count > 0)
        {
            scoreGained += lineBlastBonusScore;
        }

        OnScoreAwarded?.Invoke(scoreGained);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreGained);
        }

        // --- 5. Hancurkan semua piece ---
        // Simpan posisi yang akan jadi LinePiece agar tidak dihancurkan
        HashSet<Vector2Int> preservedPositions = new HashSet<Vector2Int>();
        foreach (var spawnInfo in linePieceSpawns)
        {
            preservedPositions.Add(spawnInfo.Position);
        }

        foreach (Vector2Int pos in allMatched)
        {
            if (!preservedPositions.Contains(pos))
            {
                DestroyPieceAt(pos.x, pos.y);
            }
        }

        // --- 6. Convert preserved pieces menjadi LinePiece ---
        foreach (var spawnInfo in linePieceSpawns)
        {
            Candy candy = GetCandyAt(spawnInfo.Position.x, spawnInfo.Position.y);
            if (candy != null)
            {
                candy.SetAsLinePiece(spawnInfo.Type);
            }
        }

        yield return new WaitForSeconds(destroyDelay);

        // --- 7. Apply gravity ---
        yield return StartCoroutine(ApplyGravity());

        yield return new WaitForSeconds(postRefillDelay);

        // --- 8. Refill ---
        yield return StartCoroutine(RefillEmptySlots());

        yield return new WaitForSeconds(postRefillDelay);

        // --- 9. Cek match baru (chain/combo) ---
        HashSet<Vector2Int> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
        {
            yield return StartCoroutine(ProcessMatchCycle());
        }
    }

    /// <summary>
    /// Kumpulkan semua posisi yang harus dihancurkan akibat LinePiece yang terkena match.
    /// </summary>
    private void CollectLineBlastTargets(HashSet<Vector2Int> matchedPositions, HashSet<Vector2Int> blastTargets)
    {
        // Cek setiap posisi yang di-match, apakah ada LinePiece
        foreach (Vector2Int pos in matchedPositions)
        {
            Candy candy = GetCandyAt(pos.x, pos.y);
            if (candy == null || !candy.IsSpecial) continue;

            // LinePiece terdeteksi di posisi match — trigger blast
            if (candy.Special == SpecialType.LineHorizontal)
            {
                // Hancurkan seluruh baris
                for (int x = 0; x < width; x++)
                {
                    blastTargets.Add(new Vector2Int(x, pos.y));
                }
                Debug.Log($"GridManager: LineHorizontal blast at row {pos.y}");
            }
            else if (candy.Special == SpecialType.LineVertical)
            {
                // Hancurkan seluruh kolom
                for (int y = 0; y < height; y++)
                {
                    blastTargets.Add(new Vector2Int(pos.x, y));
                }
                Debug.Log($"GridManager: LineVertical blast at column {pos.x}");
            }
        }

        // Recursive: cek apakah blast mengenai LinePiece lain (chain reaction)
        HashSet<Vector2Int> additionalBlasts = new HashSet<Vector2Int>();
        foreach (Vector2Int pos in blastTargets)
        {
            if (matchedPositions.Contains(pos)) continue; // sudah diproses

            Candy candy = GetCandyAt(pos.x, pos.y);
            if (candy == null || !candy.IsSpecial) continue;

            // LinePiece lain terkena blast — trigger juga
            if (candy.Special == SpecialType.LineHorizontal)
            {
                for (int x = 0; x < width; x++)
                {
                    additionalBlasts.Add(new Vector2Int(x, pos.y));
                }
            }
            else if (candy.Special == SpecialType.LineVertical)
            {
                for (int y = 0; y < height; y++)
                {
                    additionalBlasts.Add(new Vector2Int(pos.x, y));
                }
            }
        }

        // Gabungkan additional blasts
        foreach (Vector2Int pos in additionalBlasts)
        {
            blastTargets.Add(pos);
        }
    }

    /// <summary>
    /// Menghitung skor berdasarkan jumlah piece dan combo.
    /// </summary>
    private int CalculateScore(int pieceCount, int comboLevel)
    {
        int baseScore = pieceCount * baseScorePerPiece;

        int matchBonus = 0;
        if (pieceCount == 4)
            matchBonus = 20;
        else if (pieceCount == 5)
            matchBonus = 50;
        else if (pieceCount > 5)
            matchBonus = 50 + (pieceCount - 5) * 30;

        int comboBonus = (comboLevel - 1) * comboMultiplierStep * pieceCount;

        return baseScore + matchBonus + comboBonus;
    }

    /// <summary>
    /// Menghapus piece pada posisi grid.
    /// </summary>
    public void DestroyPieceAt(int x, int y)
    {
        if (!IsValidPosition(x, y) || Board[x, y] == null) return;

        Destroy(Board[x, y]);
        Board[x, y] = null;
    }

    /// <summary>
    /// Gravity Drop — menurunkan piece ke posisi kosong di bawah.
    /// </summary>
    public IEnumerator ApplyGravity()
    {
        bool hasMoved = true;

        while (hasMoved)
        {
            hasMoved = false;

            for (int x = 0; x < width; x++)
            {
                for (int y = 1; y < height; y++)
                {
                    if (Board[x, y] != null && Board[x, y - 1] == null)
                    {
                        int targetY = y - 1;
                        while (targetY > 0 && Board[x, targetY - 1] == null)
                        {
                            targetY--;
                        }

                        Board[x, targetY] = Board[x, y];
                        Board[x, y] = null;

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
    /// Mengisi ulang slot kosong dengan piece baru.
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
    /// Konversi koordinat grid ke posisi world.
    /// </summary>
    public Vector2 GridToWorldPosition(int x, int y)
    {
        float posX = gridOffset.x + x * (cellSize + spacing);
        float posY = gridOffset.y + y * (cellSize + spacing);
        return new Vector2(posX, posY);
    }

    /// <summary>
    /// Cek apakah koordinat valid.
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

    #region Helper Structs

    /// <summary>
    /// Info untuk spawn LinePiece setelah match-4.
    /// </summary>
    private struct LinePieceSpawnInfo
    {
        public Vector2Int Position;
        public SpecialType Type;
        public int PieceColorType;
    }

    #endregion
}
