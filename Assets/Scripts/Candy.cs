using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Tipe spesial piece.
/// </summary>
public enum SpecialType
{
    None,
    LineHorizontal,  // Hancurkan seluruh baris
    LineVertical     // Hancurkan seluruh kolom
}

/// <summary>
/// Component yang menempel pada setiap prefab piece/candy di grid.
/// Menangani input swipe, animasi pertukaran posisi (Lerp), power-up state, dan komunikasi dengan GridManager.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class Candy : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    #region Grid Data

    /// <summary>
    /// Posisi kolom pada grid (0-7).
    /// </summary>
    public int GridX { get; private set; }

    /// <summary>
    /// Posisi baris pada grid (0-7).
    /// </summary>
    public int GridY { get; private set; }

    /// <summary>
    /// Index tipe prefab (warna) piece ini.
    /// </summary>
    public int PieceType { get; private set; }

    /// <summary>
    /// Tipe spesial piece ini (None = biasa).
    /// </summary>
    public SpecialType Special { get; private set; } = SpecialType.None;

    /// <summary>
    /// Apakah piece ini merupakan special/power-up piece.
    /// </summary>
    public bool IsSpecial => Special != SpecialType.None;

    #endregion

    [Header("Animation Settings")]
    [SerializeField] private float swapDuration = 0.2f;
    [SerializeField] private float swipeThreshold = 0.3f;

    [Header("Special Visual")]
    [Tooltip("Sprite overlay untuk LinePiece horizontal (opsional, bisa null).")]
    [SerializeField] private Sprite lineHorizontalOverlay;
    [Tooltip("Sprite overlay untuk LinePiece vertikal (opsional, bisa null).")]
    [SerializeField] private Sprite lineVerticalOverlay;
    [Tooltip("GameObject child yang menampilkan indicator spesial (arrow/glow).")]
    [SerializeField] private GameObject specialIndicator;

    // State
    private Vector2 pointerDownPos;
    private Vector2 pointerUpPos;
    private bool isMoving = false;
    private bool inputEnabled = true;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Dipanggil oleh GridManager saat piece di-spawn.
    /// </summary>
    /// <param name="x">Kolom grid.</param>
    /// <param name="y">Baris grid.</param>
    /// <param name="type">Index tipe warna/prefab.</param>
    public void Init(int x, int y, int type)
    {
        GridX = x;
        GridY = y;
        PieceType = type;
        Special = SpecialType.None;

        if (specialIndicator != null)
            specialIndicator.SetActive(false);
    }

    /// <summary>
    /// Update koordinat grid (dipanggil setelah gravity drop).
    /// </summary>
    public void SetGridPosition(int x, int y)
    {
        GridX = x;
        GridY = y;
        gameObject.name = $"Piece ({x},{y}){(IsSpecial ? $" [{Special}]" : "")}";
    }

    #region Special Piece Setup

    /// <summary>
    /// Mengubah piece ini menjadi LinePiece (horizontal atau vertikal).
    /// Dipanggil oleh GridManager saat match-4 terdeteksi.
    /// </summary>
    /// <param name="direction">Arah blast: LineHorizontal atau LineVertical.</param>
    public void SetAsLinePiece(SpecialType direction)
    {
        Special = direction;
        gameObject.name = $"Piece ({GridX},{GridY}) [{Special}]";

        // Visual indicator
        UpdateSpecialVisual();

        Debug.Log($"Candy: Piece ({GridX},{GridY}) set as {Special}");
    }

    /// <summary>
    /// Update tampilan visual sesuai tipe spesial.
    /// </summary>
    private void UpdateSpecialVisual()
    {
        if (spriteRenderer == null) return;

        switch (Special)
        {
            case SpecialType.LineHorizontal:
                if (lineHorizontalOverlay != null)
                    spriteRenderer.sprite = lineHorizontalOverlay;
                else
                    // Fallback: tint warna untuk indikasi
                    spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
                break;

            case SpecialType.LineVertical:
                if (lineVerticalOverlay != null)
                    spriteRenderer.sprite = lineVerticalOverlay;
                else
                    spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
                break;
        }

        // Aktifkan indicator child (arrow/glow) jika ada
        if (specialIndicator != null)
        {
            specialIndicator.SetActive(true);

            // Rotasi arrow sesuai arah
            if (Special == SpecialType.LineHorizontal)
                specialIndicator.transform.rotation = Quaternion.Euler(0, 0, 0);
            else if (Special == SpecialType.LineVertical)
                specialIndicator.transform.rotation = Quaternion.Euler(0, 0, 90);
        }
    }

    #endregion

    #region Input Detection

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!inputEnabled || isMoving) return;

        // Block input jika grid sedang processing
        if (GridManager.Instance != null && GridManager.Instance.IsProcessing) return;

        pointerDownPos = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!inputEnabled || isMoving) return;

        // Block input jika grid sedang processing
        if (GridManager.Instance != null && GridManager.Instance.IsProcessing) return;

        pointerUpPos = eventData.position;

        Vector2 swipeDelta = pointerUpPos - pointerDownPos;

        // Cek apakah swipe cukup panjang
        if (swipeDelta.magnitude < swipeThreshold * Screen.dpi / 160f)
            return;

        // Tentukan arah swipe dominan
        Vector2Int direction = GetSwipeDirection(swipeDelta);

        // Hitung posisi target
        int targetX = GridX + direction.x;
        int targetY = GridY + direction.y;

        // Validasi posisi target
        if (GridManager.Instance != null && GridManager.Instance.IsValidPosition(targetX, targetY))
        {
            GameObject targetPiece = GridManager.Instance.Board[targetX, targetY];
            if (targetPiece != null)
            {
                StartCoroutine(TrySwap(targetPiece, direction));
            }
        }
    }

    /// <summary>
    /// Menentukan arah swipe (4 arah) berdasarkan delta posisi pointer.
    /// </summary>
    private Vector2Int GetSwipeDirection(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            return delta.x > 0 ? Vector2Int.right : Vector2Int.left;
        }
        else
        {
            return delta.y > 0 ? Vector2Int.up : Vector2Int.down;
        }
    }

    #endregion

    #region Swap Logic

    /// <summary>
    /// Mencoba menukar posisi dengan piece target.
    /// Jika menghasilkan match, swap dikonfirmasi. Jika tidak, swap di-revert.
    /// </summary>
    private IEnumerator TrySwap(GameObject targetObject, Vector2Int direction)
    {
        isMoving = true;
        inputEnabled = false;

        Candy targetCandy = targetObject.GetComponent<Candy>();
        if (targetCandy == null)
        {
            isMoving = false;
            inputEnabled = true;
            yield break;
        }

        // Notify GameManager bahwa pemain melakukan move
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UseMove();
        }

        // Lakukan swap visual
        yield return StartCoroutine(AnimateSwap(targetCandy));

        // Update data grid
        SwapInGrid(targetCandy);

        // Cek apakah swap menghasilkan match
        var matches = GridManager.Instance.FindAllMatches();

        if (matches.Count > 0)
        {
            // Match ditemukan — hancurkan dan refill
            yield return GridManager.Instance.StartCoroutine(
                GridManager.Instance.DestroyMatchesAndRefill(matches)
            );
        }
        else
        {
            // Tidak ada match — revert swap
            yield return StartCoroutine(AnimateSwap(targetCandy));
            SwapInGrid(targetCandy);

            // Kembalikan move yang sudah dipakai karena swap invalid
            if (GameManager.Instance != null)
            {
                GameManager.Instance.UndoMove();
            }
        }

        isMoving = false;
        inputEnabled = true;
    }

    /// <summary>
    /// Animasi pertukaran posisi antara piece ini dan target menggunakan Lerp.
    /// </summary>
    private IEnumerator AnimateSwap(Candy target)
    {
        Vector3 startPosA = transform.position;
        Vector3 startPosB = target.transform.position;

        float elapsed = 0f;

        while (elapsed < swapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / swapDuration);

            transform.position = Vector3.Lerp(startPosA, startPosB, t);
            target.transform.position = Vector3.Lerp(startPosB, startPosA, t);

            yield return null;
        }

        // Snap ke posisi akhir
        transform.position = startPosB;
        target.transform.position = startPosA;
    }

    /// <summary>
    /// Menukar data grid (Board array) dan koordinat internal antara dua piece.
    /// </summary>
    private void SwapInGrid(Candy target)
    {
        GridManager.Instance.Board[GridX, GridY] = target.gameObject;
        GridManager.Instance.Board[target.GridX, target.GridY] = this.gameObject;

        int tempX = GridX;
        int tempY = GridY;

        SetGridPosition(target.GridX, target.GridY);
        target.SetGridPosition(tempX, tempY);
    }

    #endregion

    #region Movement (Gravity)

    /// <summary>
    /// Animasi piece bergerak ke posisi baru (digunakan oleh gravity drop).
    /// </summary>
    public void MoveToPosition(Vector2 targetPosition)
    {
        StartCoroutine(AnimateMoveTo(targetPosition));
    }

    /// <summary>
    /// Coroutine animasi Lerp menuju posisi tertentu.
    /// </summary>
    private IEnumerator AnimateMoveTo(Vector2 targetPosition)
    {
        isMoving = true;
        Vector3 startPos = transform.position;
        Vector3 endPos = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);

        float elapsed = 0f;
        float moveDuration = swapDuration * 0.8f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        transform.position = endPos;
        isMoving = false;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Mengaktifkan atau menonaktifkan input pada piece ini.
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    #endregion
}
