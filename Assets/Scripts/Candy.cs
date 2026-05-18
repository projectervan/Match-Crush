using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Component yang menempel pada setiap prefab piece/candy di grid.
/// Menangani input swipe, animasi pertukaran posisi (Lerp), dan komunikasi dengan GridManager.
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

    #endregion

    [Header("Animation Settings")]
    [SerializeField] private float swapDuration = 0.2f;
    [SerializeField] private float swipeThreshold = 0.3f;

    // State
    private Vector2 pointerDownPos;
    private Vector2 pointerUpPos;
    private bool isMoving = false;
    private bool inputEnabled = true;

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
    }

    /// <summary>
    /// Update koordinat grid (dipanggil setelah gravity drop).
    /// </summary>
    public void SetGridPosition(int x, int y)
    {
        GridX = x;
        GridY = y;
        gameObject.name = $"Piece ({x},{y})";
    }

    #region Input Detection

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!inputEnabled || isMoving) return;
        pointerDownPos = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!inputEnabled || isMoving) return;
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
    /// <param name="delta">Selisih posisi pointer down dan up.</param>
    /// <returns>Vektor arah (1,0), (-1,0), (0,1), atau (0,-1).</returns>
    private Vector2Int GetSwipeDirection(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            // Horizontal
            return delta.x > 0 ? Vector2Int.right : Vector2Int.left;
        }
        else
        {
            // Vertikal
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
        // Swap di Board array
        GridManager.Instance.Board[GridX, GridY] = target.gameObject;
        GridManager.Instance.Board[target.GridX, target.GridY] = this.gameObject;

        // Swap koordinat internal
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
    /// <param name="targetPosition">Posisi world target.</param>
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
        float moveDuration = swapDuration * 0.8f; // Sedikit lebih cepat dari swap

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
